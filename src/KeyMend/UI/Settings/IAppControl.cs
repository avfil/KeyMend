using System.Windows;

namespace KeyMend.UI.Settings;

/// <summary>
/// Что окну настроек нужно от самой программы и чего нет в настройках: пауза, автозапуск, пример подсказки раскладки,
/// открытие файлов и папок. Реализует <c>App</c> (composition root), тесты подставляют заглушку.
/// </summary>
internal interface IAppControl
{
    /// <summary>KeyMend включён (не на паузе).</summary>
    bool IsEnabled { get; }

    /// <summary>Обычный автозапуск включён.</summary>
    bool IsAutostartOn { get; }

    /// <summary>Автозапуск от администратора включён.</summary>
    bool IsAdminAutostartOn { get; }

    /// <summary>Включает или выключает KeyMend (общая пауза); значок, меню и окно обновляются.</summary>
    /// <param name="enabled"><c>true</c> — работать, <c>false</c> — пауза.</param>
    void SetEnabled(bool enabled);

    /// <summary>Включает или выключает обычный автозапуск; при ошибке показывает уведомление.</summary>
    /// <param name="on"><c>true</c> — включить.</param>
    /// <returns><c>true</c>, если автозапуск теперь в нужном состоянии.</returns>
    bool TrySetAutostart(bool on);

    /// <summary>Включает или выключает автозапуск от администратора: пояснения, UAC и итог показывают собственные окна.</summary>
    /// <returns>Задача: операция закончена или отменена.</returns>
    Task ToggleAdminAutostartAsync();

    /// <summary>Показывает подсказку раскладки с текущим языком под заданным прямоугольником, как при настоящей смене языка.</summary>
    /// <param name="anchor">Прямоугольник кнопки «Показать» в физических пикселях экрана.</param>
    void ShowIndicatorSample(Rect anchor);

    /// <summary>Открывает <c>settings.json</c> в редакторе; при ошибке показывает уведомление.</summary>
    void OpenSettingsFile();

    /// <summary>Кладёт текст в буфер обмена; при ошибке показывает уведомление.</summary>
    /// <param name="text">Текст.</param>
    void CopyText(string text);

    /// <summary>Открывает папку в Проводнике; при ошибке показывает уведомление.</summary>
    /// <param name="path">Полный путь к папке.</param>
    void OpenFolder(string path);
}
