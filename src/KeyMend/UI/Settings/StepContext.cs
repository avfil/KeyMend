namespace KeyMend.UI.Settings;

/// <summary>Выбор файла и папки в системном диалоге для кнопок «Обзор…».</summary>
internal interface IFilePicker
{
    /// <summary>Просит выбрать файл программы или скрипта.</summary>
    /// <param name="initialDirectory">Папка, с которой открыть диалог.</param>
    /// <returns>Полный путь или <c>null</c>, если выбор отменён.</returns>
    string? PickFile(string initialDirectory);

    /// <summary>Просит выбрать папку.</summary>
    /// <param name="initialDirectory">Папка, с которой открыть диалог.</param>
    /// <returns>Полный путь или <c>null</c>, если выбор отменён.</returns>
    string? PickFolder(string initialDirectory);

    /// <summary>Просит выбрать, куда сохранить файл переноса.</summary>
    /// <param name="suggestedName">Предлагаемое имя файла.</param>
    /// <returns>Полный путь или <c>null</c>, если выбор отменён.</returns>
    string? PickSaveFile(string suggestedName);

    /// <summary>Просит выбрать файл переноса или обычный <c>settings.json</c>.</summary>
    /// <returns>Полный путь или <c>null</c>, если выбор отменён.</returns>
    string? PickTransferFile();
}

/// <summary>Всё, что редакторам шагов нужно от окружения: языки, папка скриптов, выбор файлов, состояние гибернации.</summary>
/// <param name="InstalledLanguages">Теги установленных языков.</param>
/// <param name="ScriptsDir">Папка скриптов.</param>
/// <param name="Picker">Диалоги выбора файла и папки.</param>
/// <param name="HibernateEnabled">Включена ли гибернация в Windows.</param>
/// <param name="OpenHint">Открывает раздел «Подсказка раскладки».</param>
internal sealed record StepContext(
    IReadOnlyList<string> InstalledLanguages, string ScriptsDir, IFilePicker Picker, bool HibernateEnabled, Action OpenHint);
