namespace KeyMend.UI;

/// <summary>
/// Место меню трея в физических пикселях (SPEC F10). Как у системного меню с <c>TPM_BOTTOMALIGN</c>: нижний левый угол у точки
/// щелчка; не помещается справа — меню уходит влево от точки, не помещается сверху (панель задач вверху) — под точку.
/// Чистая функция без WinAPI.
/// </summary>
internal static class TrayMenuPlacement
{
    /// <summary>Вычисляет левый верхний угол меню.</summary>
    /// <param name="x">Экранная координата X точки щелчка.</param>
    /// <param name="y">Экранная координата Y точки щелчка.</param>
    /// <param name="width">Ширина меню.</param>
    /// <param name="height">Высота меню.</param>
    /// <param name="area">Рабочая область монитора, на котором лежит точка.</param>
    /// <returns>Левый верхний угол меню, целиком внутри рабочей области, если оно в неё помещается.</returns>
    public static (int X, int Y) Calculate(int x, int y, int width, int height, WorkArea area)
    {
        var left = x + width <= area.Right ? x : x - width;
        var top = y - height >= area.Top ? y - height : y;
        var maxLeft = Math.Max(area.Left, area.Right - width);
        var maxTop = Math.Max(area.Top, area.Bottom - height);
        return (Math.Clamp(left, area.Left, maxLeft), Math.Clamp(top, area.Top, maxTop));
    }
}
