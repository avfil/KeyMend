using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KeyMend.Native;

/// <summary>P/Invoke к <c>kernel32.dll</c> (A3): идентификатор потока и имя процесса окна.</summary>
internal static partial class Kernel32
{
    /// <summary>Возвращает идентификатор текущего потока (адрес для <c>PostThreadMessage</c>).</summary>
    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    /// <summary>Возвращает псевдодескриптор текущего процесса.</summary>
    [LibraryImport("kernel32.dll")]
    public static partial nint GetCurrentProcess();

    /// <summary>Задаёт границы рабочего набора; значения −1 и −1 сбрасывают страницы на диск (память освобождается до следующего обращения).</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessWorkingSetSize(nint hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    /// <summary>Открывает процесс с указанными правами; 0 — отказано (например, процесс с повышенными правами).</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    /// <summary>Делает снимок процессов и потоков системы; при ошибке возвращает <see cref="Win32.InvalidHandleValue"/>.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    /// <summary>Читает первую запись о процессе из снимка (<see cref="ProcessEntry32W.DwSize"/> задаётся заранее).</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "Process32FirstW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Process32First(nint hSnapshot, ref ProcessEntry32W lppe);

    /// <summary>Читает следующую запись о процессе; <c>false</c> — записи кончились.</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "Process32NextW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Process32Next(nint hSnapshot, ref ProcessEntry32W lppe);

    /// <summary>Читает первую запись о потоке из снимка (<see cref="ThreadEntry32.DwSize"/> задаётся заранее).</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Thread32First(nint hSnapshot, ref ThreadEntry32 lpte);

    /// <summary>Читает следующую запись о потоке; <c>false</c> — записи кончились.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Thread32Next(nint hSnapshot, ref ThreadEntry32 lpte);

    /// <summary>Закрывает дескриптор.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    /// <summary>Пишет полный путь к exe процесса в буфер; <paramref name="lpdwSize"/> — размер буфера и затем длина строки в символах.</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool QueryFullProcessImageName(nint hProcess, uint dwFlags, char* lpExeName, ref uint lpdwSize);

    /// <summary>Выделяет глобальную память; 0 — не хватило памяти.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

    /// <summary>Фиксирует глобальную память и возвращает указатель; каждому вызову соответствует <see cref="GlobalUnlock"/>.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalLock(nint hMem);

    /// <summary>Снимает фиксацию памяти.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint hMem);

    /// <summary>Размер блока глобальной памяти в байтах; 0 — ошибка.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nuint GlobalSize(nint hMem);

    /// <summary>Освобождает глобальную память; 0 — успех.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalFree(nint hMem);

    /// <summary>Читает код завершения процесса; <see cref="Win32.StillActive"/> — процесс ещё работает.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetExitCodeProcess(SafeProcessHandle hProcess, out int lpExitCode);

    /// <summary>Завершает процесс принудительно с указанным кодом.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateProcess(SafeProcessHandle hProcess, uint uExitCode);
}
