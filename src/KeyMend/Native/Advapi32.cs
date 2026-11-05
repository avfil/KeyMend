using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>P/Invoke к <c>advapi32.dll</c>: токены и запуск процесса от имени токена (запуск без повышения прав, SPEC 2.6).</summary>
internal static unsafe partial class Advapi32
{
    /// <summary>Открывает токен доступа процесса.</summary>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    /// <summary>Создаёт копию токена; <paramref name="tokenType"/> 1 — основной токен (<c>TokenPrimary</c>).</summary>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DuplicateTokenEx(
        nint existingToken, uint desiredAccess, nint tokenAttributes, int impersonationLevel, int tokenType, out nint newToken);

    /// <summary>
    /// Создаёт процесс от имени основного токена. Командная строка должна лежать в изменяемом буфере (функция может её менять).
    /// </summary>
    [LibraryImport("advapi32.dll", EntryPoint = "CreateProcessWithTokenW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcessWithToken(
        nint token,
        uint logonFlags,
        string applicationName,
        char* commandLine,
        uint creationFlags,
        nint environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);
}
