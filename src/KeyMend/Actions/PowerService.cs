using System.Globalization;
using KeyMend.Infrastructure;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.UI;

namespace KeyMend.Actions;

/// <summary>Окно отсчёта с кнопкой «Отменить» (SPEC F4.2).</summary>
internal interface ICountdownPrompt
{
    /// <summary>Показывает отсчёт и ждёт его конца или отмены.</summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="message">Что произойдёт по окончании отсчёта.</param>
    /// <param name="seconds">Длительность отсчёта в секундах.</param>
    /// <param name="cancellationToken">Закрывает окно, если действие отменено другим способом.</param>
    /// <returns><c>true</c> — отсчёт дошёл до конца, <c>false</c> — пользователь нажал «Отменить».</returns>
    /// <exception cref="OperationCanceledException">Отсчёт отменён через <paramref name="cancellationToken"/>.</exception>
    Task<bool> ConfirmAsync(string title, string message, int seconds, CancellationToken cancellationToken);
}

/// <summary>Шаг «Питание» и единственный таймер питания (SPEC F4.2).</summary>
internal interface IPowerService
{
    /// <summary>Таймер поставлен, снят или его остаток изменился.</summary>
    event Action? Changed;

    /// <summary>Текст про активный таймер для меню и подсказки значка («Сон в 23:40 (через 12 мин)») или <c>null</c>, если таймера нет.</summary>
    string? TimerText { get; }

    /// <summary>Выполняет шаг: действие сразу (с отсчётом, если нужен) или ставит таймер.</summary>
    /// <param name="rule">Правило (для сообщений).</param>
    /// <param name="step">Шаг.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача выполнения; для таймера завершается сразу после его установки.</returns>
    /// <exception cref="InvalidOperationException">Действие невозможно или система отказала: пользователь уже уведомлён.</exception>
    /// <exception cref="StepCancelledException">Пользователь отменил отсчёт: остальные шаги цепочки не выполняются.</exception>
    Task RunAsync(Rule rule, PowerStep step, CancellationToken cancellationToken);

    /// <summary>Снимает таймер, если он есть.</summary>
    /// <returns><c>true</c>, если таймер был.</returns>
    bool CancelTimer();
}

/// <summary>Пользователь отменил шаг: цепочка правила прекращается без ошибки.</summary>
internal sealed class StepCancelledException : Exception
{
    /// <summary>Создаёт исключение без сообщения.</summary>
    public StepCancelledException()
        : base("step was cancelled by the user")
    {
    }
}

/// <summary>
/// Выполняет питание: сон, гибернацию, выключение, перезагрузку, блокировку и выход, сразу или по таймеру. Таймер один: новый
/// заменяет старый. За минуту до срабатывания показывается отсчёт с кнопкой «Отменить», а остаток виден в подсказке значка. Перед
/// действием пауза 250 мс (как в <c>Main.ahk</c>).
/// </summary>
/// <param name="power">Действия с питанием.</param>
/// <param name="prompt">Окно отсчёта.</param>
/// <param name="notifier">Уведомления из трея.</param>
/// <param name="time">Источник времени.</param>
/// <param name="log">Журнал.</param>
internal sealed class PowerService(ISystemPower power, ICountdownPrompt prompt, INotifier notifier, TimeProvider time, ILog log) : IPowerService
{
    /// <summary>Сколько секунд идёт отсчёт перед выключением и перезагрузкой «сейчас».</summary>
    internal const int CountdownSeconds = 10;

    /// <summary>За сколько до срабатывания таймера показывается отсчёт.</summary>
    internal static readonly TimeSpan TimerWarning = TimeSpan.FromMinutes(1);

    /// <summary>Заголовок уведомлений.</summary>
    private const string _title = "KeyMend";

    /// <summary>Пауза перед действием.</summary>
    private static readonly TimeSpan _beforeAction = TimeSpan.FromMilliseconds(250);

    /// <summary>Защита состояния таймера: его читают поток функций и поток UI.</summary>
    private readonly Lock _lock = new();

    /// <summary>Отмена текущего таймера; <c>null</c>, если таймера нет.</summary>
    private CancellationTokenSource? _timerCts;

    /// <summary>Действие и момент срабатывания текущего таймера.</summary>
    private (PowerAction Action, DateTimeOffset Due)? _timer;

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public string? TimerText
    {
        get
        {
            (PowerAction Action, DateTimeOffset Due)? timer;
            lock (_lock)
            {
                timer = _timer;
            }

            if (timer is not { } current)
            {
                return null;
            }

            return PowerTimerText.Format(current.Action, current.Due, time.GetUtcNow(), time.LocalTimeZone);
        }
    }

    /// <inheritdoc />
    public async Task RunAsync(Rule rule, PowerStep step, CancellationToken cancellationToken)
    {
        if (step.Action == PowerAction.Hibernate && !power.IsHibernateEnabled)
        {
            notifier.Notify(_title, Strings.NotifyHibernateOff, NotifyLevel.Warning);
            throw new InvalidOperationException($"rule '{rule.Name}': hibernation is disabled");
        }

        if (step.AfterMinutes > 0)
        {
            StartTimer(rule, step, cancellationToken);
            return;
        }

        if (step.Countdown && step.Action is PowerAction.Shutdown or PowerAction.Restart)
        {
            var message = Strings.Fill(step.Action == PowerAction.Shutdown ? Strings.PowerCountdownShutdown : Strings.PowerCountdownRestart, CountdownSeconds.ToString(CultureInfo.CurrentCulture));
            if (!await prompt.ConfirmAsync(_title, message, CountdownSeconds, cancellationToken))
            {
                log.Info($"rule '{rule.Name}': {step.Action} cancelled by the user");
                throw new StepCancelledException();
            }
        }

        await ExecuteAsync(rule, step.Action, cancellationToken);
    }

