using System.IO;
using KeyMend.Native;

namespace KeyMend.Input;

/// <summary>
/// Отслеживает окно переднего плана и имя его процесса. Имя определяется только при смене окна,
/// поэтому обычное нажатие клавиши стоит один вызов <c>GetForegroundWindow</c>. Кэша по pid нет: pid переиспользуются.
/// </summary>
internal sealed unsafe class ForegroundWatcher
{
    /// <summary>Длина буфера для пути к exe.</summary>
    internal const int PathBufferLength = 1024;

    /// <summary>Последнее известное окно переднего плана.</summary>
    private nint _hwnd;

    /// <summary>Имя exe процесса текущего окна (например, <c>mstsc.exe</c>); <c>null</c>, если определить не удалось.</summary>
    public string? ProcessName { get; private set; }

    /// <summary>Проверяет окно переднего плана; возвращает <c>true</c>, если оно сменилось с прошлого вызова.</summary>
    public bool Update()
    {
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == _hwnd)
        {
            return false;
        }

        _hwnd = hwnd;
        ProcessName = ResolveProcessName(hwnd);
        return true;
    }

    /// <summary>Находит имя exe по окну; для процессов с повышенными правами <c>OpenProcess</c> отказывает и возвращается <c>null</c>.</summary>
    internal static string? ResolveProcessName(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        User32.GetWindowThreadProcessId(hwnd, out var pid);
        return ResolveProcessPath(pid) is { } path ? Path.GetFileName(path) : null;
    }

    /// <summary>Находит полный путь к exe процесса; для процессов с повышенными правами возвращается <c>null</c>.</summary>
    /// <param name="pid">Идентификатор процесса.</param>
    /// <returns>Путь или <c>null</c>, если определить не удалось.</returns>
    internal static string? ResolveProcessPath(uint pid)
    {
        var process = Kernel32.OpenProcess(Win32.ProcessQueryLimitedInformation, false, pid);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var buffer = stackalloc char[PathBufferLength];
            var size = (uint)PathBufferLength;
            return Kernel32.QueryFullProcessImageName(process, 0, buffer, ref size)
                ? new string(buffer, 0, (int)size)
                : null;
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }
}
