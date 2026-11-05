using KeyMend.Actions;
using KeyMend.Layouts;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="StepExecutor"/>: выбор режима переключения языка и служебные шаги.</summary>
public sealed class StepExecutorTests
{
    /// <summary>Сервис раскладок-заглушка: запоминает запросы.</summary>
    private sealed class FakeLayouts : ILayoutService
    {
        /// <summary>Запросы вида «pair 1049/1033» или «to 1033».</summary>
        public List<string> Calls { get; } = [];

        /// <summary>Что вернуть на запрос переключения; <c>null</c> — переключать некуда.</summary>
        public LayoutInfo? Result { get; set; } = new(0x04090409, "EN");

        /// <inheritdoc />
        public LayoutInfo? GetCurrent() => new(0x04190419, "RU");

        /// <inheritdoc />
        public LayoutSnapshot? Snapshot() => new(1, new LayoutInfo(0x04190419, "RU"));

        /// <inheritdoc />
        public LayoutInfo? SwitchPair(int languageA, int languageB)
        {
            Calls.Add($"pair {languageA}/{languageB}");
            return Result;
        }

        /// <inheritdoc />
        public LayoutInfo? SwitchTo(int languageId)
        {
            Calls.Add($"to {languageId}");
            return Result;
        }

        /// <inheritdoc />
        public LayoutPairResolution? ResolvePair(int languageA, int languageB) => null;

        /// <inheritdoc />
        public LayoutInfo? FindInstalled(int languageId) => null;
    }

    /// <summary>Получатель сообщений о своей смене раскладки: запоминает коды.</summary>
    private sealed class FakeNotifier : ILayoutNotifier
    {
        /// <summary>Коды языков, о которых сообщили.</summary>
        public List<string> Notified { get; } = [];

        /// <inheritdoc />
        public Task OnOwnSwitchAsync(LayoutInfo layout, CancellationToken cancellationToken)
        {
            Notified.Add(layout.Code);
            return Task.CompletedTask;
        }
    }

    /// <summary>Действия приложения-заглушка.</summary>
    private sealed class FakeCommands : IAppCommands
    {
        /// <summary>Что вызывали.</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc />
        public void TogglePause() => Calls.Add("pause");

        /// <inheritdoc />
        public void OpenSettings() => Calls.Add("settings");
    }

    /// <summary>Исправление-заглушка: запоминает вызовы.</summary>
    private sealed class FakeCorrector : ITextCorrector
    {
        /// <summary>Шаги, с которыми вызывали исправление.</summary>
        public List<CorrectScope> Calls { get; } = [];

        /// <inheritdoc />
        public Task CorrectAsync(CorrectStep step, CancellationToken cancellationToken)
        {
            Calls.Add(step.Scope);
            return Task.CompletedTask;
        }
    }

    /// <summary>Питание-заглушка: запоминает действия и отмены таймера.</summary>
    private sealed class FakePower : IPowerService
    {
        /// <summary>Действия шагов «Питание» и отмены.</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc />
        public event Action? Changed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public string? TimerText => null;

        /// <inheritdoc />
        public Task RunAsync(Rule rule, PowerStep step, CancellationToken cancellationToken)
        {
            Calls.Add(step.Action.ToString());
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public bool CancelTimer()
        {
            Calls.Add("cancel");
            return true;
        }
    }

    /// <summary>Запуск программ-заглушка: запоминает файлы запущенных шагов.</summary>
    private sealed class FakeLaunchRunner : ILaunchStepRunner
    {
        /// <summary>Файлы шагов, которые запускали.</summary>
        public List<string> Files { get; } = [];

        /// <inheritdoc />
        public Task LaunchAsync(Rule rule, LaunchStep step, CancellationToken cancellationToken)
        {
            Files.Add(step.File);
            return Task.CompletedTask;
        }
    }

