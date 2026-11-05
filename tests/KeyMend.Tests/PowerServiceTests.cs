using System.Globalization;
using KeyMend.Actions;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="PowerService"/>: действия сейчас, отсчёт, единственный таймер и отмена (SPEC F4.2).</summary>
public sealed class PowerServiceTests
{
    /// <summary>Питание-заглушка: запоминает действия.</summary>
    private sealed class FakePower : ISystemPower
    {
        /// <summary>Выполненные действия.</summary>
        public List<string> Actions { get; } = [];

        /// <summary>Что вернут все действия.</summary>
        public bool Succeeds { get; set; } = true;

        /// <inheritdoc />
        public bool IsHibernateEnabled { get; set; } = true;

        /// <inheritdoc />
        public bool Sleep() => Do("Sleep");

        /// <inheritdoc />
        public bool Hibernate() => Do("Hibernate");

        /// <inheritdoc />
        public bool Shutdown() => Do("Shutdown");

        /// <inheritdoc />
        public bool Restart() => Do("Restart");

        /// <inheritdoc />
        public bool Lock() => Do("Lock");

        /// <inheritdoc />
        public bool SignOut() => Do("SignOut");

        /// <summary>Записывает действие.</summary>
        /// <param name="name">Название действия.</param>
        /// <returns>Результат из <see cref="Succeeds"/>.</returns>
        private bool Do(string name)
        {
            Actions.Add(name);
            return Succeeds;
        }
    }

    /// <summary>Окно отсчёта-заглушка: ждёт решения теста.</summary>
    private sealed class FakePrompt : ICountdownPrompt
    {
        /// <summary>Показанные отсчёты: секунды и сообщение.</summary>
        public List<(int Seconds, string Message)> Shown { get; } = [];

        /// <summary>Ответ, которым завершится следующий отсчёт; <c>null</c> — ждать отмены токеном.</summary>
        public bool? Answer { get; set; }

        /// <inheritdoc />
        public async Task<bool> ConfirmAsync(string title, string message, int seconds, CancellationToken cancellationToken)
        {
            Shown.Add((seconds, message));
            if (Answer is { } answer)
            {
                return answer;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return true;
        }
    }

    /// <summary>Уведомления-заглушка.</summary>
    private sealed class FakeNotifier : INotifier
    {
        /// <summary>Показанные сообщения.</summary>
        public List<string> Messages { get; } = [];

        /// <inheritdoc />
        public void Notify(string title, string message, NotifyLevel level = NotifyLevel.Info) => Messages.Add(message);
    }

    /// <summary>Питание под тестом.</summary>
    private readonly FakePower _power = new();

    /// <summary>Окно отсчёта под тестом.</summary>
    private readonly FakePrompt _prompt = new() { Answer = true };

    /// <summary>Уведомления под тестом.</summary>
    private readonly FakeNotifier _notifier = new();

    /// <summary>Управляемое время теста.</summary>
    private readonly TestTimeProvider _time = new(DateTimeOffset.UnixEpoch);

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Правило теста.</summary>
    private readonly Rule _rule = new() { Id = "p", Name = "Сон", Trigger = new PressTrigger { Key = VirtualKey.Space }, Steps = [] };

    /// <summary>Создаёт сервис под тестом.</summary>
    private PowerService CreateService() => new(_power, _prompt, _notifier, _time, _log);

    /// <summary>Выполняет шаг и двигает время на паузу перед действием.</summary>
    private async Task RunNow(PowerService service, PowerStep step)
    {
        var task = service.RunAsync(_rule, step, TestContext.Current.CancellationToken);
        await WaitForTimers(1);
        _time.Advance(TimeSpan.FromMilliseconds(250));
        await task;
    }

