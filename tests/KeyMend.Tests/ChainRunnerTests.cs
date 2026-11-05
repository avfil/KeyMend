using System.Collections.Concurrent;
using KeyMend.Actions;
using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="ChainRunner"/>: порядок шагов, очередь цепочек, немедленные служебные правила, ошибки, маска.</summary>
public sealed class ChainRunnerTests
{
    /// <summary>Исполнитель-заглушка: записывает шаги, умеет падать и ждать сигнала.</summary>
    private sealed class FakeExecutor : IStepExecutor
    {
        /// <summary>Журнал событий вида «имя правила:шаг».</summary>
        public ConcurrentQueue<string> Events { get; } = new();

        /// <summary>Шаг, на котором нужно бросить исключение, как «имя:номер», или <c>null</c>.</summary>
        public string? FailAt { get; set; }

        /// <summary>Шаг, на котором пользователь «отменяет» действие, как «имя:номер», или <c>null</c>.</summary>
        public string? CancelAt { get; set; }

        /// <summary>Пока не задан результат, шаг переключения языка правила «slow» не завершается.</summary>
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <inheritdoc />
        public async Task ExecuteAsync(Rule rule, Step step, CancellationToken cancellationToken)
        {
            var label = step switch
            {
                SwitchLayoutStep s => "layout" + (s.Layout ?? string.Empty),
                ServiceStep s => s.Action.ToString(),
                _ => "?",
            };
            Events.Enqueue($"{rule.Name}:{label}:start");
            if (rule.Name == "slow")
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            if (CancelAt == $"{rule.Name}:{label}")
            {
                throw new StepCancelledException();
            }

            if (FailAt == $"{rule.Name}:{label}")
            {
                throw new InvalidOperationException("boom");
            }

            Events.Enqueue($"{rule.Name}:{label}:done");
        }
    }

    /// <summary>Отправка ввода-заглушка: запоминает маски.</summary>
    private sealed class FakeSender : IInputSender
    {
        /// <summary>Клавиши, отпускание которых отправлено вместе с маской.</summary>
        public ConcurrentQueue<VirtualKey> Masks { get; } = new();

        /// <inheritdoc />
        public void SendMask(VirtualKey release) => Masks.Enqueue(release);

        /// <inheritdoc />
        public bool SendTyped(int backspaces, string text, NewlineMode newline) => true;

        /// <inheritdoc />
        public bool SendKeys(KeyChord chord) => true;

        /// <inheritdoc />
        public bool SendChord(VirtualKey modifier, VirtualKey key) => true;

        /// <inheritdoc />
        public bool SendKeyRepeat(VirtualKey key, int count, bool shift) => true;
    }

    /// <summary>Записывает сообщения журнала.</summary>
    private readonly TestLog _log = new();

    /// <summary>Исполнитель под тестом.</summary>
    private readonly FakeExecutor _executor = new();

    /// <summary>Отправка ввода под тестом.</summary>
    private readonly FakeSender _sender = new();

    /// <summary>Сколько раз хук просил сверить раскладку.</summary>
    private int _layoutChecks;

    /// <summary>Результаты записи триггера, дошедшие до обработчика.</summary>
    private readonly ConcurrentQueue<RecordedGesture> _gestures = new();

    /// <summary>Создаёт правило из шагов переключения языка; отличать их можно по <c>Layout</c>.</summary>
    private static Rule Chain(string name, params string[] layouts) => new()
    {
        Name = name,
        Trigger = new PressTrigger { Key = VirtualKey.Space },
        Steps = layouts.Select(l => (Step)new SwitchLayoutStep { Mode = SwitchMode.Specific, Layout = l }).ToArray(),
    };

    /// <summary>Создаёт служебное правило паузы.</summary>
    private static Rule Pause(string name = "pause") => new()
    {
        Name = name,
        Trigger = new PressTrigger { Key = VirtualKey.Space },
        Steps = [new ServiceStep { Action = ServiceAction.TogglePause }],
    };

    /// <summary>Запускает раннер на заглушках.</summary>
    private ChainRunner Start() => new(_executor, _sender, () => Interlocked.Increment(ref _layoutChecks), _gestures.Enqueue, _log);

