using System.Runtime.InteropServices;
using KeyMend.Native;

namespace KeyMend.UI;

/// <summary>Рабочая область монитора по точке экрана: замена <c>System.Windows.Forms.Screen</c> (WinForms не используем).</summary>
internal static class MonitorWorkArea
{
    /// <summary>Область, которая не ограничивает положение: запасной вариант, если монитор определить не удалось.</summary>
    private static readonly WorkArea _unbounded = new(-100_000, -100_000, 100_000, 100_000);

    /// <summary>Находит рабочую область ближайшего к точке монитора.</summary>
    /// <param name="x">Координата X в физических пикселях.</param>
    /// <param name="y">Координата Y в физических пикселях.</param>
    /// <returns>Рабочая область монитора; безграничная область при ошибке.</returns>
    public static WorkArea FromPoint(int x, int y)
    {
        var monitor = User32.MonitorFromPoint(new Native.Point { X = x, Y = y }, Win32.MonitorDefaultToNearest);
        var info = new MonitorInfo { CbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
        return monitor != 0 && User32.GetMonitorInfo(monitor, ref info)
            ? new WorkArea(info.RcWork.Left, info.RcWork.Top, info.RcWork.Right, info.RcWork.Bottom)
            : _unbounded;
    }
}
