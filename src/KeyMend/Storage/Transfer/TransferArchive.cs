using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace KeyMend.Storage.Transfer;

/// <summary>Файл из папки скриптов, лежащий в архиве переноса.</summary>
/// <param name="RelativePath">Путь относительно <c>scripts\</c>, с прямыми слэшами.</param>
/// <param name="Content">Содержимое.</param>
internal sealed record ScriptFile(string RelativePath, byte[] Content);

/// <summary>Что не так с файлом переноса. Сообщение пользователю выбирается по этому значению.</summary>
internal enum TransferError
{
    /// <summary>Всё в порядке.</summary>
    None,

    /// <summary>Файл не читается или не является архивом.</summary>
    Unreadable,

    /// <summary>В архиве нет <c>settings.json</c>.</summary>
    MissingSettings,

    /// <summary>Настройки в файле не разобрались.</summary>
    BadSettings,

    /// <summary>Настройки записаны более новой версией KeyMend.</summary>
    NewerVersion,

    /// <summary>Секреты в файле повреждены.</summary>
    BadSecrets,

    /// <summary>В архиве есть записи, которых там быть не должно, или небезопасные пути.</summary>
    UnexpectedEntry,

    /// <summary>Архив больше допустимого.</summary>
    TooLarge,
}

/// <summary>Содержимое файла переноса, проверенное целиком.</summary>
/// <param name="Settings">Настройки; тексты секретов в них отсутствуют.</param>
/// <param name="Scripts">Файлы папки скриптов.</param>
/// <param name="Secrets">Зашифрованные секреты или <c>null</c>, если их в файле нет.</param>
/// <param name="Warnings">Замечания разбора настроек (пропущенные правила); содержимого ввода в них нет.</param>
internal sealed record TransferPackage(
    AppSettings Settings, IReadOnlyList<ScriptFile> Scripts, SealedSecrets? Secrets, IReadOnlyList<string> Warnings);

/// <summary>Итог чтения файла переноса.</summary>
/// <param name="Package">Содержимое или <c>null</c> при ошибке.</param>
/// <param name="Error">Вид ошибки.</param>
/// <param name="Detail">Подробности для журнала на английском, без содержимого; пусто, если ошибки нет.</param>
internal sealed record TransferReadResult(TransferPackage? Package, TransferError Error, string Detail)
{
    /// <summary>Создаёт результат с ошибкой.</summary>
    /// <param name="error">Вид ошибки.</param>
    /// <param name="detail">Подробности для журнала.</param>
    /// <returns>Результат без содержимого.</returns>
    public static TransferReadResult Fail(TransferError error, string detail) => new(null, error, detail);
}

/// <summary>
/// Файл <c>.keymend</c> (SPEC F8): zip с <c>settings.json</c>, папкой <c>scripts/</c> и необязательным <c>secrets.json</c>. Чтение
/// проверяет архив целиком в памяти и ничего не пишет на диск: пути только внутри <c>scripts/</c>, число записей и размер
/// ограничены (защита от архива-бомбы).
/// </summary>
internal static class TransferArchive
{
    /// <summary>Имя файла настроек в архиве.</summary>
    public const string SettingsEntry = "settings.json";

    /// <summary>Имя файла секретов в архиве.</summary>
    public const string SecretsEntry = "secrets.json";

    /// <summary>Папка скриптов в архиве.</summary>
    public const string ScriptsPrefix = "scripts/";

    /// <summary>Наибольшее число записей в архиве.</summary>
    public const int MaxEntries = 1000;

    /// <summary>Наибольший суммарный размер распакованных данных, байт.</summary>
    public const long MaxTotalBytes = 64L * 1024 * 1024;

    /// <summary>Параметры JSON секретов.</summary>
    private static readonly JsonSerializerOptions _secretsOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>Записывает архив.</summary>
    /// <param name="output">Куда писать.</param>
    /// <param name="settings">Настройки; тексты секретов в них не должны быть.</param>
    /// <param name="scripts">Файлы папки скриптов.</param>
    /// <param name="secrets">Зашифрованные секреты или <c>null</c>.</param>
    public static void Write(Stream output, AppSettings settings, IReadOnlyList<ScriptFile> scripts, SealedSecrets? secrets)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        AddEntry(zip, SettingsEntry, Encoding.UTF8.GetBytes(SettingsJson.Serialize(settings)));
        if (secrets is not null)
        {
            AddEntry(zip, SecretsEntry, JsonSerializer.SerializeToUtf8Bytes(secrets, _secretsOptions));
        }