    /// <summary>Закрывает канал и ждёт, пока всё принятое будет обработано.</summary>
    private static async Task FinishAsync(ChainRunner runner)
    {
        runner.Writer.TryComplete();
        await runner.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>Шаги цепочки идут по порядку.</summary>
    [Fact]
    public async Task RuleFired_ChainWithSteps_RunsStepsInOrder()
    {
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("a", "1", "2", "3")));
        await FinishAsync(runner);

        Assert.Equal(
            ["a:layout1:start", "a:layout1:done", "a:layout2:start", "a:layout2:done", "a:layout3:start", "a:layout3:done"],
            _executor.Events);
    }

    /// <summary>Вторая цепочка ждёт окончания первой.</summary>
    [Fact]
    public async Task RuleFired_TwoChains_RunOneAfterAnother()
    {
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("a", "1")));
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("b", "1")));
        await FinishAsync(runner);

        Assert.Equal(["a:layout1:start", "a:layout1:done", "b:layout1:start", "b:layout1:done"], _executor.Events);
    }

    /// <summary>Служебное правило выполняется сразу, даже когда обычная цепочка ещё идёт.</summary>
    [Fact]
    public async Task RuleFired_ServiceRule_RunsWhileLongChainIsBusy()
    {
        using var runner = Start();
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("slow", "1")));
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Pause()));

        await WaitForAsync(() => _executor.Events.Contains("pause:TogglePause:done"));
        Assert.DoesNotContain("slow:layout1:done", _executor.Events);

        _executor.Gate.SetResult();
        await FinishAsync(runner);
        Assert.Contains("slow:layout1:done", _executor.Events);
    }

    /// <summary>Ошибка шага прерывает свою цепочку, следующая цепочка выполняется, ошибка попадает в лог.</summary>
    [Fact]
    public async Task RuleFired_StepFails_AbortsThatChainOnly()
    {
        _executor.FailAt = "a:layout2";
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("a", "1", "2", "3")));
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("b", "1")));
        await FinishAsync(runner);

        Assert.DoesNotContain("a:layout3:start", _executor.Events);
        Assert.Contains("b:layout1:done", _executor.Events);
        Assert.Contains(_log.Entries, e => e.StartsWith("ERROR rule 'a' step 2 failed", StringComparison.Ordinal));
    }

    /// <summary>Отмена шага пользователем прекращает цепочку без ошибки в журнале.</summary>
    [Fact]
    public async Task RuleFired_StepCancelledByUser_StopsChainWithoutError()
    {
        _executor.CancelAt = "a:layout2";
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("a", "1", "2", "3")));
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("b", "1")));
        await FinishAsync(runner);

        Assert.DoesNotContain("a:layout3:start", _executor.Events);
        Assert.Contains("b:layout1:done", _executor.Events);
        Assert.DoesNotContain(_log.Entries, e => e.StartsWith("ERROR", StringComparison.Ordinal));
        Assert.Contains(_log.Entries, e => e.Contains("cancelled by the user", StringComparison.Ordinal));
    }

    /// <summary>Маска отправляется вместе с клавишей, отпускание которой подавлено.</summary>
    [Fact]
    public async Task RuleFired_WithMask_SendsMaskWithReleaseKey()
    {
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("a", "1"), SendMask: true, ReleaseKey: VirtualKey.RMenu));
        await FinishAsync(runner);

        Assert.Equal([VirtualKey.RMenu], _sender.Masks);
    }

    /// <summary>Сообщение только с маской не запускает шагов.</summary>
    [Fact]
    public async Task MaskOnly_SendsMaskAndRunsNothing()
    {
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.MaskOnly, null, SendMask: true, ReleaseKey: VirtualKey.LWin));
        await FinishAsync(runner);

        Assert.Equal([VirtualKey.LWin], _sender.Masks);
        Assert.Empty(_executor.Events);
    }

    /// <summary>Просьба хука сверить раскладку доходит до обработчика.</summary>
    [Fact]
    public async Task LayoutCheck_Message_CallsRequestCallback()
    {
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.LayoutCheck));
        await FinishAsync(runner);

        Assert.Equal(1, _layoutChecks);
    }

    /// <summary>Результат записи триггера доходит до обработчика и не запускает цепочек.</summary>
    [Fact]
    public async Task TriggerRecorded_Message_PassesGestureToCallback()
    {
        using var runner = Start();
        var gesture = new RecordedGesture(ModifierSet.RShift, ModifierSet.RCtrl, VirtualKey.None);

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.TriggerRecorded, Gesture: gesture));
        await FinishAsync(runner);

        Assert.Equal([gesture], _gestures);
        Assert.Empty(_executor.Events);
    }

    /// <summary>Ошибка в колбэке хука пишется в лог.</summary>
    [Fact]
    public async Task HookError_Message_IsLogged()
    {
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.HookError));
        await FinishAsync(runner);

        Assert.Contains(_log.Entries, e => e.Contains("hook callback failed", StringComparison.Ordinal));
    }

    /// <summary>Освобождение раннера отменяет шаг, который ждёт.</summary>
    [Fact]
    public async Task Dispose_WhileStepIsWaiting_CancelsIt()
    {
        var runner = Start();
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("slow", "1")));
        await WaitForAsync(() => _executor.Events.Contains("slow:layout1:start"));

        runner.Dispose();

        await runner.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("slow:layout1:done", _executor.Events);
    }

    /// <summary>Создаёт правило исправления ввода.</summary>
    private static Rule Fix(string name) => new()
    {
        Name = name,
        Trigger = new PressTrigger { Key = VirtualKey.Space },
        Steps = [new CorrectStep { Scope = CorrectScope.Selection }],
    };

    /// <summary>Пока исправление идёт, повторные нажатия исправления отбрасываются, а не копятся в очереди.</summary>
    [Fact]
    public async Task RuleFired_CorrectionWhileCorrectionRuns_IsIgnored()
    {
        using var runner = Start();
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Fix("slow")));
        await WaitForAsync(() => _executor.Events.Contains("slow:?:start"));

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Fix("again")));
        await WaitForAsync(() => _log.Entries.Contains("INFO correction ignored: busy"));

        _executor.Gate.SetResult();
        await FinishAsync(runner);
        Assert.DoesNotContain("again:?:start", _executor.Events);
        Assert.Contains("slow:?:done", _executor.Events);
    }

    /// <summary>Исправление, ждущее очереди за другой цепочкой, тоже блокирует повторные нажатия.</summary>
    [Fact]
    public async Task RuleFired_CorrectionQueuedBehindChain_BlocksSecondCorrection()
    {
        using var runner = Start();
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Chain("slow", "1")));
        await WaitForAsync(() => _executor.Events.Contains("slow:layout1:start"));

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Fix("first")));
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Fix("second")));
        await WaitForAsync(() => _log.Entries.Contains("INFO correction ignored: busy"));

        _executor.Gate.SetResult();
        await FinishAsync(runner);
        Assert.Contains("first:?:done", _executor.Events);
        Assert.DoesNotContain("second:?:start", _executor.Events);
    }

    /// <summary>После окончания исправления следующее нажатие снова работает, в том числе после ошибки шага.</summary>
    [Fact]
    public async Task RuleFired_CorrectionAfterPreviousFinished_Runs()
    {
        _executor.FailAt = "first:?";
        using var runner = Start();

        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Fix("first")));
        await WaitForAsync(() => _log.Entries.Any(e => e.StartsWith("ERROR rule 'first'", StringComparison.Ordinal)));
        runner.Writer.TryWrite(new HookMessage(HookMessageKind.RuleFired, Fix("second")));
        await FinishAsync(runner);

        Assert.Contains("second:?:done", _executor.Events);
        Assert.DoesNotContain("INFO correction ignored: busy", _log.Entries);
    }

    /// <summary>Ждёт выполнения условия не дольше пяти секунд.</summary>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var until = Environment.TickCount64 + 5000;
        while (!condition())
        {
            Assert.True(Environment.TickCount64 < until, "condition was not met in time");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
