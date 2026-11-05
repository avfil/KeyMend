using KeyMend.Actions;
using KeyMend.Infrastructure;
using KeyMend.Rules;
using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="LaunchStepRunner"/>: повторный запуск, ожидание, ошибки и уведомления (SPEC F4.1).</summary>
public sealed class LaunchStepRunnerTests
{
    /// <summary>Процесс-заглушка: завершается вручную.</summary>
    private sealed class FakeProcess : IRunningProcess
    {
        /// <summary>Завершение процесса с кодом.</summary>
        private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public bool HasExited => _exit.Task.IsCompleted;

        /// <summary>Процесс остановлен через <see cref="Kill"/>.</summary>
        public bool Killed { get; private set; }

        /// <summary>Процесс освобождён.</summary>
        public bool Disposed { get; private set; }

        /// <summary>Завершает процесс.</summary>
        /// <param name="code">Код возврата.</param>
        public void Exit(int code) => _exit.TrySetResult(code);

        /// <inheritdoc />
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task.WaitAsync(cancellationToken);

        /// <inheritdoc />
        public void Kill()
        {
            Killed = true;
            _exit.TrySetResult(1);
        }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    /// <summary>Запуск процессов-заглушка.</summary>
    private sealed class FakeLauncher : IProcessLauncher
    {
        /// <summary>Запущенные процессы.</summary>
        public List<FakeProcess> Started { get; } = [];

        /// <summary>Команды запусков.</summary>
        public List<LaunchCommand> Commands { get; } = [];

        /// <summary>Что вернуть из запуска: <c>true</c> — процесс, <c>false</c> — <c>null</c>.</summary>
        public bool ReturnsProcess { get; set; } = true;

        /// <summary>Вызывать ли сбой запуска.</summary>
        public bool Fails { get; set; }