    /// <inheritdoc />
    public bool CancelTimer()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            cts = _timerCts;
            _timerCts = null;
            _timer = null;
        }

        if (cts is null)
        {
            return false;
        }

        cts.Cancel();
        cts.Dispose();
        log.Info("power timer cancelled");
        notifier.Notify(_title, Strings.PowerTimerCancelled);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Русское название действия для сообщений.</summary>
    /// <param name="action">Действие.</param>
    /// <returns>Название в нижнем регистре.</returns>
    internal static string Name(PowerAction action) => action switch
    {
        PowerAction.Sleep => Strings.PowerNameSleep,
        PowerAction.Hibernate => Strings.PowerNameHibernate,
        PowerAction.Shutdown => Strings.PowerNameShutdown,
        PowerAction.Restart => Strings.PowerNameRestart,
        PowerAction.Lock => Strings.PowerNameLock,
        _ => Strings.PowerNameSignOut,
    };

    /// <summary>Делает первую букву названия заглавной: название действия в начале фразы.</summary>
    /// <param name="name">Название из <see cref="Name"/>.</param>
    /// <returns>Название с заглавной первой буквой.</returns>
    internal static string Capitalize(string name) => string.Concat(char.ToUpper(name[0], CultureInfo.CurrentCulture).ToString(), name.AsSpan(1));

    /// <summary>Ставит таймер, заменяя предыдущий, и запускает его ожидание.</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="step">Шаг с задержкой.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private void StartTimer(Rule rule, PowerStep step, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var due = time.GetUtcNow().AddMinutes(step.AfterMinutes);
        CancellationTokenSource? previous;
        lock (_lock)
        {
            previous = _timerCts;
            _timerCts = cts;
            _timer = (step.Action, due);
        }

        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
            log.Info("power timer replaced");
        }

        log.Info($"rule '{rule.Name}': power timer set, {step.Action} in {step.AfterMinutes} min");
        var dueLocal = TimeZoneInfo.ConvertTime(due, time.LocalTimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        notifier.Notify(_title, Strings.Fill(Strings.PowerTimerSet, Name(step.Action), step.AfterMinutes.ToString(CultureInfo.CurrentCulture), dueLocal));
        Changed?.Invoke();
        _ = RunTimerAsync(rule, step.Action, due, cts, cancellationToken);
    }

    /// <summary>Ждёт срок таймера, показывает отсчёт за минуту и выполняет действие.</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="action">Действие.</param>
    /// <param name="due">Момент срабатывания.</param>
    /// <param name="cts">Отмена этого таймера; освобождается при снятии или замене.</param>
    /// <param name="appToken">Отмена при завершении программы: действие после снятия таймера от него не зависит.</param>
    private async Task RunTimerAsync(
        Rule rule, PowerAction action, DateTimeOffset due, CancellationTokenSource cts, CancellationToken appToken)
    {
        try
        {
            var token = cts.Token;
            while (due - time.GetUtcNow() > TimerWarning)
            {
                var untilWarning = due - TimerWarning - time.GetUtcNow();
                await Task.Delay(untilWarning < TimeSpan.FromMinutes(1) ? untilWarning : TimeSpan.FromMinutes(1), time, token);
                Changed?.Invoke();
            }

            var seconds = (int)Math.Max(1, Math.Ceiling((due - time.GetUtcNow()).TotalSeconds));
            var message = Strings.Fill(Strings.PowerSoon, Capitalize(Name(action)), seconds.ToString(CultureInfo.CurrentCulture));
            if (!await prompt.ConfirmAsync(_title, message, seconds, token))
            {
                ClearTimer(cts);
                log.Info($"rule '{rule.Name}': power timer cancelled by the user");
                return;
            }

            ClearTimer(cts);
            await ExecuteAsync(rule, action, appToken);
        }
        catch (OperationCanceledException)
        {
            // Таймер снят или заменён, либо программа завершается.
        }
        catch (InvalidOperationException ex)
        {
            ClearTimer(cts);
            log.Error($"rule '{rule.Name}': power timer failed", ex);
        }
    }

    /// <summary>Убирает таймер, если он всё ещё текущий.</summary>
    /// <param name="cts">Отмена того таймера, который сработал.</param>
    private void ClearTimer(CancellationTokenSource cts)
    {
        var changed = false;
        lock (_lock)
        {
            if (ReferenceEquals(_timerCts, cts))
            {
                _timerCts = null;
                _timer = null;
                changed = true;
            }
        }

        if (changed)
        {
            cts.Dispose();
            Changed?.Invoke();
        }
    }

    /// <summary>Выжидает 250 мс и выполняет действие; отказ системы — уведомление и исключение.</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="action">Действие.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <exception cref="InvalidOperationException">Система отказала.</exception>
    private async Task ExecuteAsync(Rule rule, PowerAction action, CancellationToken cancellationToken)
    {
        await Task.Delay(_beforeAction, time, cancellationToken);
        log.Info($"rule '{rule.Name}': power action {action}");
        var done = action switch
        {
            PowerAction.Sleep => power.Sleep(),
            PowerAction.Hibernate => power.Hibernate(),
            PowerAction.Shutdown => power.Shutdown(),
            PowerAction.Restart => power.Restart(),
            PowerAction.Lock => power.Lock(),
            _ => power.SignOut(),
        };
        if (!done)
        {
            notifier.Notify(_title, Strings.Fill(Strings.PowerFailed, Name(action)), NotifyLevel.Error);
            throw new InvalidOperationException($"rule '{rule.Name}': power action {action} failed");
        }
    }
}
