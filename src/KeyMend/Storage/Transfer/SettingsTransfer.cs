using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KeyMend.Actions;
using KeyMend.Infrastructure;
using KeyMend.Layouts;

namespace KeyMend.Storage.Transfer;

/// <summary>Чем закончился экспорт.</summary>
internal enum ExportResult
{
    /// <summary>Файл записан.</summary>
    Saved,

    /// <summary>Скрипты не умещаются в допустимый размер файла.</summary>
    TooLarge,

    /// <summary>Не удалось записать файл.</summary>
    Failed,
}

/// <summary>Почему загрузка не состоялась.</summary>
internal enum ImportFailure
{
    /// <summary>Всё прошло.</summary>
    None,

    /// <summary>Пароль от секретов не подошёл; ничего не менялось.</summary>
    WrongPassword,

    /// <summary>Не удалось сделать резервную копию; ничего не менялось.</summary>
    BackupFailed,

    /// <summary>Не удалось записать секреты; настройки не менялись.</summary>
    SecretsFailed,

    /// <summary>Не удалось записать файлы скриптов; настройки не менялись.</summary>
    ScriptsFailed,

    /// <summary>Не удалось записать настройки.</summary>
    SettingsFailed,

    /// <summary>Резервная копия не читается или повреждена.</summary>
    BadBackup,
}

/// <summary>Файл переноса, прочитанный и проверенный, но ещё не применённый.</summary>
/// <param name="FileName">Имя файла без папки.</param>
/// <param name="Package">Содержимое.</param>
/// <param name="Conflicts">Конфликты с текущими настройками, о которых надо спросить при объединении.</param>
internal sealed record ImportPreview(string FileName, TransferPackage Package, IReadOnlyList<ImportConflict> Conflicts)
{
    /// <summary>В файле есть секреты, для которых нужен пароль.</summary>
    public bool HasSecrets => Package.Secrets is not null;
}

/// <summary>Итог открытия файла.</summary>
/// <param name="Preview">Файл или <c>null</c> при ошибке.</param>
/// <param name="Error">Вид ошибки.</param>
internal sealed record OpenResult(ImportPreview? Preview, TransferError Error);

/// <summary>Итог загрузки или восстановления.</summary>
/// <param name="Failure">Причина неудачи или <see cref="ImportFailure.None"/>.</param>
/// <param name="Report">Числа загрузки; у восстановления <c>null</c>.</param>
/// <param name="SkippedScripts">Файлы скриптов, оставленные как у владельца.</param>
/// <param name="MissingPrograms">Названия правил, чьи программы не найдены на этом компьютере.</param>
internal sealed record ImportResult(
    ImportFailure Failure, ImportReport? Report, IReadOnlyList<string> SkippedScripts, IReadOnlyList<string> MissingPrograms)
{
    /// <summary>Создаёт результат с ошибкой.</summary>
    /// <param name="failure">Причина.</param>
    /// <returns>Результат без отчёта.</returns>
    public static ImportResult Fail(ImportFailure failure) => new(failure, null, [], []);
}

