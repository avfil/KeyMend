namespace KeyMend.UI;

/// <summary>Важность уведомления из трея.</summary>
internal enum NotifyLevel
{
    /// <summary>Информация (таймеры).</summary>
    Info,

    /// <summary>Предупреждение.</summary>
    Warning,

    /// <summary>Ошибка.</summary>
    Error,
}

/// <summary>Показывает пользователю уведомления об ошибках и таймерах (SPEC F11); обычные действия уведомлений не вызывают.</summary>
internal interface INotifier
{
    /// <summary>Показывает уведомление; можно вызывать из любого потока.</summary>
    /// <param name="title">Заголовок.</param>
    /// <param name="message">Текст без содержимого ввода и секретов.</param>
    /// <param name="level">Важность.</param>
    void Notify(string title, string message, NotifyLevel level = NotifyLevel.Info);
}