        /// <inheritdoc />
        public IRunningProcess? Start(LaunchCommand command)
        {
            if (Fails)
            {
                throw new InvalidOperationException("boom");
            }

            Commands.Add(command);
            if (!ReturnsProcess)
            {
                return null;
            }

            var process = new FakeProcess();
            Started.Add(process);
            return process;
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

    /// <summary>Запуск процессов под тестом.</summary>
    private readonly FakeLauncher _launcher = new();

    /// <summary>Уведомления под тестом.</summary>
    private readonly FakeNotifier _notifier = new();

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Правило теста.</summary>
    private readonly Rule _rule = new() { Id = "r", Name = "Монитор", Trigger = new PressTrigger { Key = Input.VirtualKey.Space }, Steps = [] };

    /// <summary>Создаёт исполнитель с одним существующим файлом <c>C:\t\a.exe</c>.</summary>
    private LaunchStepRunner CreateRunner() => new(
        new LaunchEnvironment(@"C:\t", @"C:\Windows\System32", @"C:\Program Files", [], f => f == @"C:\t\a.exe", _ => true),
        _launcher, _notifier, _log);

    /// <summary>Шаг теста.</summary>
    private static LaunchStep Step(LaunchRepeat repeat = LaunchRepeat.Ignore, bool wait = false, string args = "") =>
        new() { File = "a.exe", Arguments = args, Repeat = repeat, Wait = wait };

    /// <summary>Запускает шаг.</summary>
    private Task Launch(LaunchStepRunner runner, LaunchStep step) => runner.LaunchAsync(_rule, step, TestContext.Current.CancellationToken);

    /// <summary>Ждёт, пока условие выполнится (фоновое наблюдение за процессом работает в другом потоке).</summary>
    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    /// <summary>Пока предыдущий процесс жив, повторный запуск по умолчанию игнорируется.</summary>
    [Fact]
    public async Task Launch_PreviousRunning_IgnoredByDefault()
    {
        var runner = CreateRunner();

        await Launch(runner, Step());
        await Launch(runner, Step());

        Assert.Single(_launcher.Started);
        Assert.Contains(_log.Entries, e => e.Contains("launch ignored", StringComparison.Ordinal));
    }

    /// <summary>После завершения процесса следующий запуск проходит.</summary>
    [Fact]
    public async Task Launch_PreviousExited_StartsAgain()
    {
        var runner = CreateRunner();
        await Launch(runner, Step());
        _launcher.Started[0].Exit(0);
        await Eventually(() => _launcher.Started[0].Disposed);

        await Launch(runner, Step());

        Assert.Equal(2, _launcher.Started.Count);
        Assert.Empty(_notifier.Messages);
    }

    /// <summary>Режим «ещё одна копия» запускает второй процесс, не трогая первый.</summary>
    [Fact]
    public async Task Launch_AnotherMode_StartsSecondCopy()
    {
        var runner = CreateRunner();

        await Launch(runner, Step(LaunchRepeat.Another));
        await Launch(runner, Step(LaunchRepeat.Another));

        Assert.Equal(2, _launcher.Started.Count);
        Assert.False(_launcher.Started[0].Killed);
    }

    /// <summary>Режим «остановить предыдущий» убивает процесс без уведомления об ошибке и запускает новый.</summary>
    [Fact]
    public async Task Launch_StopPreviousMode_KillsAndRestarts()
    {
        var runner = CreateRunner();
        await Launch(runner, Step(LaunchRepeat.StopPrevious));

        await Launch(runner, Step(LaunchRepeat.StopPrevious));

        Assert.True(_launcher.Started[0].Killed);
        Assert.Equal(2, _launcher.Started.Count);
        await Eventually(() => _launcher.Started[0].Disposed);
        Assert.Empty(_notifier.Messages);
    }

    /// <summary>Один файл с разными аргументами в одном правиле не мешает сам себе.</summary>
    [Fact]
    public async Task Launch_DifferentArguments_AreIndependent()
    {
        var runner = CreateRunner();

        await Launch(runner, Step(args: "/a"));
        await Launch(runner, Step(args: "/b"));

        Assert.Equal(2, _launcher.Started.Count);
    }

    /// <summary>«Ждать завершения» держит цепочку до выхода процесса.</summary>
    [Fact]
    public async Task Launch_Wait_CompletesAfterExit()
    {
        var runner = CreateRunner();

        var task = Launch(runner, Step(wait: true));
        await Eventually(() => _launcher.Started.Count == 1);
        Assert.False(task.IsCompleted);

        _launcher.Started[0].Exit(0);
        await task;
    }

    /// <summary>Ненулевой код возврата даёт уведомление и запись в журнал с кодом.</summary>
    [Fact]
    public async Task Launch_NonZeroExit_NotifiesWithCode()
    {
        var runner = CreateRunner();

        await Launch(runner, Step());
        _launcher.Started[0].Exit(3);

        await Eventually(() => _notifier.Messages.Count == 1);
        Assert.Contains("кодом 3", _notifier.Messages[0], StringComparison.Ordinal);
        Assert.Contains(_log.Entries, e => e.Contains("exited with code 3", StringComparison.Ordinal));
    }

    /// <summary>Несуществующий файл: уведомление и исключение, процесс не запускается.</summary>
    [Fact]
    public async Task Launch_FileMissing_NotifiesAndThrows()
    {
        var runner = CreateRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Launch(runner, new LaunchStep { File = "missing.exe" }));

        Assert.Empty(_launcher.Started);
        Assert.Contains("файл не найден", Assert.Single(_notifier.Messages), StringComparison.Ordinal);
    }

    /// <summary>Сбой запуска процесса: уведомление, исключение прерывает цепочку.</summary>
    [Fact]
    public async Task Launch_StartFails_NotifiesAndThrows()
    {
        _launcher.Fails = true;
        var runner = CreateRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Launch(runner, Step()));

        Assert.Single(_notifier.Messages);
    }

    /// <summary>Если система не вернула процесс, запуск считается выполненным без наблюдения.</summary>
    [Fact]
    public async Task Launch_NoProcessReturned_Succeeds()
    {
        _launcher.ReturnsProcess = false;
        var runner = CreateRunner();

        await Launch(runner, Step(wait: true));

        Assert.Single(_launcher.Commands);
        Assert.Empty(_notifier.Messages);
    }
}
