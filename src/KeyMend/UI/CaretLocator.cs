using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using Accessibility;
using KeyMend.Input;
using KeyMend.Native;
using KeyMend.Storage;

namespace KeyMend.UI;

/// <summary>
/// Ищет, где показать подсказку EN/RU (SPEC F2.2): системная каретка, затем MSAA, затем UI Automation, затем курсор мыши.
/// Вызывается из потока функций, не из UI и не из колбэка хука; запросы в чужие процессы идут с таймаутом.
/// </summary>
internal static class CaretLocator
{
    /// <summary>Сколько ждать ответ чужого процесса по MSAA, мс. Зависшее окно не должно тормозить поток функций.</summary>
    internal const int MsaaTimeoutMs = 150;

    /// <summary>Сколько ждать UI Automation, мс. Первый вызов в процессе медленнее, поэтому есть <see cref="WarmUp"/>.</summary>
    internal const int UiaTimeoutMs = 300;

    /// <summary>Примерная высота строки в физических пикселях для каретки в пустом поле, у которого нет прямоугольников текста.</summary>
    internal const double EmptyFieldLineHeight = 30;

    /// <summary>Процессы окон оболочки Windows 10/11, которые перекрывают обычные окна «поверх всех».</summary>
    private static readonly HashSet<string> _shellOverlayProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchApp.exe",
        "SearchUI.exe",
        "SearchHost.exe",
        "StartMenuExperienceHost.exe",
        "ShellExperienceHost.exe",
    };

    /// <summary>Заранее загружает UI Automation в фоне, чтобы первое переключение в Windows Terminal не упёрлось в таймаут.</summary>
    public static void WarmUp() =>
        _ = Task.Run(() =>
        {
            try
            {
                _ = AutomationElement.FocusedElement;
            }
            catch (Exception ex) when (IsUiaFailure(ex))
            {
                // Прогрев необязателен: при ошибке первый реальный запрос просто повторит попытку.
            }
        });

    /// <summary>Находит точку привязки для указанного режима; в режиме мыши каретку не ищет.</summary>
    /// <param name="position">Режим расположения подсказки.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Точка привязки или <c>null</c>, если не нашлась даже мышь.</returns>
    public static async Task<Anchor?> LocateAsync(IndicatorPosition position, CancellationToken cancellationToken)
    {
        Anchor? anchor = null;
        if (position == IndicatorPosition.Caret)
        {
            anchor = await TryLocateCaretAsync(cancellationToken);
        }

        if (anchor is null && User32.GetCursorPos(out var point))
        {
            anchor = new Anchor(point.X, point.Y, point.Y, AnchorSource.Mouse);
        }

        return anchor is { } found ? found with { Obscured = FindShellOverlay() } : null;
    }

    /// <summary>
    /// Определяет положение каретки в текстовом элементе по <c>TextPattern</c>. Пустая область выделения (каретка)
    /// прямоугольника не имеет, поэтому расширяем её на символ вперёд и берём левый край клетки; в конце строки
    /// расширяем назад и берём правый край предыдущего символа.
    /// </summary>
    /// <param name="element">Сфокусированный элемент или <c>null</c>.</param>
    /// <returns>Точка привязки или <c>null</c>.</returns>
    internal static Anchor? AnchorFromElement(AutomationElement? element)
    {
        if (element is null
            || !element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
            || pattern is not TextPattern text)
        {
            return null;
        }

        var selection = text.GetSelection();
        if (selection.Length == 0)
        {
            return null;
        }

        var forward = selection[0].Clone();
        forward.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1);
        if (forward.GetBoundingRectangles() is [var next, ..])
        {
            return new Anchor((int)next.Left, (int)next.Top, (int)next.Bottom, AnchorSource.Uia);
        }

        var backward = selection[0].Clone();
        backward.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -1);
        if (backward.GetBoundingRectangles() is [var previous, ..])
        {
            return new Anchor((int)previous.Right, (int)previous.Top, (int)previous.Bottom, AnchorSource.Uia);
        }

        // Пустое поле (например, «Сообщение…» в Telegram): у документа нет ни одного прямоугольника,
        // а каретка стоит у левого края самого поля. В высоком редакторе она вверху, поэтому берём не больше одной строки.
        if (text.DocumentRange.GetBoundingRectangles().Length == 0)
        {
            var bounds = element.Current.BoundingRectangle;
            if (!bounds.IsEmpty && bounds.Height > 0)
            {
                var bottom = (int)(bounds.Top + Math.Min(bounds.Height, EmptyFieldLineHeight));
                return new Anchor((int)bounds.Left, (int)bounds.Top, bottom, AnchorSource.Uia);
            }
        }

        return null;
    }

    /// <summary>Ошибки UI Automation, которые означают «элемент недоступен», а не сбой программы.</summary>
    /// <param name="ex">Исключение.</param>
    /// <returns><c>true</c>, если это ожидаемый отказ.</returns>
    private static bool IsUiaFailure(Exception ex) =>
        ex is ElementNotAvailableException or InvalidOperationException or COMException
            or NotSupportedException or ArgumentException or TimeoutException;

    /// <summary>
    /// Если на переднем плане окно оболочки Windows («Пуск», поиск, центр уведомлений), возвращает его прямоугольник.
    /// Эти окна лежат в слое выше обычных окон «поверх всех»: подсказка под ними не видна, поэтому её ставят снаружи.
    /// </summary>
    private static WorkArea? FindShellOverlay()
    {
        var foreground = User32.GetForegroundWindow();
        var name = foreground == 0 ? null : ForegroundWatcher.ResolveProcessName(foreground);
        if (name is null || !_shellOverlayProcesses.Contains(name) || !User32.GetWindowRect(foreground, out var rect))
        {
            return null;
        }

        return new WorkArea(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>Пробует системную каретку, затем MSAA в окне с фокусом активного потока, затем UI Automation.</summary>
    private static async Task<Anchor?> TryLocateCaretAsync(CancellationToken cancellationToken)
    {
        var foreground = User32.GetForegroundWindow();
        if (foreground == 0)
        {
            return null;
        }

        var threadId = User32.GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { CbSize = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        if (!User32.GetGUIThreadInfo(threadId, ref info))
        {
            return await TryMsaaThenUiaAsync(foreground, cancellationToken);
        }

        // Браузеры на Chromium заводят системную каретку с неверным положением (внизу слева окна): им сначала MSAA и UIA.
        if (IsChromium(foreground)
            && await TryMsaaThenUiaAsync(info.HwndFocus != 0 ? info.HwndFocus : foreground, cancellationToken) is { } chromiumAnchor)
        {
            return chromiumAnchor;
        }

        // Нулевая ширина у каретки — норма, поэтому проверяем только высоту.
        if (info.HwndCaret != 0 && info.RcCaret.Bottom > info.RcCaret.Top)
        {
            var point = new Native.Point { X = info.RcCaret.Left, Y = info.RcCaret.Bottom };
            if (User32.ClientToScreen(info.HwndCaret, ref point))
            {
                return new Anchor(point.X, point.Y - (info.RcCaret.Bottom - info.RcCaret.Top), point.Y, AnchorSource.Caret);
            }
        }

        return await TryMsaaThenUiaAsync(info.HwndFocus != 0 ? info.HwndFocus : foreground, cancellationToken);
    }

    /// <summary>Окно браузера на Chromium (Chrome, Edge, Яндекс, Electron): класс начинается с <c>Chrome_</c>.</summary>
    /// <param name="hwnd">Окно переднего плана.</param>
    /// <returns><c>true</c> для окон Chromium.</returns>
    private static unsafe bool IsChromium(nint hwnd)
    {
        const string Prefix = "Chrome_";
        var buffer = stackalloc char[64];
        var length = User32.GetClassName(hwnd, buffer, 64);
        return new ReadOnlySpan<char>(buffer, length).StartsWith(Prefix, StringComparison.Ordinal);
    }

    /// <summary>Спрашивает каретку по MSAA, а если не вышло — по UI Automation.</summary>
    /// <param name="hwnd">Окно с фокусом.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private static async Task<Anchor?> TryMsaaThenUiaAsync(nint hwnd, CancellationToken cancellationToken) =>
        await WithTimeoutAsync(() => QueryMsaa(hwnd), MsaaTimeoutMs, cancellationToken)
        ?? await WithTimeoutAsync(QueryUia, UiaTimeoutMs, cancellationToken);

    /// <summary>
    /// Выполняет запрос в чужой процесс в пуле потоков с таймаутом: зависшее окно не должно тормозить поток функций.
    /// При таймауте фоновая задача просто брошена.
    /// </summary>
    /// <param name="query">Запрос.</param>
    /// <param name="timeoutMs">Сколько ждать, мс.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private static async Task<Anchor?> WithTimeoutAsync(Func<Anchor?> query, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(query, cancellationToken).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    /// <summary>Читает сфокусированный элемент; любая ошибка означает «не нашли».</summary>
    private static Anchor? QueryUia()
    {
        try
        {
            return AnchorFromElement(AutomationElement.FocusedElement);
        }
        catch (Exception ex) when (IsUiaFailure(ex))
        {
            return null;
        }
    }

    /// <summary>Вызов <c>AccessibleObjectFromWindow</c> и чтение положения каретки; любая ошибка означает «не нашли».</summary>
    /// <param name="hwnd">Окно.</param>
    private static Anchor? QueryMsaa(nint hwnd)
    {
        nint pointer = 0;
        object? comObject = null;
        try
        {
            if (Oleacc.AccessibleObjectFromWindow(hwnd, Win32.ObjIdCaret, in Oleacc.IidAccessible, out pointer) != 0
                || pointer == 0)
            {
                return null;
            }

            comObject = Marshal.GetObjectForIUnknown(pointer);
            if (comObject is not IAccessible accessible)
            {
                return null;
            }

            accessible.accLocation(out var x, out var y, out var width, out var height, 0);

            // Пустой прямоугольник в нуле — приложение каретку не отдаёт.
            return height > 0 || width > 0 ? new Anchor(x, y, y + height, AnchorSource.Msaa) : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or NotImplementedException)
        {
            return null;
        }
        finally
        {
            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }

            if (pointer != 0)
            {
                Marshal.Release(pointer);
            }
        }
    }
}
