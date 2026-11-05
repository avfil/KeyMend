using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using KeyMend.Native;

namespace KeyMend.Infrastructure;

/// <summary>
/// Питание через WinAPI. Сон и гибернация — <c>SetSuspendState</c>, блокировка — <c>LockWorkStation</c>. Выключение,
/// перезагрузка и выход выполняет системная <c>shutdown.exe</c> с нулевым временем ожидания: она сама включает нужную привилегию и,
/// без ключа <c>/f</c>, не закрывает программы принудительно.
/// </summary>
internal sealed class Win32SystemPower : ISystemPower
{
    /// <summary>Раздел реестра с настройками питания.</summary>
    private const string _powerKey = @"SYSTEM\CurrentControlSet\Control\Power";

    /// <summary>Значение реестра: гибернация включена (1) или выключена (0).</summary>
    private const string _hibernateValue = "HibernateEnabled";

    /// <inheritdoc />
    public bool IsHibernateEnabled
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(_powerKey);
            return key?.GetValue(_hibernateValue) is int enabled && enabled != 0;
        }
    }

    /// <inheritdoc />
    public bool Sleep() => Powrprof.SetSuspendState(false, false, false);

    /// <inheritdoc />
    public bool Hibernate() => Powrprof.SetSuspendState(true, false, false);

    /// <inheritdoc />
    public bool Lock() => User32.LockWorkStation();

    /// <inheritdoc />
    public bool Shutdown() => RunShutdownTool("/s /t 0");

    /// <inheritdoc />
    public bool Restart() => RunShutdownTool("/r /t 0");

    /// <inheritdoc />
    public bool SignOut() => RunShutdownTool("/l");

    /// <summary>Запускает системную <c>shutdown.exe</c> без окна и не ждёт её завершения.</summary>
    /// <param name="arguments">Аргументы команды.</param>
    /// <returns><c>true</c>, если процесс запущен.</returns>
    private static bool RunShutdownTool(string arguments)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using var process = Process.Start(info);
            return process is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