    /// <summary>Шаги ввода-заглушка: запоминает, какие шаги вызывали.</summary>
    private sealed class FakeInputSteps : IInputStepRunner
    {
        /// <summary>Вызовы вида «insert 3» или «press».</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc />
        public Task InsertAsync(Rule rule, InsertTextStep step, CancellationToken cancellationToken)
        {
            Calls.Add($"insert {step.Erase}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task PressAsync(Rule rule, PressStep step, CancellationToken cancellationToken)
        {
            Calls.Add("press " + step.Keys.Key);
            return Task.CompletedTask;
        }
    }

    /// <summary>Шаги ввода под тестом.</summary>
    private readonly FakeInputSteps _inputSteps = new();

    /// <summary>Сервис раскладок под тестом.</summary>
    private readonly FakeLayouts _layouts = new();

    /// <summary>Получатель сообщений о своей смене под тестом.</summary>
    private readonly FakeNotifier _notifier = new();

    /// <summary>Действия приложения под тестом.</summary>
    private readonly FakeCommands _commands = new();

    /// <summary>Исправление под тестом.</summary>
    private readonly FakeCorrector _corrector = new();

    /// <summary>Питание-заглушка: запоминает вызовы.</summary>
    private readonly FakePower _power = new();

    /// <summary>Запуск программ-заглушка: запоминает вызовы.</summary>
    private readonly FakeLaunchRunner _launcher = new();

    /// <summary>Управляемое время теста.</summary>
    private readonly TestTimeProvider _time = new(DateTimeOffset.UnixEpoch);

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Создаёт правило с заданным шагом.</summary>
    private static Rule RuleOf(Step step) => new()
    {
        Name = "r",
        Trigger = new PressTrigger { Key = Input.VirtualKey.Space },
        Steps = [step],
    };

    /// <summary>Выполняет шаг на исполнителе под тестом.</summary>
    private Task Run(Step step) =>
        new StepExecutor(_layouts, _notifier, _commands, _corrector, _inputSteps, _launcher, _power, _time, _log)
            .ExecuteAsync(RuleOf(step), step, TestContext.Current.CancellationToken);

    /// <summary>Режим «пара» вызывает переключение между двумя языками.</summary>
    [Fact]
    public async Task Execute_PairMode_SwitchesBetweenPair()
    {
        await Run(new SwitchLayoutStep());

        Assert.Equal(["pair 1049/1033"], _layouts.Calls);
        Assert.Contains("INFO layout switched RU -> EN", _log.Entries);
        Assert.Equal(["EN"], _notifier.Notified);
    }

    /// <summary>Режим «конкретная» включает один язык.</summary>
    [Fact]
    public async Task Execute_SpecificMode_SwitchesToLanguage()
    {
        await Run(new SwitchLayoutStep { Mode = SwitchMode.Specific, Layout = "en-US" });

        Assert.Equal(["to 1033"], _layouts.Calls);
    }

    /// <summary>Неверный тег языка не вызывает переключения и пишется в лог.</summary>
    [Fact]
    public async Task Execute_InvalidTag_LogsAndDoesNotSwitch()
    {
        await Run(new SwitchLayoutStep { Mode = SwitchMode.Specific, Layout = "nonsense" });
        await Run(new SwitchLayoutStep { Pair = ["ru-RU"] });

        Assert.Empty(_layouts.Calls);
        Assert.Equal(2, _log.Entries.Count(e => e.StartsWith("ERROR rule 'r'", StringComparison.Ordinal)));
    }

    /// <summary>Если переключать некуда, это отмечается в логе без ошибки.</summary>
    [Fact]
    public async Task Execute_NothingToSwitch_LogsSkipped()
    {
        _layouts.Result = null;

        await Run(new SwitchLayoutStep());

        Assert.Contains("INFO layout switch skipped", _log.Entries);
        Assert.Empty(_notifier.Notified);
    }

    /// <summary>Служебные шаги вызывают действия приложения.</summary>
    [Fact]
    public async Task Execute_ServiceSteps_CallAppCommands()
    {
        await Run(new ServiceStep { Action = ServiceAction.TogglePause });
        await Run(new ServiceStep { Action = ServiceAction.OpenSettings });

        Assert.Equal(["pause", "settings"], _commands.Calls);
    }

    /// <summary>Оба вида исправления идут исполнителю исправлений.</summary>
    [Fact]
    public async Task Execute_CorrectSteps_CallCorrector()
    {
        await Run(new CorrectStep { Scope = CorrectScope.Selection });
        await Run(new CorrectStep { Scope = CorrectScope.Line });

        Assert.Equal([CorrectScope.Selection, CorrectScope.Line], _corrector.Calls);
    }

    /// <summary>«Вставить текст» и «Нажать» идут исполнителю шагов ввода.</summary>
    [Fact]
    public async Task Execute_InsertAndPressSteps_CallInputSteps()
    {
        Assert.True(Input.KeyChordParser.TryParse("Ctrl+Enter", out var chord));

        await Run(new InsertTextStep { Text = "x", Erase = 3 });
        await Run(new PressStep { Keys = chord });

        Assert.Equal(["insert 3", "press Return"], _inputSteps.Calls);
    }

    /// <summary>«Запустить программу» идёт исполнителю запуска.</summary>
    [Fact]
    public async Task Execute_LaunchStep_CallsLauncher()
    {
        await Run(new LaunchStep { File = "tool.exe" });

        Assert.Equal(["tool.exe"], _launcher.Files);
    }

    /// <summary>«Пауза» ждёт заданное время и не раньше.</summary>
    [Fact]
    public async Task Execute_DelayStep_WaitsGivenTime()
    {
        var task = Run(new DelayStep { Ms = 500 });

        _time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.False(task.IsCompleted);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await task;
    }

    /// <summary>«Питание» идёт сервису питания, а служебный шаг отменяет таймер.</summary>
    [Fact]
    public async Task Execute_PowerAndCancelTimerSteps_CallPowerService()
    {
        await Run(new PowerStep { Action = PowerAction.Sleep });
        await Run(new ServiceStep { Action = ServiceAction.CancelPowerTimer });

        Assert.Equal(["Sleep", "cancel"], _power.Calls);
    }
}
