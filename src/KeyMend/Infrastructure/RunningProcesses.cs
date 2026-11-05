using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using KeyMend.Native;

namespace KeyMend.Infrastructure;

/// <summary>Процесс, запущенный через <see cref="Process"/>.</summary>
/// <param name="process">Процесс; освобождается вместе с обёрткой.</param>
internal sealed class SystemProcess(Process process) : IRunningProcess
{
    /// <inheritdoc />
    public bool HasExited => process.HasExited;

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    /// <inheritdoc />
    public void Kill()
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Процесс уже завершился: останавливать нечего.
        }
    }

    /// <inheritdoc />
    public void Dispose() => process.Dispose();
}

/// <summary>Процесс, созданный через <c>CreateProcessWithTokenW</c>: известен только дескриптор.</summary>
internal sealed class HandleProcess : IRunningProcess
{
    /// <summary>Дескриптор процесса; закрывается вместе с обёрткой.</summary>
    private readonly SafeProcessHandle _handle;

    /// <summary>Принимает во владение дескриптор процесса.</summary>
    /// <param name="processHandle">Дескриптор из <c>PROCESS_INFORMATION</c>.</param>
    public HandleProcess(nint processHandle) => _handle = new SafeProcessHandle(processHandle, true);

    /// <inheritdoc />
    public bool HasExited => _handle.IsClosed || (Kernel32.GetExitCodeProcess(_handle, out var code) && code != Win32.StillActive);

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Обёртка над чужим дескриптором: закрывает его только Dispose этого объекта.
        using var wait = new ManualResetEvent(false)
        {
            SafeWaitHandle = new SafeWaitHandle(_handle.DangerousGetHandle(), ownsHandle: false),
        };
        var registration = ThreadPool.RegisterWaitForSingleObject(
            wait, static (state, _) => ((TaskCompletionSource)state!).TrySetResult(), exited, Timeout.Infinite, true);
        try
        {
            await exited.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _ = registration.Unregister(null);
        }

        return Kernel32.GetExitCodeProcess(_handle, out var code) ? code : -1;
    }

    /// <inheritdoc />
    public void Kill() => _ = Kernel32.TerminateProcess(_handle, 1);

    /// <inheritdoc />
    public void Dispose() => _handle.Dispose();
}
