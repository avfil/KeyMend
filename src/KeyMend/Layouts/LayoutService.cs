using System.Globalization;
using System.Runtime.InteropServices;
using KeyMend.Native;

namespace KeyMend.Layouts;

/// <summary>
/// Чтение и переключение раскладки активного окна (SPEC F2.1). Раскладку надо брать у потока окна с фокусом,
/// а не у нашего процесса, и менять сообщением <c>WM_INPUTLANGCHANGEREQUEST</c> этому окну. Вызывается из потока функций.
/// </summary>
internal sealed unsafe class LayoutService : ILayoutService
{
    /// <summary>Оконный класс консоли <c>conhost</c> (cmd, PowerShell в обычном окне).</summary>
    internal const string ConsoleClass = "ConsoleWindowClass";

    /// <summary>Сколько консольных окон помнить; при переполнении память сбрасывается.</summary>
    internal const int MaxRememberedConsoles = 32;

    /// <summary>
    /// Раскладка, которую мы последней запросили у консольного окна. Запасной вариант на случай, когда
    /// <see cref="ConsoleLayoutReader"/> не нашёл <c>conhost.exe</c>: поток самого окна консоли всегда отдаёт <c>HKL = 0</c>.
    /// </summary>
    private readonly Dictionary<nint, nint> _consoleLayouts = [];

    /// <inheritdoc />
    public LayoutInfo? GetCurrent() => FindTarget() is { } target ? Describe(ReadCurrent(target)) : null;

    /// <inheritdoc />
    public LayoutSnapshot? Snapshot() =>
        FindTarget() is { } target ? new LayoutSnapshot(target.Foreground, Describe(ReadCurrent(target))) : null;

    /// <inheritdoc />
    public LayoutInfo? SwitchPair(int languageA, int languageB)
    {
        if (FindTarget() is not { } target)
        {
            return null;
        }

        var next = LayoutPairSelector.PickPair(GetInstalledLayouts(), ReadCurrent(target), languageA, languageB);
        return next == 0 ? null : RequestLayout(target, next);
    }

    /// <inheritdoc />
    public LayoutInfo? SwitchTo(int languageId)
    {
        if (FindTarget() is not { } target)
        {
            return null;
        }

        var next = LayoutPairSelector.PickSpecific(GetInstalledLayouts(), languageId);
        return next == 0 ? null : RequestLayout(target, next);
    }

    /// <inheritdoc />
    public LayoutPairResolution? ResolvePair(int languageA, int languageB)
    {
        if (FindTarget() is not { } target)
        {
            return null;
        }

        var current = ReadCurrent(target);
        var next = LayoutPairSelector.PickPair(GetInstalledLayouts(), current, languageA, languageB);
        return next == 0 ? null : new LayoutPairResolution(Describe(current), Describe(next));
    }

    /// <inheritdoc />
    public LayoutInfo? FindInstalled(int languageId)
    {
        var hkl = LayoutPairSelector.PickSpecific(GetInstalledLayouts(), languageId);
        return hkl == 0 ? null : Describe(hkl);
    }

    /// <summary>Строит описание раскладки; код языка берётся из младшего слова HKL без привязки к ru/en.</summary>
    /// <param name="hkl">Описатель раскладки.</param>
    /// <returns>Описание с двухбуквенным кодом языка.</returns>
    internal static LayoutInfo Describe(nint hkl)
    {
        var languageId = LayoutPairSelector.LanguageOf(hkl);
        string code;
        try
        {
            code = new CultureInfo(languageId).TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            code = "??";
        }

        return new LayoutInfo(hkl, code);
    }

    /// <summary>Проверяет, что окно — консоль <c>conhost</c> (Windows Terminal сюда не относится: там чтение работает).</summary>
    /// <param name="hwnd">Окно.</param>
    /// <returns><c>true</c> для классической консоли.</returns>
    internal static bool IsConsoleWindow(nint hwnd)
    {
        var buffer = stackalloc char[ConsoleClass.Length + 1];
        var length = User32.GetClassName(hwnd, buffer, ConsoleClass.Length + 1);
        return new ReadOnlySpan<char>(buffer, length).SequenceEqual(ConsoleClass);
    }

    /// <summary>Находит окно с фокусом в активном потоке (иначе активное, иначе окно переднего плана).</summary>
    /// <returns>Цель переключения или <c>null</c>, если окна переднего плана нет.</returns>
    private static Target? FindTarget()
    {
        var foreground = User32.GetForegroundWindow();
        if (foreground == 0)
        {
            return null;
        }

        var threadId = User32.GetWindowThreadProcessId(foreground, out var processId);
        if (threadId == 0)
        {
            return null;
        }

        var info = new GuiThreadInfo { CbSize = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        var window = User32.GetGUIThreadInfo(threadId, ref info)
            ? FirstNonZero(info.HwndFocus, info.HwndActive, foreground)
            : foreground;
        return new Target(window, threadId, foreground, IsConsoleWindow(foreground), processId);
    }

    /// <summary>Первое ненулевое значение (последнее — запасное).</summary>
    private static nint FirstNonZero(nint first, nint second, nint fallback) =>
        first != 0 ? first : second != 0 ? second : fallback;

    /// <summary>Список установленных раскладок.</summary>
    /// <returns>Описатели раскладок в порядке системы.</returns>
    internal static nint[] GetInstalledLayouts()
    {
        var count = User32.GetKeyboardLayoutList(0, []);
        if (count <= 0)
        {
            return [];
        }

        var buffer = new nint[count];
        var filled = User32.GetKeyboardLayoutList(count, buffer);
        return filled == count ? buffer : buffer[..Math.Max(filled, 0)];
    }

    /// <summary>Окно, которому адресуется смена раскладки, и сведения о нём.</summary>
    /// <param name="Window">Окно, которому отправляется запрос.</param>
    /// <param name="ThreadId">Поток окна переднего плана.</param>
    /// <param name="Foreground">Окно переднего плана верхнего уровня; ключ памяти консолей.</param>
    /// <param name="IsConsole">Окно переднего плана — консоль <c>conhost</c>.</param>
    /// <param name="ProcessId">Процесс окна переднего плана.</param>
    private readonly record struct Target(nint Window, uint ThreadId, nint Foreground, bool IsConsole, uint ProcessId);

    /// <summary>
    /// Текущая раскладка окна. Для консоли спрашиваем потоки <c>conhost.exe</c>, затем вспоминаем последнюю запрошенную нами;
    /// для остальных окон читаем раскладку потока окна.
    /// </summary>
    private nint ReadCurrent(Target target)
    {
        if (target.IsConsole)
        {
            var fromHost = ConsoleLayoutReader.Read(target.ProcessId);
            if (fromHost != 0)
            {
                return fromHost;
            }

            if (_consoleLayouts.TryGetValue(target.Foreground, out var remembered))
            {
                return remembered;
            }
        }

        return User32.GetKeyboardLayout(target.ThreadId);
    }

    /// <summary>Просит окно сменить раскладку и запоминает её для консоли; возвращает описание или <c>null</c>, если запрос не принят.</summary>
    private LayoutInfo? RequestLayout(Target target, nint next)
    {
        // Сообщение, а не ActivateKeyboardLayout: тот меняет раскладку только своего потока.
        if (!User32.PostMessage(target.Window, Win32.WmInputLangChangeRequest, 0, next))
        {
            return null;
        }

        if (target.IsConsole)
        {
            if (_consoleLayouts.Count >= MaxRememberedConsoles)
            {
                _consoleLayouts.Clear();
            }

            _consoleLayouts[target.Foreground] = next;
        }

        return Describe(next);
    }
}
