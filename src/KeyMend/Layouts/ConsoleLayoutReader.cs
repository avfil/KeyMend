using KeyMend.Native;

namespace KeyMend.Layouts;

/// <summary>
/// Читает раскладку консоли <c>conhost</c>. Поток самого окна консоли всегда отдаёт <c>HKL = 0</c> (проверено
/// экспериментом 07.10.2026), а настоящую раскладку держат потоки процесса <c>conhost.exe</c>: после
/// <c>WM_INPUTLANGCHANGEREQUEST</c> она меняется у них.
/// </summary>
internal static unsafe class ConsoleLayoutReader
{
    /// <summary>Имя процесса, владеющего раскладкой консоли.</summary>
    internal const string ConsoleHost = "conhost.exe";

    /// <summary>
    /// Ищет <c>conhost.exe</c> консоли и возвращает раскладку его потоков. Консольным хостом считается процесс
    /// окна, если он сам <c>conhost.exe</c>, либо <c>conhost.exe</c>, чей родитель — процесс окна (так он создаётся для cmd и PowerShell).
    /// </summary>
    /// <param name="windowProcessId">Идентификатор процесса окна консоли.</param>
    /// <returns>HKL или 0, если хост не найден или раскладка не читается.</returns>
    public static nint Read(uint windowProcessId)
    {
        var snapshot = Kernel32.CreateToolhelp32Snapshot(Win32.Th32CsSnapProcessAndThread, 0);
        if (snapshot == Win32.InvalidHandleValue)
        {
            return 0;
        }

        try
        {
            var hosts = FindHosts(snapshot, windowProcessId);
            return hosts.Count == 0 ? 0 : ReadFromThreads(snapshot, hosts);
        }
        finally
        {
            Kernel32.CloseHandle(snapshot);
        }
    }

    /// <summary>Собирает идентификаторы процессов <c>conhost.exe</c>, относящихся к окну.</summary>
    private static HashSet<uint> FindHosts(nint snapshot, uint windowProcessId)
    {
        var hosts = new HashSet<uint>();
        var entry = new ProcessEntry32W { DwSize = (uint)sizeof(ProcessEntry32W) };
        for (var ok = Kernel32.Process32First(snapshot, ref entry); ok; ok = Kernel32.Process32Next(snapshot, ref entry))
        {
            var isHost = IsConsoleHost(entry);
            if (isHost && (entry.Th32ProcessID == windowProcessId || entry.Th32ParentProcessID == windowProcessId))
            {
                hosts.Add(entry.Th32ProcessID);
            }
        }

        return hosts;
    }

    /// <summary>Совпадает ли имя exe записи с <see cref="ConsoleHost"/> (без учёта регистра).</summary>
    private static bool IsConsoleHost(in ProcessEntry32W entry)
    {
        fixed (char* name = entry.SzExeFile)
        {
            var span = new ReadOnlySpan<char>(name, ProcessEntry32W.ExeFileLength);
            var end = span.IndexOf('\0');
            return (end < 0 ? span : span[..end]).Equals(ConsoleHost, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Возвращает первую ненулевую раскладку среди потоков указанных процессов.</summary>
    private static nint ReadFromThreads(nint snapshot, HashSet<uint> processIds)
    {
        var entry = new ThreadEntry32 { DwSize = (uint)sizeof(ThreadEntry32) };
        for (var ok = Kernel32.Thread32First(snapshot, ref entry); ok; ok = Kernel32.Thread32Next(snapshot, ref entry))
        {
            if (!processIds.Contains(entry.Th32OwnerProcessID))
            {
                continue;
            }

            var hkl = User32.GetKeyboardLayout(entry.Th32ThreadID);
            if ((hkl & 0xFFFF) != 0)
            {
                return hkl;
            }
        }

        return 0;
    }
}
