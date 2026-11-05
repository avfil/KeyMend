using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using KeyMend.Actions;
using KeyMend.Native;
using KeyMend.Rules;

namespace KeyMend.Infrastructure;

/// <summary>
/// Запускает программы и скрипты правил с правами обычного пользователя, даже если сам KeyMend работает от администратора
/// (SPEC 2.6, В8). Из обычного экземпляра процесс стартует напрямую; из администраторского берётся основной токен Проводника
/// (он без повышения) и вызывается <c>CreateProcessWithTokenW</c>. Если Проводника нет, запуск не выполняется: молча
/// запускать от администратора нельзя.
/// </summary>
/// <param name="elevated">KeyMend работает от администратора.</param>
internal sealed class UnelevatedLauncher(bool elevated) : IProcessLauncher
{
    /// <summary>Наибольшая длина командной строки <c>CreateProcessWithTokenW</c> в символах.</summary>
    private const int _maxCommandLine = 1024;

    /// <inheritdoc />
    public IRunningProcess? Start(LaunchCommand command) => elevated ? StartWithShellToken(command) : StartDirect(command);

    /// <summary>Запускает процесс обычным способом (права наследуются от неповышенного KeyMend).</summary>
    /// <param name="command">Команда.</param>
    /// <returns>Процесс или <c>null</c>, если система не вернула процесс.</returns>
    /// <exception cref="InvalidOperationException">Запустить не удалось.</exception>
    private static SystemProcess? StartDirect(LaunchCommand command)
    {
        var info = new ProcessStartInfo(command.FileName, command.Arguments)
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = true,
            WindowStyle = command.Window switch
            {
                LaunchWindow.Hidden => ProcessWindowStyle.Hidden,
                LaunchWindow.Minimized => ProcessWindowStyle.Minimized,
                _ => ProcessWindowStyle.Normal,
            },
        };
        try
        {
            var process = Process.Start(info);
            return process is null ? null : new SystemProcess(process);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            throw new InvalidOperationException("could not start the process", ex);
        }
    }

    /// <summary>Запускает процесс от токена Проводника, то есть без повышения.</summary>
    /// <param name="command">Команда.</param>
    /// <returns>Запущенный процесс.</returns>
    /// <exception cref="InvalidOperationException">Нет Проводника, токен недоступен или запуск не удался.</exception>
    private static HandleProcess StartWithShellToken(LaunchCommand command)
    {
        var commandLine = $"\"{command.FileName}\" {command.Arguments}".TrimEnd();
        if (commandLine.Length >= _maxCommandLine)
        {
            throw new InvalidOperationException("command line is too long to start without elevation");
        }

        var shell = User32.GetShellWindow();
        if (shell == 0)
        {
            throw new InvalidOperationException("shell window is not found: cannot start without elevation");
        }

        _ = User32.GetWindowThreadProcessId(shell, out var shellPid);
        using var shellProcess = new SafeProcessHandle(Kernel32.OpenProcess(Win32.ProcessQueryLimitedInformation, false, shellPid), true);
        if (shellProcess.IsInvalid)
        {
            throw new InvalidOperationException("shell process is not available");
        }

        if (!Advapi32.OpenProcessToken(shellProcess.DangerousGetHandle(), Win32.TokenDuplicate, out var rawShellToken))
        {
            throw new InvalidOperationException($"shell token is not available: {Marshal.GetLastPInvokeError()}");
        }

        using var shellToken = new SafeAccessTokenHandle(rawShellToken);
        if (!Advapi32.DuplicateTokenEx(
                shellToken.DangerousGetHandle(), Win32.TokenForNewProcess, 0, Win32.SecurityImpersonation, Win32.TokenPrimary, out var rawPrimary))
        {
            throw new InvalidOperationException($"token duplication failed: {Marshal.GetLastPInvokeError()}");
        }

        using var primary = new SafeAccessTokenHandle(rawPrimary);
        return CreateProcess(primary, command, commandLine);
    }

    /// <summary>Создаёт процесс от основного токена.</summary>
    /// <param name="token">Основной токен без повышения.</param>
    /// <param name="command">Команда.</param>
    /// <param name="commandLine">Командная строка с именем файла в кавычках.</param>
    /// <returns>Запущенный процесс.</returns>
    /// <exception cref="InvalidOperationException">Создать процесс не удалось.</exception>
    private static unsafe HandleProcess CreateProcess(SafeAccessTokenHandle token, LaunchCommand command, string commandLine)
    {
        var startup = new StartupInfo { Cb = (uint)sizeof(StartupInfo) };
        switch (command.Window)
        {
            case LaunchWindow.Hidden:
                startup.DwFlags = Win32.StartfUseShowWindow;
                startup.WShowWindow = Win32.SwHide;
                break;
            case LaunchWindow.Minimized:
                startup.DwFlags = Win32.StartfUseShowWindow;
                startup.WShowWindow = Win32.SwShowMinNoActive;
                break;
        }

        // Функция может менять командную строку, поэтому нужен собственный изменяемый буфер с нулём на конце.
        var buffer = (commandLine + "\0").ToCharArray();
        bool created;
        ProcessInformation info;
        fixed (char* line = buffer)
        {
            created = Advapi32.CreateProcessWithToken(
                token.DangerousGetHandle(), 0, command.FileName, line, 0, 0, command.WorkingDirectory, ref startup, out info);
        }

        if (!created)
        {
            throw new InvalidOperationException($"CreateProcessWithToken failed: {Marshal.GetLastPInvokeError()}");
        }

        _ = Kernel32.CloseHandle(info.HThread);
        return new HandleProcess(info.HProcess);
    }
}
