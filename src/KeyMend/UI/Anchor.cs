namespace KeyMend.UI;

/// <summary>Рабочая область монитора в физических пикселях (без панели задач).</summary>
/// <param name="Left">Левая граница.</param>
/// <param name="Top">Верхняя граница.</param>
/// <param name="Right">Правая граница.</param>
/// <param name="Bottom">Нижняя граница.</param>
internal readonly record struct WorkArea(int Left, int Top, int Right, int Bottom);

/// <summary>Откуда взято положение подсказки.</summary>
internal enum AnchorSource
{
    /// <summary>Системная каретка (<c>GetGUIThreadInfo</c>): Блокнот, Word, консоль.</summary>
    Caret,

    /// <summary>Каретка по MSAA (<c>OBJID_CARET</c>): Chrome, Edge, Electron.</summary>
    Msaa,

    /// <summary>Каретка по UI Automation (<c>TextPattern</c>): Windows Terminal и другие программы, рисующие курсор сами.</summary>
    Uia,

    /// <summary>Курсор мыши: режим «мышь» или каретка не найдена.</summary>
    Mouse,
}

/// <summary>Точка привязки подсказки в физических экранных пикселях.</summary>
/// <param name="X">Координата X.</param>
/// <param name="Top">Верхняя граница каретки (для мыши совпадает с <paramref name="Bottom"/>).</param>
/// <param name="Bottom">Нижняя граница каретки.</param>
/// <param name="Source">Откуда взята точка.</param>
/// <param name="Obscured">
/// Область окна оболочки Windows («Пуск», поиск), которое лежит выше обычных окон «поверх всех» и перекрывает подсказку:
/// подсказку надо ставить снаружи от неё. <c>null</c> — обычное окно.
/// </param>
internal readonly record struct Anchor(int X, int Top, int Bottom, AnchorSource Source, WorkArea? Obscured = null);
