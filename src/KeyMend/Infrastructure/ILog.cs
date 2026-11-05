namespace KeyMend.Infrastructure;

/// <summary>
/// Журнал событий. В нём только события без содержимого ввода («converted 5 chars»): ни набранного текста,
/// ни клавиш, ни текста сокращений и секретов (SPEC 2.3). Реализации не бросают исключений.
/// </summary>
internal interface ILog
{
    /// <summary>Пишет информационное событие.</summary>
    /// <param name="message">Сообщение на английском, без содержимого ввода.</param>
    void Info(string message);

    /// <summary>Пишет ошибку; если передано исключение, оно добавляется к сообщению.</summary>
    /// <param name="message">Сообщение на английском, без содержимого ввода.</param>
    /// <param name="exception">Причина ошибки или <c>null</c>.</param>
    void Error(string message, Exception? exception = null);
}
