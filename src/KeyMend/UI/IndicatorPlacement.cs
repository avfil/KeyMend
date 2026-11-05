namespace KeyMend.UI;

/// <summary>
/// Выбор места для подсказки EN/RU (F1.4): по умолчанию под кареткой, но если снизу не хватает места,
/// а каретка видна, — над ней, чтобы не закрывать вводимый текст. Если каретка скрыта за панелью задач,
/// подсказка просто прижимается к рабочей области. Чистая функция без WinAPI.
/// </summary>
internal static class IndicatorPlacement
{
    /// <summary>Зазор между кареткой и подсказкой, физические пиксели.</summary>
    internal const int CaretGap = 4;

    /// <summary>Смещение от курсора мыши вправо и вниз, чтобы подсказка не закрывала сам курсор.</summary>
    internal const int MouseOffset = 18;

    /// <summary>Отступ подсказки от края рабочей области.</summary>
    internal const int ScreenMargin = 4;

    /// <summary>Вычисляет левый верхний угол подсказки.</summary>
    /// <param name="anchor">Каретка или курсор мыши.</param>
    /// <param name="width">Ширина подсказки в пикселях.</param>
    /// <param name="height">Высота подсказки в пикселях.</param>
    /// <param name="area">Рабочая область монитора, на котором находится точка привязки.</param>
    public static (int X, int Y) Calculate(Anchor anchor, int width, int height, WorkArea area)
    {
        if (anchor.Obscured is { } covered)
        {
            return PlaceOutside(anchor, covered, width, height, area);
        }

        var isMouse = anchor.Source == AnchorSource.Mouse;
        var x = isMouse ? anchor.X + MouseOffset : anchor.X;
        var y = anchor.Bottom + (isMouse ? MouseOffset : CaretGap);

        var fitsBelow = y + height + ScreenMargin <= area.Bottom;
        var anchorVisible = anchor.Top < area.Bottom;
        if (!fitsBelow && anchorVisible)
        {
            var above = anchor.Top - CaretGap - height;
            if (above >= area.Top)
            {
                y = above;
            }
        }

        return Clamp(x, y, width, height, area);
    }

    /// <summary>
    /// Окно оболочки (например, поиск «Пуск») лежит выше нашей подсказки, поэтому внутри него её не видно.
    /// Ставим подсказку над верхним краем окна, а если там нет места — под нижним краем. По горизонтали держимся у каретки.
    /// </summary>
    private static (int X, int Y) PlaceOutside(Anchor anchor, WorkArea covered, int width, int height, WorkArea area)
    {
        var x = Math.Clamp(anchor.X, covered.Left, Math.Max(covered.Left, covered.Right - width));
        var above = covered.Top - CaretGap - height;
        var y = above >= area.Top ? above : covered.Bottom + CaretGap;
        return Clamp(x, y, width, height, area);
    }

    /// <summary>Сдвигает подсказку так, чтобы она целиком лежала в рабочей области.</summary>
    private static (int X, int Y) Clamp(int x, int y, int width, int height, WorkArea area)
    {
        var maxX = Math.Max(area.Left, area.Right - width - ScreenMargin);
        var maxY = Math.Max(area.Top, area.Bottom - height - ScreenMargin);
        return (Math.Clamp(x, area.Left, maxX), Math.Clamp(y, area.Top, maxY));
    }
}