    /// <summary>Ждёт, пока в тестовых часах появится нужное число ожидающих таймеров (ожидание ставится в другом потоке).</summary>
    private async Task WaitForTimers(int count)
    {
        for (var i = 0; i < 500 && _time.PendingTimerCount < count; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.True(_time.PendingTimerCount >= count);
    }

    /// <summary>Сон «сейчас» выполняется после паузы 250 мс, без отсчёта.</summary>
    [Fact]
    public async Task Run_SleepNow_WaitsThenSleeps()
    {
        var service = CreateService();
        var task = service.RunAsync(_rule, new PowerStep { Action = PowerAction.Sleep }, TestContext.Current.CancellationToken);
        await WaitForTimers(1);

        Assert.Empty(_power.Actions);
        _time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        Assert.Equal(["Sleep"], _power.Actions);
        Assert.Empty(_prompt.Shown);
    }

    /// <summary>Каждое действие вызывает своё системное действие.</summary>
    [Theory]
    [InlineData("Hibernate", "Hibernate")]
    [InlineData("Lock", "Lock")]
    [InlineData("SignOut", "SignOut")]
    public async Task Run_ImmediateActions_CallMatchingPowerAction(string actionName, string expected)
    {
        var action = Enum.Parse<PowerAction>(actionName);
        await RunNow(CreateService(), new PowerStep { Action = action });

        Assert.Equal([expected], _power.Actions);
        Assert.Empty(_prompt.Shown);
    }

    /// <summary>Выключение и перезагрузка «сейчас» идут через отсчёт 10 с.</summary>
    [Theory]
    [InlineData("Shutdown", "Shutdown")]
    [InlineData("Restart", "Restart")]
    public async Task Run_ShutdownOrRestartNow_ShowsTenSecondCountdown(string actionName, string expected)
    {
        var action = Enum.Parse<PowerAction>(actionName);
        await RunNow(CreateService(), new PowerStep { Action = action });

        Assert.Equal(PowerService.CountdownSeconds, Assert.Single(_prompt.Shown).Seconds);
        Assert.Equal([expected], _power.Actions);
    }

    /// <summary>Отсчёт можно отключить в настройках шага.</summary>
    [Fact]
    public async Task Run_CountdownDisabled_ActsWithoutPrompt()
    {
        await RunNow(CreateService(), new PowerStep { Action = PowerAction.Shutdown, Countdown = false });

        Assert.Empty(_prompt.Shown);
        Assert.Equal(["Shutdown"], _power.Actions);
    }

    /// <summary>Отмена отсчёта прерывает цепочку и ничего не выключает.</summary>
    [Fact]
    public async Task Run_CountdownCancelled_ThrowsAndDoesNothing()
    {
        _prompt.Answer = false;
        var service = CreateService();

        await Assert.ThrowsAsync<StepCancelledException>(
            () => service.RunAsync(_rule, new PowerStep { Action = PowerAction.Restart }, TestContext.Current.CancellationToken));

        Assert.Empty(_power.Actions);
    }

    /// <summary>Гибернация при выключенной гибернации: уведомление и отказ.</summary>
    [Fact]
    public async Task Run_HibernateDisabled_NotifiesAndThrows()
    {
        _power.IsHibernateEnabled = false;
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunAsync(_rule, new PowerStep { Action = PowerAction.Hibernate }, TestContext.Current.CancellationToken));

        Assert.Contains("Гибернация выключена", Assert.Single(_notifier.Messages), StringComparison.Ordinal);
        Assert.Empty(_power.Actions);
    }

    /// <summary>Отказ системы: уведомление и исключение.</summary>
    [Fact]
    public async Task Run_SystemRefuses_NotifiesAndThrows()
    {
        _power.Succeeds = false;
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await RunNow(service, new PowerStep { Action = PowerAction.Sleep }));

        Assert.Contains("Не удалось", Assert.Single(_notifier.Messages), StringComparison.Ordinal);
    }

