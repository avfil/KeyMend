using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>P/Invoke к <c>dwmapi.dll</c>: оформление рамки окна.</summary>
internal static partial class Dwmapi
{
    /// <summary>Атрибут окна: тёмная рамка и заголовок (<c>DWMWA_USE_IMMERSIVE_DARK_MODE</c>, Windows 10 20H1 и новее).</summary>
    public const uint UseImmersiveDarkMode = 20;

    /// <summary>Задаёт атрибут оформления окна.</summary>
    /// <param name="hwnd">Окно.</param>
    /// <param name="attribute">Атрибут <c>DWMWA_*</c>.</param>
    /// <param name="value">Значение (<c>BOOL</c>: 1 или 0).</param>
    /// <param name="size">Размер значения в байтах.</param>
    /// <returns><c>HRESULT</c>; ноль — успех.</returns>
    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, uint attribute, ref int value, int size);
}
