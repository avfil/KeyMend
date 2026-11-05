using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>P/Invoke к <c>powrprof.dll</c>: сон и гибернация.</summary>
internal static partial class Powrprof
{
    /// <summary>Усыпляет компьютер или отправляет его в гибернацию; значение <c>BOOLEAN</c> занимает один байт.</summary>
    /// <param name="hibernate"><c>true</c> — гибернация, <c>false</c> — сон.</param>
    /// <param name="force">Не используется системой.</param>
    /// <param name="wakeupEventsDisabled"><c>true</c> — отключить все события пробуждения.</param>
    /// <returns><c>false</c>, если система отказала. Возвращается после пробуждения.</returns>
    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool force,
        [MarshalAs(UnmanagedType.U1)] bool wakeupEventsDisabled);
}