    /// <summary>При установке таймера уведомление называет действие, задержку и точное время срабатывания.</summary>
    [Fact]
    public async Task Run_Timer_NotifiesWithDueTime()
    {
        var service = CreateService();
        var due = TimeZoneInfo.ConvertTime(_time.GetUtcNow().AddMinutes(2), _time.LocalTimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        await service.RunAsync(_rule, new PowerStep { Action = PowerAction.Lock, AfterMinutes = 2 }, TestContext.Current.CancellationToken);

        var message = Assert.Single(_notifier.Messages);
        Assert.Contains("блокировка через 2 мин, в " + due, message, StringComparison.Ordinal);
    }

    /// <summary>Таймер возвращает управление сразу, показывает остаток и срабатывает после отсчёта за минуту.</summary>
    [Fact]
    public async Task Run_Timer_FiresAfterWarningCountdown()
    {
        var service = CreateService();
        var changes = 0;
        service.Changed += () => Interlocked.Increment(ref changes);

        await service.RunAsync(_rule, new PowerStep { Action = PowerAction.Sleep, AfterMinutes = 3 }, TestContext.Current.CancellationToken);

        Assert.Equal("Сон в 00:03 (через 3 мин)", service.TimerText);
        Assert.Empty(_power.Actions);

        // До предупреждения: ждём 2 минуты, остаток в подсказке уменьшается.
        await WaitForTimers(1);
        _time.Advance(TimeSpan.FromMinutes(1));
        await WaitForTimers(1);
        Assert.Equal("Сон в 00:03 (через 2 мин)", service.TimerText);
        _time.Advance(TimeSpan.FromMinutes(1));

        // За минуту до срабатывания показан отсчёт; после него и паузы 250 мс — сон.
        for (var i = 0; i < 500 && _prompt.Shown.Count == 0; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(60, Assert.Single(_prompt.Shown).Seconds);
        await WaitForTimers(1);
        _time.Advance(TimeSpan.FromMilliseconds(250));
        for (var i = 0; i < 500 && _power.Actions.Count == 0; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(["Sleep"], _power.Actions);
        Assert.Null(service.TimerText);
        Assert.True(changes > 0);
    }

    /// <summary>Новый таймер заменяет старый: сработает только он.</summary>
    [Fact]
    public async Task Run_SecondTimer_ReplacesFirst()
    {
        var service = CreateService();

        await service.RunAsync(_rule, new PowerStep { Action = PowerAction.Sleep, AfterMinutes = 5 }, TestContext.Current.CancellationToken);
        await service.RunAsync(_rule, new PowerStep { Action = PowerAction.Lock, AfterMinutes = 7 }, TestContext.Current.CancellationToken);

        Assert.Equal("Блокировка в 00:07 (через 7 мин)", service.TimerText);
        Assert.Contains(_log.Entries, e => e.Contains("replaced", StringComparison.Ordinal));
    }

    /// <summary>Отмена снимает таймер; действие не выполняется, повторная отмена ничего не делает.</summary>
    [Fact]
    public async Task CancelTimer_ActiveTimer_ClearsIt()
    {
        var service = CreateService();
        await service.RunAsync(_rule, new PowerStep { Action = PowerAction.Sleep, AfterMinutes = 5 }, TestContext.Current.CancellationToken);

        Assert.True(service.CancelTimer());
        Assert.False(service.CancelTimer());

        Assert.Null(service.TimerText);
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Empty(_power.Actions);
    }

    /// <summary>Отмена кнопкой в окне отсчёта снимает таймер без действия.</summary>
    [Fact]
    public async Task Run_TimerCountdownCancelledByUser_ClearsTimer()
    {
        _prompt.Answer = false;
        var service = CreateService();

        await service.RunAsync(_rule, new PowerStep { Action = PowerAction.Sleep, AfterMinutes = 1 }, TestContext.Current.CancellationToken);

        for (var i = 0; i < 500 && service.TimerText is not null; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Null(service.TimerText);
        Assert.Empty(_power.Actions);
    }
}
