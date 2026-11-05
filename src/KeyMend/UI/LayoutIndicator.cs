using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KeyMend.Native;

namespace KeyMend.UI;

/// <summary>
/// Подсказка с кодом языка (EN / RU) рядом с кареткой или мышью (SPEC F2.2). Одно переиспользуемое окно:
/// не забирает фокус, не активируется, клики проходят сквозь него, скрывается само через заданное время.
/// Вид совпадает с LayoutSwitcher (решение владельца). Все члены вызываются из потока UI.
/// </summary>
internal sealed class LayoutIndicator : Window
{
    /// <summary>Текст с кодом языка.</summary>
    private readonly TextBlock _label;

    /// <summary>Таймер скрытия.</summary>
    private readonly DispatcherTimer _hideTimer;

    /// <summary>Создаёт окно (пока не показывая) и настраивает внешний вид.</summary>
    public LayoutIndicator()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        _label = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        // Windows не даёт окну быть уже минимального размера, поэтому рамку не растягиваем на всё окно:
        // остаток окна прозрачен и пропускает клики.
        Content = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x20, 0x20)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 3, 10, 3),
            Child = _label,
        };

        _hideTimer = new DispatcherTimer();

        // Окно не скрываем через Hide(): у слоистого окна после этого остаётся прежняя картинка, и при следующем
        // Show() она на мгновение появляется на старом месте. Вместо этого «гасим» окно прозрачностью.
        Opacity = 0;
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Opacity = 0;
        };

        SourceInitialized += (_, _) => MakeClickThrough();
    }

    /// <summary>Показывает код языка у точки привязки и перезапускает таймер скрытия.</summary>
    /// <param name="text">Код языка, например «RU».</param>
    /// <param name="anchor">Точка привязки в физических пикселях.</param>
    /// <param name="durationMs">Сколько миллисекунд подсказка видна.</param>
    public void ShowAt(string text, Anchor anchor, int durationMs)
    {
        _label.Text = text;
        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        // Первый показ: окно создаётся прозрачным (Opacity = 0 из конструктора) и дальше остаётся «показанным».
        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        var (x, y) = IndicatorPlacement.Calculate(anchor, width, height, MonitorWorkArea.FromPoint(anchor.X, anchor.Top));

        // Размер не трогаем (SWP_NOSIZE): им управляет WPF. Сначала переносим ещё прозрачное окно, потом включаем видимость.
        User32.SetWindowPos(hwnd, Win32.HwndTopmost, x, y, 0, 0, Win32.SwpNoSize | Win32.SwpNoActivate);
        Opacity = 1;

        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _hideTimer.Start();
    }

    /// <summary>Добавляет расширенные стили: без активации, прозрачно для кликов, не в списке окон.</summary>
    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = User32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);
        User32.SetWindowLongPtr(
            hwnd,
            Win32.GwlExStyle,
            style | Win32.WsExNoActivate | Win32.WsExTransparent | Win32.WsExToolWindow | Win32.WsExLayered);
    }
}
