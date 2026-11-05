using System.Windows;
using System.Windows.Interop;
using KeyMend.Infrastructure;
using KeyMend.Native;
using KeyMend.Storage;

namespace KeyMend.UI.Themes;

/// <summary>
/// Переключает светлую и тёмную тему приложения без перезапуска (SPEC F1). <c>Application.ThemeMode</c> из кода недоступен
/// (WPF0001), поэтому подключаются словари Fluent и своя палитра; тёмная рамка окна включается через DWM.
/// Все методы вызываются из потока UI.
/// </summary>
/// <param name="application">Приложение, чьи ресурсы меняются.</param>
/// <param name="systemTheme">Тема приложений Windows.</param>
internal sealed class ThemeManager(Application application, ISystemTheme systemTheme)
{
    /// <summary>Начало адреса словарей Fluent.</summary>
    private const string _fluentBase = "pack://application:,,,/PresentationFramework.Fluent;component/";

    /// <summary>Окна, которым нужна тёмная или светлая рамка.</summary>
    private readonly List<Window> _windows = [];

    /// <summary>Словари, подключённые сейчас; убираются при смене темы.</summary>
    private readonly List<ResourceDictionary> _current = [];

    /// <summary>Выбранная пользователем тема.</summary>
    private AppTheme _theme = AppTheme.System;

    /// <summary>Действующая тема тёмная.</summary>
    private bool _dark;

    /// <summary>Подключены ли словари хотя бы раз.</summary>
    private bool _applied;

    /// <summary>Применяет тему: меняет словари, если тёмная или светлая сторона изменилась, и обновляет рамки окон.</summary>
    /// <param name="theme">Настройка пользователя.</param>
    public void Apply(AppTheme theme)
    {
        _theme = theme;
        var dark = ThemeResolver.IsDark(theme, systemTheme.IsLight());
        if (!_applied || dark != _dark)
        {
            SwapDictionaries(dark);
            _dark = dark;
            _applied = true;
        }

        foreach (var window in _windows)
        {
            ApplyFrame(window);
        }
    }

    /// <summary>Перечитывает тему Windows: нужна при настройке «Как в Windows».</summary>
    public void OnSystemThemeChanged()
    {
        if (_theme == AppTheme.System)
        {
            Apply(_theme);
        }
    }

    /// <summary>Подключает окно: ему сразу ставится рамка текущей темы и ставится при каждой смене темы.</summary>
    /// <param name="window">Окно.</param>
    public void Attach(Window window)
    {
        _windows.Add(window);
        window.Closed += (_, _) => _windows.Remove(window);
        window.SourceInitialized += (_, _) => ApplyFrame(window);
        if (new WindowInteropHelper(window).Handle != 0)
        {
            ApplyFrame(window);
        }
    }

    /// <summary>Заменяет словари Fluent и палитру на пару для нужной темы.</summary>
    /// <param name="dark"><c>true</c> — тёмная.</param>
    private void SwapDictionaries(bool dark)
    {
        var merged = application.Resources.MergedDictionaries;
        foreach (var dictionary in _current)
        {
            merged.Remove(dictionary);
        }

        _current.Clear();
        var side = dark ? "Dark" : "Light";
        _current.Add(new ResourceDictionary { Source = new Uri($"{_fluentBase}Themes/Fluent.{side}.xaml", UriKind.Absolute) });
        _current.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Themes/Palette.{side}.xaml", UriKind.Absolute) });
        foreach (var dictionary in _current)
        {
            merged.Add(dictionary);
        }
    }

    /// <summary>Ставит окну тёмную или светлую рамку и заголовок.</summary>
    /// <param name="window">Окно.</param>
    private void ApplyFrame(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        var value = _dark ? 1 : 0;
        _ = Dwmapi.DwmSetWindowAttribute(handle, Dwmapi.UseImmersiveDarkMode, ref value, sizeof(int));

        // Без этого рамка перерисуется только после смены активного окна.
        _ = User32.SetWindowPos(handle, 0, 0, 0, 0, 0,
            Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoZOrder | Win32.SwpNoActivate | Win32.SwpFrameChanged);
        // Рамку окна DWM перерисовывает только при смене активности: переключаем её туда и обратно.
        var active = User32.GetForegroundWindow() == handle;
        _ = User32.SendMessage(handle, Win32.WmNcActivate, active ? 0u : 1u, 0);
        _ = User32.SendMessage(handle, Win32.WmNcActivate, active ? 1u : 0u, 0);
    }
}
