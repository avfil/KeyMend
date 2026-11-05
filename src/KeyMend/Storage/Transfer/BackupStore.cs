using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using KeyMend.Infrastructure;

namespace KeyMend.Storage.Transfer;

/// <summary>Резервная копия настроек в папке <c>backup\</c>.</summary>
/// <param name="Path">Полный путь к файлу копии.</param>
/// <param name="Created">Когда копия создана (местное время).</param>
/// <param name="SourceName">Имя файла, который собирались загрузить; пусто, если неизвестно.</param>
internal sealed record BackupInfo(string Path, DateTime Created, string SourceName);

/// <summary>Содержимое резервной копии.</summary>
/// <param name="SettingsJson">Текст <c>settings.json</c>.</param>
/// <param name="SecretsDat">Файл <c>secrets.dat</c> как был (защищён DPAPI этого пользователя) или <c>null</c>.</param>
/// <param name="Scripts">Файлы папки скриптов.</param>
internal sealed record BackupContent(string SettingsJson, byte[]? SecretsDat, IReadOnlyList<ScriptFile> Scripts);

/// <summary>
/// Резервные копии перед загрузкой настроек (SPEC 2.2, F8): zip в <c>%AppData%\KeyMend\backup\</c> с <c>settings.json</c>,
/// <c>secrets.dat</c> и папкой <c>scripts\</c>. Хранятся последние <see cref="MaxCopies"/>. В журнал попадают только числа и имена файлов копий.
/// </summary>
/// <param name="paths">Пути данных программы.</param>
/// <param name="time">Источник времени для имени копии.</param>
/// <param name="log">Журнал.</param>
internal sealed class BackupStore(AppPaths paths, TimeProvider time, ILog log)
{
    /// <summary>Сколько копий хранится.</summary>
    public const int MaxCopies = 10;

    /// <summary>Формат времени в имени файла: до миллисекунд, чтобы две копии подряд не столкнулись.</summary>
    private const string _nameFormat = "yyyyMMdd-HHmmssfff";

    /// <summary>Имя записи с описанием копии.</summary>
    private const string _metaEntry = "backup.json";

    /// <summary>Имя записи с секретами.</summary>
    private const string _secretsEntry = "secrets.dat";

    /// <summary>Создаёт копию текущих настроек, секретов и скриптов и удаляет самые старые сверх лимита.</summary>
    /// <param name="sourceName">Имя файла, который собираются загрузить (или пусто для копии перед восстановлением).</param>
    /// <returns>Описание копии или <c>null</c>, если записать не удалось (причина в журнале).</returns>
    public BackupInfo? Create(string sourceName)
    {
        var now = time.GetLocalNow().DateTime;
        var path = Path.Combine(paths.BackupDir, now.ToString(_nameFormat, CultureInfo.InvariantCulture) + ".zip");
        var tmp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(paths.BackupDir);
            using (var file = File.Create(tmp))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                AddFile(zip, TransferArchive.SettingsEntry, paths.SettingsFile);
                AddFile(zip, _secretsEntry, paths.SecretsFile);
                AddScripts(zip);
                using var meta = zip.CreateEntry(_metaEntry).Open();
                JsonSerializer.Serialize(meta, new Dictionary<string, string> { ["source"] = sourceName });
            }

            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not create a backup", ex);
            TryDelete(tmp);
            return null;
        }

        Prune();
        log.Info("backup created: " + Path.GetFileName(path));
        return new BackupInfo(path, now, sourceName);
    }

    /// <summary>Перечисляет копии, самые новые первыми.</summary>
    /// <returns>Копии, чьё имя разобралось.</returns>
    public IReadOnlyList<BackupInfo> List()
    {
        if (!Directory.Exists(paths.BackupDir))
        {
            return [];
        }

        var result = new List<BackupInfo>();
        foreach (var path in Directory.EnumerateFiles(paths.BackupDir, "*.zip").OrderByDescending(p => p, StringComparer.Ordinal))
        {
            if (DateTime.TryParseExact(
                    Path.GetFileNameWithoutExtension(path), _nameFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var created))
            {
                result.Add(new BackupInfo(path, created, ReadSourceName(path)));
            }
        }

        return result;
    }

    /// <summary>Читает копию целиком, проверяя имена записей так же строго, как при загрузке файла.</summary>
    /// <param name="backup">Копия.</param>
    /// <returns>Содержимое или <c>null</c>, если копия не читается или повреждена (причина в журнале).</returns>
    public BackupContent? Read(BackupInfo backup)
    {
        try
        {
            using var zip = ZipFile.OpenRead(backup.Path);
            string? settings = null;
            byte[]? secrets = null;
            var scripts = new List<ScriptFile>();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName == _metaEntry)
                {
                    continue;
                }

                if (entry.FullName == TransferArchive.SettingsEntry)
                {
                    settings = System.Text.Encoding.UTF8.GetString(ReadAll(entry));
                }
                else if (entry.FullName == _secretsEntry)
                {
                    secrets = ReadAll(entry);
                }
                else if (entry.FullName.StartsWith(TransferArchive.ScriptsPrefix, StringComparison.Ordinal) && TransferArchive.IsAllowedName(entry.FullName))
                {
                    scripts.Add(new ScriptFile(entry.FullName[TransferArchive.ScriptsPrefix.Length..], ReadAll(entry)));
                }
                else
                {
                    log.Error("backup contains an unexpected entry and is ignored");
                    return null;
                }
            }

            return settings is null ? null : new BackupContent(settings, secrets, scripts);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log.Error("could not read a backup", ex);
            return null;
        }
    }

    /// <summary>Добавляет в архив файл, если он есть.</summary>
    /// <param name="zip">Архив.</param>
    /// <param name="name">Имя записи.</param>
    /// <param name="source">Путь к файлу.</param>
    private static void AddFile(ZipArchive zip, string name, string source)
    {
        if (File.Exists(source))
        {
            _ = zip.CreateEntryFromFile(source, name, CompressionLevel.Optimal);
        }
    }

    /// <summary>Добавляет в архив все файлы папки скриптов.</summary>
    /// <param name="zip">Архив.</param>
    private void AddScripts(ZipArchive zip)
    {
        if (!Directory.Exists(paths.ScriptsDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(paths.ScriptsDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(paths.ScriptsDir, file).Replace('\\', '/');
            _ = zip.CreateEntryFromFile(file, TransferArchive.ScriptsPrefix + relative, CompressionLevel.Optimal);
        }
    }

    /// <summary>Удаляет копии сверх лимита, самые старые первыми.</summary>
    private void Prune()
    {
        foreach (var old in List().Skip(MaxCopies))
        {
            TryDelete(old.Path);
        }
    }

    /// <summary>Читает имя файла, ради которого сделана копия.</summary>
    /// <param name="path">Файл копии.</param>
    /// <returns>Имя или пустая строка.</returns>
    private static string ReadSourceName(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.GetEntry(_metaEntry) is not { } entry)
            {
                return string.Empty;
            }

            using var stream = entry.Open();
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.TryGetProperty("source", out var source) ? source.GetString() ?? string.Empty : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>Читает запись архива целиком.</summary>
    /// <param name="entry">Запись.</param>
    /// <returns>Содержимое.</returns>
    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Удаляет файл; неудача не мешает работе и пишется в журнал.</summary>
    /// <param name="path">Файл.</param>
    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not delete a backup file", ex);
        }
    }
}