        foreach (var script in scripts)
        {
            AddEntry(zip, ScriptsPrefix + script.RelativePath, script.Content);
        }
    }

    /// <summary>Читает и проверяет архив.</summary>
    /// <param name="input">Откуда читать.</param>
    /// <returns>Содержимое или ошибка; настройки при этом не применяются.</returns>
    public static TransferReadResult Read(Stream input)
    {
        try
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            return ReadEntries(zip);
        }
        catch (InvalidDataException ex)
        {
            return TransferReadResult.Fail(TransferError.Unreadable, ex.Message);
        }
    }

    /// <summary>Разбирает обычный <c>settings.json</c> (например, стартовый набор) как файл переноса без скриптов и секретов.</summary>
    /// <param name="json">Содержимое файла.</param>
    /// <returns>Содержимое или ошибка.</returns>
    public static TransferReadResult ReadSettings(string json)
    {
        if (SchemaMigrator.ReadVersion(json) is { } version && version > AppSettings.CurrentSchemaVersion)
        {
            return TransferReadResult.Fail(TransferError.NewerVersion, $"schemaVersion {version}");
        }

        var parsed = SettingsJson.Parse(json);
        return parsed.Settings is { } settings
            ? new TransferReadResult(new TransferPackage(settings, [], null, parsed.Warnings), TransferError.None, string.Empty)
            : TransferReadResult.Fail(TransferError.BadSettings, parsed.Error ?? "invalid settings");
    }

    /// <summary>Читает записи уже открытого архива.</summary>
    /// <param name="zip">Архив.</param>
    /// <returns>Содержимое или ошибка.</returns>
    private static TransferReadResult ReadEntries(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxEntries)
        {
            return TransferReadResult.Fail(TransferError.TooLarge, $"{zip.Entries.Count} entries");
        }

        byte[]? settingsBytes = null;
        byte[]? secretsBytes = null;
        var scripts = new List<ScriptFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/') && entry.Length == 0)
            {
                continue;
            }

            if (!IsAllowedName(entry.FullName) || !names.Add(entry.FullName))
            {
                return TransferReadResult.Fail(TransferError.UnexpectedEntry, "unexpected entry in the archive");
            }

            var content = ReadLimited(entry, MaxTotalBytes - total);
            if (content is null)
            {
                return TransferReadResult.Fail(TransferError.TooLarge, "unpacked data is too large");
            }

            total += content.Length;
            switch (entry.FullName)
            {
                case SettingsEntry:
                    settingsBytes = content;
                    break;
                case SecretsEntry:
                    secretsBytes = content;
                    break;
                default:
                    scripts.Add(new ScriptFile(entry.FullName[ScriptsPrefix.Length..], content));
                    break;
            }
        }

        if (settingsBytes is null)
        {
            return TransferReadResult.Fail(TransferError.MissingSettings, "settings.json is absent");
        }

        var settings = ReadSettings(Encoding.UTF8.GetString(settingsBytes));
        if (settings.Package is null)
        {
            return settings;
        }

        SealedSecrets? secrets = null;
        if (secretsBytes is not null && !TryParseSecrets(secretsBytes, out secrets))
        {
            return TransferReadResult.Fail(TransferError.BadSecrets, "secrets.json is invalid");
        }

        return new TransferReadResult(settings.Package with { Scripts = scripts, Secrets = secrets }, TransferError.None, string.Empty);
    }

    /// <summary>Разбирает <c>secrets.json</c>.</summary>
    /// <param name="bytes">Содержимое.</param>
    /// <param name="secrets">Секреты.</param>
    /// <returns><c>true</c>, если все поля на месте.</returns>
    private static bool TryParseSecrets(byte[] bytes, out SealedSecrets? secrets)
    {
        try
        {
            secrets = JsonSerializer.Deserialize<SealedSecrets>(bytes, _secretsOptions);
        }
        catch (JsonException)
        {
            secrets = null;
            return false;
        }

        return secrets is { Kdf: not null, Salt: not null, Nonce: not null, Tag: not null, Data: not null };
    }

    /// <summary>Допустимо ли имя записи: известный файл или файл внутри <c>scripts/</c> без выхода из неё.</summary>
    /// <param name="name">Имя записи.</param>
    /// <returns><c>true</c>, если имя безопасно и ожидаемо.</returns>
    internal static bool IsAllowedName(string name)
    {
        if (name is SettingsEntry or SecretsEntry)
        {
            return true;
        }

        if (!name.StartsWith(ScriptsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var segments = name[ScriptsPrefix.Length..].Split('/');
        return segments.All(segment =>
            segment.Length > 0
            && segment is not ("." or "..")
            && segment.IndexOfAny(invalid) < 0
            && !segment.EndsWith('.')
            && !segment.EndsWith(' '));
    }

    /// <summary>Читает запись, считая настоящие байты, а не заявленный размер.</summary>
    /// <param name="entry">Запись.</param>
    /// <param name="budget">Сколько байт ещё можно прочитать.</param>
    /// <returns>Содержимое или <c>null</c>, если оно не умещается в бюджет.</returns>
    private static byte[]? ReadLimited(ZipArchiveEntry entry, long budget)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > budget)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Добавляет запись в архив.</summary>
    /// <param name="zip">Архив.</param>
    /// <param name="name">Имя записи.</param>
    /// <param name="content">Содержимое.</param>
    private static void AddEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}