/// <summary>
/// Перенос настроек (SPEC F8): экспорт в <c>.keymend</c>, проверка файла, загрузка с резервной копией и восстановление из копии.
/// Вызывается из потока UI (читает и меняет <see cref="SettingsService"/>); тяжёлое (шифрование, архив, диск) уходит в пул.
/// Порядок загрузки: копия → секреты → скрипты → настройки; при сбое на любом шаге прежнее состояние возвращается. Тексты секретов,
/// пароль и содержимое сокращений в журнал не попадают.
/// </summary>
/// <param name="paths">Пути данных программы.</param>
/// <param name="settings">Текущие настройки и их запись.</param>
/// <param name="secrets">Хранилище секретов.</param>
/// <param name="protector">Шифрование <c>secrets.dat</c>: нужно, чтобы вернуть секреты из резервной копии.</param>
/// <param name="backups">Резервные копии.</param>
/// <param name="tables">Таблицы установленных раскладок для сравнения сокращений.</param>
/// <param name="newGuid">Источник случайных значений.</param>
/// <param name="log">Журнал.</param>
internal sealed class SettingsTransfer(
    AppPaths paths,
    SettingsService settings,
    SecretStore secrets,
    ISecretProtector protector,
    BackupStore backups,
    IInstalledTables tables,
    Func<Guid> newGuid,
    ILog log)
{
    /// <summary>Наименьшая длина пароля для секретов.</summary>
    public const int MinPasswordLength = 8;

    /// <summary>Сохраняет настройки в файл переноса.</summary>
    /// <param name="path">Куда записать.</param>
    /// <param name="password">Пароль для секретов; <c>null</c> — секреты в файл не кладутся.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Итог.</returns>
    public async Task<ExportResult> ExportAsync(string path, string? password, CancellationToken cancellationToken)
    {
        var current = settings.Current;
        var result = await Task.Run(() => Export(path, current, password), cancellationToken);
        if (result == ExportResult.Saved)
        {
            log.Info($"exported {current.Rules.Count} rules, {current.Snippets.Count} snippets, secrets={password is not null}");
        }

        return result;
    }

    /// <summary>Читает файл переноса или обычный <c>settings.json</c> и проверяет его целиком; настройки не меняются.</summary>
    /// <param name="path">Файл.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Файл готов к загрузке или вид ошибки.</returns>
    public async Task<OpenResult> OpenAsync(string path, CancellationToken cancellationToken)
    {
        var read = await Task.Run(() => ReadFile(path), cancellationToken);
        if (read.Package is null)
        {
            log.Error($"import file rejected: {read.Error} ({read.Detail})");
            return new OpenResult(null, read.Error);
        }

        var conflicts = ImportPlanner.FindConflicts(settings.Current, read.Package.Settings, tables.Tables(), secrets.Snapshot());
        return new OpenResult(new ImportPreview(Path.GetFileName(path), read.Package, conflicts), TransferError.None);
    }

    /// <summary>
    /// Пересчитывает конфликты, зная пароль от секретов файла: секретные сокращения с тем же текстом, что у вас, конфликтами не считаются.
    /// </summary>
    /// <param name="preview">Файл, открытый через <see cref="OpenAsync"/>.</param>
    /// <param name="password">Пароль от секретов файла.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Новые конфликты или <c>null</c>, если пароль не подошёл (тогда список остаётся прежним).</returns>
    public async Task<IReadOnlyList<ImportConflict>?> RecheckConflictsAsync(ImportPreview preview, string password, CancellationToken cancellationToken)
    {
        if (preview.Package.Secrets is not { } sealedSecrets)
        {
            return null;
        }

        var opened = await Task.Run(() => (Ok: TransferCrypto.TryDecrypt(sealedSecrets, password, out var plain), Plain: plain), cancellationToken);
        return opened.Ok
            ? ImportPlanner.FindConflicts(settings.Current, preview.Package.Settings, tables.Tables(), secrets.Snapshot(), opened.Plain)
            : null;
    }

    /// <summary>Загружает настройки из проверенного файла.</summary>
    /// <param name="preview">Файл, открытый через <see cref="OpenAsync"/>.</param>
    /// <param name="mode">Заменить всё или добавить к текущим.</param>
    /// <param name="takeFromFile">Конфликты, в которых выбрано «Взять из файла».</param>
    /// <param name="password">Пароль от секретов файла; <c>null</c> — загрузить без секретов.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Итог загрузки.</returns>
    public async Task<ImportResult> ImportAsync(
        ImportPreview preview, ImportMode mode, IReadOnlySet<ConflictKey> takeFromFile, string? password, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> fileSecrets = new Dictionary<string, string>();
        if (preview.Package.Secrets is { } sealedSecrets && password is not null)
        {
            var opened = await Task.Run(() => (Ok: TransferCrypto.TryDecrypt(sealedSecrets, password, out var plain), Plain: plain), cancellationToken);
            if (!opened.Ok)
            {
                log.Error("import: the password for secrets was rejected");
                return ImportResult.Fail(ImportFailure.WrongPassword);
            }

            fileSecrets = opened.Plain!;
        }

        if (backups.Create(preview.FileName) is null)
        {
            return ImportResult.Fail(ImportFailure.BackupFailed);
        }

        var before = settings.Current;
        var outcome = ImportPlanner.Apply(before, preview.Package.Settings, mode, takeFromFile, fileSecrets, tables.Tables(), newGuid, secrets.Snapshot());
        var plan = ScriptMerge.Plan(preview.Package.Scripts, ReadScript, mode);
        var failure = await ApplyAsync(outcome.Settings, outcome.NewSecrets, outcome.ObsoleteSecretKeys, plan.ToWrite, cancellationToken);
        if (failure != ImportFailure.None)
        {
            return ImportResult.Fail(failure);
        }

        // Только правила из файла: при «Добавить» прежние правила остаются теми же объектами, о них не говорим.
        var fromFile = mode == ImportMode.Replace
            ? outcome.Settings.Rules
            : [.. outcome.Settings.Rules.Where(rule => !before.Rules.Any(old => ReferenceEquals(old, rule)))];
        var missing = ScriptMerge.FindMissingPrograms(fromFile, LaunchEnvironment.Current(paths.ScriptsDir));
        log.Info(
            $"imported: mode={mode}, {outcome.Report.Rules} rules, {outcome.Report.Snippets} snippets, {outcome.Report.Exclusions} exclusions, "
            + $"{plan.ToWrite.Count} script files, {plan.Skipped.Count} skipped");
        return new ImportResult(ImportFailure.None, outcome.Report, plan.Skipped, missing);
    }

    /// <summary>Возвращает настройки, секреты и скрипты из резервной копии. Текущее состояние перед этим тоже сохраняется копией.</summary>
    /// <param name="backup">Копия.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Итог восстановления.</returns>
    public async Task<ImportResult> RestoreAsync(BackupInfo backup, CancellationToken cancellationToken)
    {
        var content = await Task.Run(() => backups.Read(backup), cancellationToken);
        var parsed = content is null ? null : SettingsJson.Parse(content.SettingsJson).Settings;
        if (content is null || parsed is null || !TryUnprotect(content.SecretsDat, out var restoredSecrets))
        {
            return ImportResult.Fail(ImportFailure.BadBackup);
        }

        if (backups.Create(string.Empty) is null)
        {
            return ImportResult.Fail(ImportFailure.BackupFailed);
        }

        var failure = await ApplyAsync(parsed, restoredSecrets, null, content.Scripts, cancellationToken);
        if (failure != ImportFailure.None)
        {
            return ImportResult.Fail(failure);
        }

        log.Info("restored from a backup");
        var missing = ScriptMerge.FindMissingPrograms(parsed.Rules, LaunchEnvironment.Current(paths.ScriptsDir));
        return new ImportResult(ImportFailure.None, null, [], missing);
    }

    /// <summary>
    /// Записывает секреты, скрипты и настройки. Если <paramref name="obsoleteKeys"/> равно <c>null</c>, хранилище секретов заменяется
    /// целиком (восстановление), иначе новые секреты добавляются, а лишние ключи убираются после успешной записи настроек.
    /// </summary>
    private async Task<ImportFailure> ApplyAsync(
        AppSettings next,
        IReadOnlyDictionary<string, string> newSecrets,
        IReadOnlyList<string>? obsoleteKeys,
        IReadOnlyList<ScriptFile> scripts,
        CancellationToken cancellationToken)
    {
        var before = secrets.Snapshot();
        var wrote = obsoleteKeys is null ? secrets.ReplaceAll(newSecrets) : secrets.SetMany(newSecrets);
        if (!wrote)
        {
            return ImportFailure.SecretsFailed;
        }

        if (!await Task.Run(() => WriteScripts(scripts), cancellationToken))
        {
            _ = secrets.ReplaceAll(before);
            return ImportFailure.ScriptsFailed;
        }

        if (!settings.Update(_ => next))
        {
            if (!secrets.ReplaceAll(before))
            {
                log.Error("could not roll back secrets after a failed import");
            }

            return ImportFailure.SettingsFailed;
        }

        if (obsoleteKeys is { Count: > 0 } && !secrets.Remove(obsoleteKeys))
        {
            log.Error("could not remove unused secrets after an import");
        }

        return ImportFailure.None;
    }

    /// <summary>Собирает и пишет файл переноса.</summary>
    private ExportResult Export(string path, AppSettings current, string? password)
    {
        var scripts = ReadAllScripts();
        if (scripts is null)
        {
            return ExportResult.TooLarge;
        }

        SealedSecrets? sealedSecrets = null;
        if (password is not null)
        {
            var plain = new Dictionary<string, string>();
            foreach (var key in current.Snippets.Where(s => s.Secret).Select(s => s.SecretRef).OfType<string>())
            {
                if (secrets.TryGet(key, out var text))
                {
                    plain[key] = text;
                }
            }

            sealedSecrets = TransferCrypto.Encrypt(plain, password);
        }

        var tmp = path + ".tmp";
        try
        {
            using (var file = File.Create(tmp))
            {
                TransferArchive.Write(file, current, scripts, sealedSecrets);
            }

            File.Move(tmp, path, overwrite: true);
            return ExportResult.Saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not write the export file", ex);
            try
            {
                File.Delete(tmp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                log.Error("could not delete the temporary export file", cleanup);
            }

            return ExportResult.Failed;
        }
    }

    /// <summary>Читает файл с диска и разбирает по расширению: <c>.json</c> — обычные настройки, остальное — архив.</summary>
    private static TransferReadResult ReadFile(string path)
    {
        try
        {
            if (new FileInfo(path).Length > TransferArchive.MaxTotalBytes)
            {
                return TransferReadResult.Fail(TransferError.TooLarge, "file is too large");
            }

            if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            {
                return TransferArchive.ReadSettings(File.ReadAllText(path));
            }

            using var stream = File.OpenRead(path);
            return TransferArchive.Read(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return TransferReadResult.Fail(TransferError.Unreadable, ex.GetType().Name);
        }
    }

    /// <summary>Читает все файлы папки скриптов; <c>null</c>, если их больше, чем помещается в файл переноса.</summary>
    private List<ScriptFile>? ReadAllScripts()
    {
        var result = new List<ScriptFile>();
        if (!Directory.Exists(paths.ScriptsDir))
        {
            return result;
        }

        long total = 0;
        foreach (var file in Directory.EnumerateFiles(paths.ScriptsDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(paths.ScriptsDir, file).Replace('\\', '/');
            if (!TransferArchive.IsAllowedName(TransferArchive.ScriptsPrefix + relative))
            {
                log.Error("a script file with an unsupported name is left out of the export");
                continue;
            }

            total += new FileInfo(file).Length;
            if (total > TransferArchive.MaxTotalBytes / 2 || result.Count >= TransferArchive.MaxEntries - 2)
            {
                return null;
            }

            result.Add(new ScriptFile(relative, File.ReadAllBytes(file)));
        }

        return result;
    }

    /// <summary>Читает файл из папки скриптов.</summary>
    /// <param name="relativePath">Путь относительно папки скриптов.</param>
    /// <returns>Содержимое или <c>null</c>, если файла нет.</returns>
    private byte[]? ReadScript(string relativePath)
    {
        var full = ScriptPath(relativePath);
        return full is not null && File.Exists(full) ? File.ReadAllBytes(full) : null;
    }

    /// <summary>Записывает файлы в папку скриптов; путь вне папки не записывается никогда.</summary>
    private bool WriteScripts(IReadOnlyList<ScriptFile> files)
    {
        try
        {
            foreach (var file in files)
            {
                if (ScriptPath(file.RelativePath) is not { } full)
                {
                    log.Error("a script file path is outside the scripts folder and is ignored");
                    return false;
                }

                _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                AtomicFile.Write(full, file.Content, ".keymend-tmp");
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not write script files", ex);
            return false;
        }
    }

    /// <summary>Полный путь файла внутри папки скриптов.</summary>
    /// <param name="relativePath">Путь относительно папки скриптов.</param>
    /// <returns>Путь или <c>null</c>, если он выходит из папки.</returns>
    private string? ScriptPath(string relativePath)
    {
        var root = Path.GetFullPath(paths.ScriptsDir) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(paths.ScriptsDir, relativePath));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>Расшифровывает <c>secrets.dat</c> из резервной копии.</summary>
    /// <param name="data">Содержимое файла или <c>null</c>, если в копии секретов не было.</param>
    /// <param name="restored">Секреты.</param>
    /// <returns><c>true</c>, если файл разобран.</returns>
    private bool TryUnprotect(byte[]? data, out IReadOnlyDictionary<string, string> restored)
    {
        restored = new Dictionary<string, string>();
        if (data is null)
        {
            return true;
        }

        try
        {
            restored = JsonSerializer.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(protector.Unprotect(data))) ?? [];
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            log.Error("secrets in the backup cannot be read", ex);
            return false;
        }
    }
}
