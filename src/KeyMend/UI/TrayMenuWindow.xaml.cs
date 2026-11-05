using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KeyMend.Native;
using KeyMend.Resources;

namespace KeyMend.UI;

/// <summary>
/// Меню значка в трее на WPF (SPEC F10): тема как у окна настроек, пункт в две строки, работа с клавиатуры. Закрывается по
/// клику мимо (потеря активности), Esc и после выбора пункта. Окно одноразовое: на каждое открытие создаётся новое.
/// </summary>
internal sealed partial class TrayMenuWindow : Window
{
    /// <summary>Поле вокруг меню под тень, в независимых от DPI единицах; должно совпадать с разметкой.</summary>
    private const double _shadowMargin = 16;

    /// <summary>Строки меню.</summary>
    private readonly IReadOnlyList<TrayMenuEntry> _entries;

    /// <summary>Строки для привязки; индекс совпадает с <see cref="_entries"/>.</summary>
    private readonly List<TrayMenuRow> _rows;

    /// <summary>Подсвеченная строка или <see cref="TrayMenuNavigator.None"/>.</summary>
    private int _hot = TrayMenuNavigator.None;

    /// <summary>Выбранная команда; сообщается после закрытия окна.</summary>
    private TrayCommand? _chosen;

    /// <summary>Закрытие уже начато: повторные вызовы игнорируются.</summary>
    private bool _closing;

    /// <summary>Создаёт окно; показать его нужно вызовом <see cref="ShowAt"/>.</summary>
    /// <param name="entries">Строки меню.</param>
    public TrayMenuWindow(IReadOnlyList<TrayMenuEntry> entries)
    {
        _entries = entries;
        _rows = [.. entries.Select(e => new TrayMenuRow(e))];
        InitializeComponent();
        Title = Strings.AppName;
        Rows.ItemsSource = _rows;
        SourceInitialized += (_, _) => HideFromAltTab();
        Deactivated += (_, _) => CloseMenu();

        // Команда уходит только после закрытия: «О программе» и «Выход» не должны выполняться из живого окна меню.
        Closed += (_, _) =>
        {
            if (_chosen is { } command)
            {
                Chosen?.Invoke(command);
            }
        };
    }

    /// <summary>Пользователь выбрал доступный пункт; вызывается из <see cref="Window.Closed"/>, уже после закрытия окна.</summary>
    public event Action<TrayCommand>? Chosen;

    /// <summary>Показывает меню у точки щелчка по значку.</summary>
    /// <param name="x">Экранная координата X в физических пикселях.</param>
    /// <param name="y">Экранная координата Y в физических пикселях.</param>
    public void ShowAt(int x, int y)
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        Show();

        // Сначала переносим ещё прозрачное окно на нужный монитор: размер зависит от его масштаба.
        User32.SetWindowPos(hwnd, Win32.HwndTopmost, x, y, 0, 0, Win32.SwpNoSize | Win32.SwpNoActivate);
        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(this);
        var margin = (int)Math.Round(_shadowMargin * dpi.DpiScaleX);
        var width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX) - (2 * margin);
        var height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY) - (2 * margin);
        var (left, top) = TrayMenuPlacement.Calculate(x, y, width, height, MonitorWorkArea.FromPoint(x, y));
        User32.SetWindowPos(hwnd, Win32.HwndTopmost, left - margin, top - margin, 0, 0, Win32.SwpNoSize | Win32.SwpNoActivate);

        Opacity = 1;

        // Без этого окно не станет активным и не потеряет активность по клику мимо.
        User32.SetForegroundWindow(hwnd);
        Activate();
    }

    /// <summary>Закрывает меню без выбора.</summary>
    public void CloseMenu()
    {
        if (!_closing)
        {
            _closing = true;
            Close();
        }
    }

    /// <summary>Убирает окно из Alt+Tab.</summary>
    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = User32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);
        User32.SetWindowLongPtr(hwnd, Win32.GwlExStyle, style | Win32.WsExToolWindow);
    }

    /// <summary>Подсвечивает строку.</summary>
    /// <param name="index">Индекс строки или <see cref="TrayMenuNavigator.None"/>.</param>
    private void SetHot(int index)
    {
        if (_hot == index)
        {
            return;
        }

        if (_hot != TrayMenuNavigator.None)
        {
            _rows[_hot].IsHot = false;
        }

        _hot = index;
        if (_hot != TrayMenuNavigator.None)
        {
            _rows[_hot].IsHot = true;
        }
    }

    /// <summary>Выбирает строку: закрывает меню и сообщает команду после закрытия.</summary>
    /// <param name="index">Индекс строки.</param>
    private void Choose(int index)
    {
        if (index < 0 || index >= _entries.Count || !TrayMenuNavigator.IsSelectable(_entries[index]) || _entries[index].Command is not { } command)
        {
            return;
        }

        _chosen = command;
        CloseMenu();
    }

    /// <summary>Находит индекс строки по источнику события.</summary>
    /// <param name="sender">Элемент строки.</param>
    /// <returns>Индекс или <see cref="TrayMenuNavigator.None"/>.</returns>
    private int IndexOf(object sender) =>
        sender is FrameworkElement { DataContext: TrayMenuRow row } ? _rows.IndexOf(row) : TrayMenuNavigator.None;

    /// <summary>Курсор над строкой: подсвечиваем её, если пункт доступен.</summary>
    private void OnRowMouseMove(object sender, MouseEventArgs e)
    {
        var index = IndexOf(sender);
        SetHot(index >= 0 && TrayMenuNavigator.IsSelectable(_entries[index]) ? index : TrayMenuNavigator.None);
    }

    /// <summary>Кнопка мыши отпущена над строкой: выбор пункта.</summary>
    private void OnRowMouseUp(object sender, MouseButtonEventArgs e)
    {
        Choose(IndexOf(sender));
        e.Handled = true;
    }

    /// <summary>Курсор покинул меню: подсветка снимается.</summary>
    private void OnMouseLeave(object sender, MouseEventArgs e) => SetHot(TrayMenuNavigator.None);

    /// <summary>Клавиши меню: стрелки, Home, End, Enter, пробел, Esc.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                SetHot(TrayMenuNavigator.Next(_entries, _hot, forward: true));
                break;
            case Key.Up:
                SetHot(TrayMenuNavigator.Next(_entries, _hot, forward: false));
                break;
            case Key.Home:
                SetHot(TrayMenuNavigator.First(_entries));
                break;
            case Key.End:
                SetHot(TrayMenuNavigator.Last(_entries));
                break;
            case Key.Enter or Key.Space:
                Choose(_hot);
                break;
            case Key.Escape:
                CloseMenu();
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
