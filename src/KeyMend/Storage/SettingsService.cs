using System.IO;
using KeyMend.Infrastructure;

namespace KeyMend.Storage;

/// <summary>Запись настроек в файл; выделена, чтобы <see cref="SettingsService"/> проверялся без диска.</summary>
internal interface ISettingsWriter
{
    /// <summary>Пишет настройки атомарно (временный файл, затем замена), чтобы сбой не оставил наполовину записанный файл.</summary>
    /// <param name="settings">Настройки.</param>
    void Save(AppSettings settings);
}

/// <summary>
/// Текущие настройки приложения и единственный путь их изменения из окна настроек (SPEC F11: изменения применяются сразу).
/// Правка из окна пишется в файл и сообщается подписчикам; перечитанный извне файл только сообщается. Все вызовы — из потока UI.
/// </summary>
/// <param name="initial">Настройки при старте.</param>
/// <param name="writer">Запись в файл.</param>
/// <param name="log">Журнал.</param>
internal sealed class SettingsService(AppSettings initial, ISettingsWriter writer, ILog log)
{
    /// <summary>Настройки, действующие сейчас.</summary>
    public AppSettings Current { get; private set; } = initial;

    /// <summary>Настройки изменились: правкой из окна или перечитыванием файла.</summary>
    public event Action<AppSettings>? Changed;

    /// <summary>Меняет настройки и записывает файл. Если записать не вышло, настройки остаются прежними.</summary>
    /// <param name="change">Получает текущие настройки, возвращает новые.</param>
    /// <returns><c>true</c>, если изменение записано и применено.</returns>
    public bool Update(Func<AppSettings, AppSettings> change)
    {
        var next = change(Current);
        try
        {
            writer.Save(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("could not save settings", ex);
            return false;
        }

        Replace(next);
        return true;
    }

    /// <summary>Принимает настройки, уже записанные в файл или перечитанные из него.</summary>
    /// <param name="settings">Новые настройки.</param>
    public void Replace(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(settings);
    }
}
