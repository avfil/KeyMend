using KeyMend.Native;

namespace KeyMend.Layouts;

/// <summary>
/// Дешёвое чтение раскладки окна переднего плана для колбэка хука (SPEC F5.1): три вызова WinAPI без выделения памяти,
/// без блокировок и ввода-вывода. Вызывается редко: только когда набранные клавиши совпали с шаблоном сокращения.
/// </summary>
internal static class ForegroundLayout
{
    /// <summary>Читает описатель раскладки потока окна переднего плана.</summary>
    /// <returns>HKL или 0, если окна нет или это консоль (её раскладка читается другим путём и здесь неизвестна).</returns>
    public static nint ReadHkl()
    {
        var foreground = User32.GetForegroundWindow();
        if (foreground == 0 || LayoutService.IsConsoleWindow(foreground))
        {
            return 0;
        }

        var threadId = User32.GetWindowThreadProcessId(foreground, out _);
        return threadId == 0 ? 0 : User32.GetKeyboardLayout(threadId);
    }
}
