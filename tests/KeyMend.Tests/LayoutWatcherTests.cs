using System.Collections.Concurrent;
using KeyMend.Layouts;
using KeyMend.Storage;
using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="LayoutChangeDetector"/> и <see cref="LayoutWatcher"/>: подсказка при смене раскладки извне и своей.</summary>
public sealed class LayoutWatcherTests : IDisposable
{
    /// <summary>Раскладка RU.</summary>
    private static readonly LayoutInfo _ru = new(0x04190419, "RU");

    /// <summary>Раскладка EN.</summary>
    private static readonly LayoutInfo _en = new(0x04090409, "EN");

    /// <summary>Сервис раскладок-заглушка с управляемым снимком.</summary>
    private sealed class FakeLayouts : ILayoutService
    {
        /// <summary>Сколько раз читали снимок.</summary>
        private int _snapshotCalls;

        /// <summary>Что вернёт <see cref="Snapshot"/>.</summary>
        public LayoutSnapshot? Current { get; set; }

        /// <summary>Сколько раз читали снимок.</summary>
        public int SnapshotCalls => Volatile.Read(ref _snapshotCalls);

        /// <inheritdoc />
        public LayoutSnapshot? Snapshot()
        {
            Interlocked.Increment(ref _snapshotCalls);
            return Current;
        }

        /// <inheritdoc />
        public LayoutInfo? GetCurrent() => Current?.Layout;

        /// <inheritdoc />
        public LayoutInfo? SwitchPair(int languageA, int languageB) => null;

        /// <inheritdoc />
        public LayoutInfo? SwitchTo(int languageId) => null;

        /// <inheritdoc />
        public LayoutPairResolution? ResolvePair(int languageA, int languageB) => null;

        /// <inheritdoc />
        public LayoutInfo? FindInstalled(int languageId) => null;
    }

    /// <summary>Подсказка-заглушка: запоминает показанные коды.</summary>
    private sealed class FakePresenter : IIndicatorPresenter
    {
        /// <summary>Показанные коды языков.</summary>
        public ConcurrentQueue<string> Shown { get; } = new();

        /// <inheritdoc />
        public Task ShowAsync(string code, CancellationToken cancellationToken)
        {
            Shown.Enqueue(code);
            return Task.CompletedTask;
        }
    }

    /// <summary>Часы теста.</summary>
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Сервис раскладок.</summary>
    private readonly FakeLayouts _layouts = new() { Current = new LayoutSnapshot(1, _ru) };

    /// <summary>Подсказка.</summary>
    private readonly FakePresenter _presenter = new();

    /// <summary>Настройки подсказки.</summary>
    private IndicatorSettings _settings = new();

    /// <summary>Наблюдатель под тестом.</summary>
    private readonly LayoutWatcher _watcher;

    /// <summary>Создаёт наблюдатель на ручных часах.</summary>
    public LayoutWatcherTests()
    {
        _watcher = new LayoutWatcher(_layouts, _presenter, () => _settings, _time, new TestLog());
    }

    /// <summary>Останавливает наблюдатель.</summary>
    public void Dispose() => _watcher.Dispose();

    /// <summary>Детектор: первое наблюдение и смена окна подсказку не вызывают.</summary>
    [Fact]
    public void Observe_FirstAndWindowChange_ReportNoChange()
    {
        var detector = new LayoutChangeDetector();

        Assert.False(detector.Observe(1, 0x0419));
        Assert.False(detector.Observe(2, 0x0409));
        Assert.False(detector.Observe(2, 0x0409));
    }

    /// <summary>Детектор: смена раскладки в том же окне — изменение, повтор — нет.</summary>
    [Fact]
    public void Observe_SameWindowOtherLayout_ReportsChangeOnce()
    {
        var detector = new LayoutChangeDetector();
        detector.Observe(1, 0x0419);

        Assert.True(detector.Observe(1, 0x0409));
        Assert.False(detector.Observe(1, 0x0409));
    }

    /// <summary>Детектор: своя смена не считается внешней.</summary>
    [Fact]
    public void Observe_AfterOwnSwitch_ReportsNoChange()
    {
        var detector = new LayoutChangeDetector();
        detector.Observe(1, 0x0419);
        detector.NoteOwnSwitch(0x0409);

        Assert.False(detector.Observe(1, 0x0409));
    }

    /// <summary>Внешняя смена раскладки в том же окне показывает подсказку.</summary>
    [Fact]
    public async Task Check_LayoutChangedInSameWindow_ShowsIndicator()
    {
        await CheckAsync();
        _layouts.Current = new LayoutSnapshot(1, _en);

        await CheckAsync();

        Assert.Equal(["EN"], _presenter.Shown);
    }

    /// <summary>Первая сверка и переход в другое окно подсказку не показывают.</summary>
    [Fact]
    public async Task Check_FirstCheckAndWindowChange_ShowNothing()
    {
        await CheckAsync();
        _layouts.Current = new LayoutSnapshot(2, _en);
        await CheckAsync();
        await CheckAsync();

        Assert.Empty(_presenter.Shown);
    }

    /// <summary>При «только при переключении через KeyMend» внешняя смена подсказку не показывает.</summary>
    [Fact]
    public async Task Check_OnlyOwnSwitchEnabled_DoesNotShowExternalChange()
    {
        _settings = new IndicatorSettings { OnlyOwnSwitch = true };
        await CheckAsync();
        _layouts.Current = new LayoutSnapshot(1, _en);

        await CheckAsync();
        await CheckAsync();

        Assert.Empty(_presenter.Shown);
    }

    /// <summary>Своя смена показывает подсказку сразу, а следующая сверка её не дублирует.</summary>
    [Fact]
    public async Task OnOwnSwitch_ShowsOnceAndIsNotRepeatedByCheck()
    {
        await CheckAsync();

        await _watcher.OnOwnSwitchAsync(_en, TestContext.Current.CancellationToken);
        _layouts.Current = new LayoutSnapshot(1, _en);
        await CheckAsync();
        await CheckAsync();

        Assert.Equal(["EN"], _presenter.Shown);
    }

    /// <summary>Несколько запросов подряд сливаются в одну сверку.</summary>
    [Fact]
    public async Task RequestCheck_Burst_IsCoalescedIntoOneCheck()
    {
        var before = _layouts.SnapshotCalls;

        _watcher.RequestCheck();
        _watcher.RequestCheck();
        _watcher.RequestCheck();
        await WaitForAsync(() => _time.PendingTimerCount == 1);
        _time.Advance(LayoutWatcher.DefaultCheckDelay);
        await WaitForAsync(() => _layouts.SnapshotCalls > before);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(before + 1, _layouts.SnapshotCalls);
    }

    /// <summary>Выполняет один цикл «запрос — пауза — сверка» и ждёт, пока сверка начнётся.</summary>
    private async Task CheckAsync()
    {
        var before = _layouts.SnapshotCalls;
        _watcher.RequestCheck();
        await WaitForAsync(() => _time.PendingTimerCount == 1);
        _time.Advance(LayoutWatcher.DefaultCheckDelay);
        await WaitForAsync(() => _layouts.SnapshotCalls > before);

        // Сверка выполняется последовательно: пока она не вернулась, следующий запрос не примется, поэтому ждём чуть-чуть.
        await Task.Delay(20, TestContext.Current.CancellationToken);
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
