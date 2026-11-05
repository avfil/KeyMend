using System.Collections.Concurrent;
using KeyMend.Storage;
using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SettingsWatcher"/>: применение правок, отказ от ошибочного файла, повтор при занятом файле.</summary>
public sealed class SettingsWatcherTests : IDisposable
{
    /// <summary>Уведомления-заглушка: запоминает показанное.</summary>
    private sealed class FakeNotifier : INotifier
    {
        /// <summary>Показанные уведомления вида «уровень: текст».</summary>
        public ConcurrentQueue<string> Shown { get; } = new();

        /// <inheritdoc />
        public void Notify(string title, string message, NotifyLevel level = NotifyLevel.Info) => Shown.Enqueue($"{level}: {message}");
    }

    /// <summary>Часы теста.</summary>
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Уведомления теста.</summary>
    private readonly FakeNotifier _notifier = new();

    /// <summary>Применённые настройки.</summary>
    private readonly ConcurrentQueue<AppSettings> _applied = new();

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Очередь ответов чтения файла: каждый вызов берёт следующий, последний повторяется.</summary>
    private readonly Queue<SettingsParseResult> _reads = new();

    /// <summary>Наблюдатель под тестом.</summary>
    private readonly SettingsWatcher _watcher;

    /// <summary>Создаёт наблюдатель на ручных часах без слежения за файлом.</summary>
    public SettingsWatcherTests()
    {
        _watcher = new SettingsWatcher(NextRead, _applied.Enqueue, _notifier, _time, _log);
    }

    /// <summary>Останавливает наблюдатель.</summary>
    public void Dispose() => _watcher.Dispose();

    /// <summary>Корректный файл применяется.</summary>
    [Fact]
    public async Task Reload_ValidFile_AppliesSettings()
    {
        _reads.Enqueue(new SettingsParseResult(new AppSettings { WordBufferLimit = 300 }, null, []));

        await ReloadAsync();

        Assert.Equal(300, Assert.Single(_applied).WordBufferLimit);
        Assert.Empty(_notifier.Shown);
    }

    /// <summary>Ошибочный файл не применяется, приходит уведомление с описанием.</summary>
    [Fact]
    public async Task Reload_InvalidFile_KeepsOldSettingsAndNotifies()
    {
        _reads.Enqueue(new SettingsParseResult(null, "line 3: bad", []));

        await ReloadAsync();

        Assert.Empty(_applied);
        Assert.Contains("Error: settings.json не применён, работают прежние настройки. line 3: bad", _notifier.Shown);
    }

    /// <summary>Пропущенные правила применяются с предупреждением.</summary>
    [Fact]
    public async Task Reload_WithSkippedRules_AppliesAndWarns()
    {
        _reads.Enqueue(new SettingsParseResult(new AppSettings(), null, ["rule #1 skipped: x"]));

        await ReloadAsync();

        Assert.Single(_applied);
        Assert.Contains(_notifier.Shown, s => s.StartsWith("Warning:", StringComparison.Ordinal));
    }

    /// <summary>Занятый редактором файл читается повторно, а не считается ошибкой.</summary>
    [Fact]
    public async Task Reload_FileBusy_RetriesAndApplies()
    {
        _reads.Enqueue(new SettingsParseResult(null, SettingsStore.ReadFailedMessage, []));
        _reads.Enqueue(new SettingsParseResult(new AppSettings { WordBufferLimit = 250 }, null, []));

        await ReloadAsync(extraDelays: 1);

        Assert.Equal(250, Assert.Single(_applied).WordBufferLimit);
        Assert.Empty(_notifier.Shown);
    }

    /// <summary>Если файл так и не читается, пользователю сообщается ошибка.</summary>
    [Fact]
    public async Task Reload_FileStaysBusy_NotifiesAfterRetries()
    {
        _reads.Enqueue(new SettingsParseResult(null, SettingsStore.ReadFailedMessage, []));

        await ReloadAsync(extraDelays: SettingsWatcher.ReadAttempts - 1);

        Assert.Empty(_applied);
        Assert.Single(_notifier.Shown);
    }

    /// <summary>Следующее чтение; последний ответ повторяется.</summary>
    private SettingsParseResult NextRead()
    {
        lock (_reads)
        {
            return _reads.Count > 1 ? _reads.Dequeue() : _reads.Peek();
        }
    }

    /// <summary>Запрашивает перечитку и проводит её через паузы, двигая часы.</summary>
    /// <param name="extraDelays">Сколько дополнительных пауз (повторные чтения) пройти после первой.</param>
    private async Task ReloadAsync(int extraDelays = 0)
    {
        _watcher.RequestReload();
        for (var i = 0; i <= extraDelays; i++)
        {
            await WaitForAsync(() => _time.PendingTimerCount == 1);
            _time.Advance(SettingsWatcher.DefaultDebounce);
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await Task.Delay(50, TestContext.Current.CancellationToken);
    }

    /// <summary>Ждёт выполнения условия не дольше пяти секунд.</summary>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var until = Environment.TickCount64 + 5000;
        while (!condition())
        {
            Assert.True(Environment.TickCount64 < until, "condition was not met in time");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
