namespace KeyMend.Input;

/// <summary>
/// Распознаёт консольные окна по имени класса. В консолях исправление ввода не работает: Ctrl+C там прерывает
/// команду, а UI Automation не показывает ни выделения, ни строки ввода (SPEC F3). Без WinAPI.
/// </summary>
internal static class TerminalWindows
{
    /// <summary>Имена классов окон консолей: классическая консоль, Windows Terminal, mintty (Git Bash), ConEmu.</summary>
    private static readonly string[] _classNames =
    [
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "mintty", "VirtualConsoleClass",
    ];

    /// <summary>Проверяет, что класс окна принадлежит консоли.</summary>
    /// <param name="className">Имя класса окна.</param>
    /// <returns><c>true</c> для известных консолей.</returns>
    public static bool IsTerminal(ReadOnlySpan<char> className)
    {
        foreach (var name in _classNames)
        {
            if (className.Equals(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
