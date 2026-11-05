using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>P/Invoke к <c>shell32.dll</c>: значок в области уведомлений и запуск файлов оболочкой.</summary>
internal static partial class Shell32
{
    /// <summary>Добавляет, меняет или удаляет значок в области уведомлений; <c>false</c> — оболочка отказала (например, ещё не запущена).</summary>
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShellNotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    /// <summary>Открывает файл или папку оболочкой; результат больше 32 — успех.</summary>
    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint ShellExecute(nint hwnd, string? lpOperation, string lpFile, string? lpParameters, string? lpDirectory, int nShowCmd);
}
