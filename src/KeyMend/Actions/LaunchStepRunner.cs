using System.Collections.Concurrent;
using System.Globalization;
using KeyMend.Infrastructure;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.UI;

namespace KeyMend.Actions;

/// <summary>Шаг «Запустить программу» (SPEC F4.1).</summary>
internal interface ILaunchStepRunner
{
    /// <summary>Запускает программу или скрипт; при «ждать завершения» возвращается после выхода процесса.</summary>
    /// <param name="rule">Правило (для сообщений).</param>
    /// <param name="step">Шаг.</param>
    /// <param name="cancellationToken">Отмена при завершении программы; запущенный процесс при этом не останавливается.</param>
    /// <returns>Задача запуска.</returns>
    /// <exception cref="InvalidOperationException">Запустить не удалось: пользователь уже уведомлён, цепочка прерывается.</exception>
    Task LaunchAsync(Rule rule, LaunchStep step, CancellationToken cancellationToken);
}

/// <summary>
/// Запускает программы правил с правами обычного пользователя (<see cref="IProcessLauncher"/>), следит за повторным запуском
/// и сообщает об ошибках уведомлением из трея. В журнал идут только имя правила и код возврата: без вывода программы.
/// </summary>
/// <param name="environment">Окружение для разбора путей шага.</param>
/// <param name="launcher">Запуск процессов без повышения прав.</param>
/// <param name="notifier">Уведомления из трея.</param>
/// <param name="log">Журнал.</param>
internal sealed class LaunchStepRunner(LaunchEnvironment environment, IProcessLauncher launcher, INotifier notifier, ILog log) : ILaunchStepRunner
{
    /// <summary>Заголовок уведомлений.</summary>
    private static readonly string _title = Strings.AppName;

    /// <summary>Процессы, которые ещё могут работать: ключ — правило и шаг, чтобы один файл с разными аргументами не мешал сам себе.</summary>
    private readonly ConcurrentDictionary<(string RuleId, LaunchStep Step), IRunningProcess> _running = new();

    /// <summary>Процессы, остановленные самой программой: их код возврата ошибкой не считается.</summary>
    private readonly ConcurrentDictionary<IRunningProcess, bool> _stopped = new();

    /// <inheritdoc />
    public async Task LaunchAsync(Rule rule, LaunchStep step, CancellationToken cancellationToken)
    {
        if (!LaunchCommandBuilder.TryBuild(step, environment, out var command, out var error))
        {
            throw Fail(rule, error);
        }

        var key = (rule.Id, step);
        if (_running.TryGetValue(key, out var previous) && !previous.HasExited)
        {
            switch (step.Repeat)
            {
                case LaunchRepeat.Ignore:
                    log.Info($"rule '{rule.Name}': launch ignored, the previous process is still running");
                    return;
                case LaunchRepeat.StopPrevious:
                    _stopped[previous] = true;
                    previous.Kill();
                    break;
            }
        }

        IRunningProcess? process;
        try
        {
            process = launcher.Start(command);
        }
        catch (InvalidOperationException ex)
        {
            log.Error($"rule '{rule.Name}': launch failed", ex);
            notifier.Notify(_title, Strings.Fill(Strings.LaunchFailed, rule.Name), NotifyLevel.Error);
            throw;
        }

        if (process is null)
        {
            log.Info($"rule '{rule.Name}': started, the process is not tracked");
            return;
        }

        _running[key] = process;
        log.Info($"rule '{rule.Name}': started");
        var watching = WatchAsync(rule, key, process, cancellationToken);
        if (step.Wait)
        {
            await watching;
        }
    }

    /// <summary>Сообщает об ошибке в шаге: журнал и уведомление.</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="reason">Причина без содержимого ввода.</param>
    /// <returns>Исключение, которое прерывает цепочку.</returns>
    private InvalidOperationException Fail(Rule rule, string reason)
    {
        log.Error($"rule '{rule.Name}': launch rejected, {reason}");
        notifier.Notify(_title, Strings.Fill(Strings.LaunchRejected, rule.Name, Describe(reason)), NotifyLevel.Error);
        return new InvalidOperationException($"rule '{rule.Name}': {reason}");
    }

    /// <summary>Переводит причину отказа на русский для уведомления.</summary>
    /// <param name="reason">Причина из <see cref="LaunchCommandBuilder"/>.</param>
    /// <returns>Текст для пользователя.</returns>
    private static string Describe(string reason) => reason switch
    {
        "file is not set" => Strings.LaunchReasonNoFile,
        "file is not found" => Strings.LaunchReasonFileMissing,
        "working directory is not found" => Strings.LaunchReasonWorkDirMissing,
        "path is outside the scripts folder" => Strings.LaunchReasonOutsideScripts,
        "pwsh.exe is not found" => Strings.LaunchReasonNoPwsh,
        _ => Strings.LaunchReasonBad,
    };

    /// <summary>Ждёт выхода процесса, убирает его из учёта и сообщает о ненулевом коде возврата.</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="key">Ключ учёта процесса.</param>
    /// <param name="process">Процесс.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task WatchAsync(Rule rule, (string RuleId, LaunchStep Step) key, IRunningProcess process, CancellationToken cancellationToken)
    {
        try
        {
            var code = await process.WaitForExitAsync(cancellationToken);
            if (code != 0 && !_stopped.ContainsKey(process))
            {
                log.Error($"rule '{rule.Name}': process exited with code {code}");
                notifier.Notify(_title, Strings.Fill(Strings.LaunchExitCode, rule.Name, code.ToString(CultureInfo.CurrentCulture)), NotifyLevel.Error);
            }
            else
            {
                log.Info($"rule '{rule.Name}': process exited with code {code}");
            }
        }
        catch (OperationCanceledException)
        {
            // Программа завершается: процесс остаётся работать, учёт больше не нужен.
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log.Error($"rule '{rule.Name}': could not watch the process", ex);
        }
        finally
        {
            _ = _running.TryRemove(new KeyValuePair<(string RuleId, LaunchStep Step), IRunningProcess>(key, process));
            _ = _stopped.TryRemove(process, out _);
            process.Dispose();
        }
    }
}
