namespace KeyMend.Actions;

/// <summary>Действия приложения, которые вызывают служебные шаги правил. Реализация сама переносит вызов в поток UI.</summary>
internal interface IAppCommands
{
    /// <summary>Включает или выключает паузу KeyMend.</summary>
    void TogglePause();

    /// <summary>Открывает настройки.</summary>
    void OpenSettings();
}
