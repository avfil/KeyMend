using System.IO;
using System.Text;
using KeyMend.Infrastructure;

namespace KeyMend.Storage;

/// <summary>Итог загрузки настроек при старте.</summary>
/// <param name="Settings">Настройки; при отсутствии или порче файла — значения по умолчанию.</param>
/// <param name="IsFirstRun">Файла не было: это первый запуск (SPEC F11).</param>
internal sealed record SettingsLoadResult(AppSettings Settings, bool IsFirstRun);

/// <summary>
/// Читает и пишет <c>settings.json</c> (SPEC 7.3). Запись атомарная; повреждённый файл при старте сохраняется как <c>.bad</c>.
/// В лог попадают только имена правил и описания ошибок, не содержимое ввода.
/// </summary>
/// <param name="path">Полный путь к файлу настроек.</param>
/// <param name="log">Журнал.</param>
internal sealed class SettingsStore(string path, ILog log) : ISettingsWriter
{
    /// <summary>Текст ошибки, когда файл не удалось прочитать (занят редактором и т. п.); по нему перечитка понимает, что можно повторить.</summary>
    internal const string ReadFailedMessage = "file cannot be read";

    /// <summary>Загружает настройки при старте: нет файла — значения по умолчанию; битый файл — копия <c>.bad</c> и значения по умолчанию.</summary>
    /// <returns>Настройки и признак первого запуска.</returns>
    public SettingsLoadResult Load()
    {
        if (!File.Exists(path))
        {
            return new SettingsLoadResult(DefaultSettings.Create(), IsFirstRun: true);
        }

        var result = TryRead();
        if (result.Settings is { } settings)
        {
            return new SettingsLoadResult(settings, IsFirstRun: false);
        }

        log.Error($"settings.json is invalid, defaults used: {result.Error}");
        try
        {
            File.Copy(path, path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not save settings.json.bad", ex);
        }

        return new SettingsLoadResult(DefaultSettings.Create(), IsFirstRun: false);
    }

    /// <summary>
    /// Читает и проверяет файл без подстановки значений по умолчанию (для перечитки при правке: при ошибке прежние
    /// настройки остаются). Пропущенные правила пишутся в лог.
    /// </summary>
    /// <returns>Настройки либо описание ошибки.</returns>
    public SettingsParseResult TryRead()
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not read settings.json", ex);
            return new SettingsParseResult(null, ReadFailedMessage, []);
        }

        var result = SettingsJson.Parse(json);
        foreach (var warning in result.Warnings)
        {
            log.Error(warning);
        }

        return result;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.Write(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SettingsJson.Serialize(settings)));
    }
}
