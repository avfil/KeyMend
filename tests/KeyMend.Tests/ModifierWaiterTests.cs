using KeyMend.Input;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="ModifierWaiter"/>: ожидание отпускания модификаторов на ручных часах.</summary>
public sealed class ModifierWaiterTests
{
    /// <summary>Клавиши, «физически» зажатые сейчас.</summary>
    private readonly HashSet<VirtualKey> _held = [];

    /// <summary>Ручные часы.</summary>
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Ожидание под тестом.</summary>
    private readonly ModifierWaiter _waiter;

    /// <summary>Создаёт ожидание на ручных часах.</summary>
    public ModifierWaiterTests() => _waiter = new ModifierWaiter(_held.Contains, _time);

    /// <summary>Ждёт, пока ожидание создаст таймер опроса.</summary>
    private async Task WaitForPollTimerAsync()
    {
        while (_time.PendingTimerCount == 0)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Модификаторы уже отпущены: ответ сразу.</summary>
    [Fact]
    public async Task WaitReleasedAsync_NothingHeld_ReturnsTrueImmediately()
    {
        Assert.True(await _waiter.WaitReleasedAsync(ignoreShift: false, TestContext.Current.CancellationToken));
    }

    /// <summary>Зажатый Shift при <c>ignoreShift</c> не задерживает ответ, а Ctrl задерживает.</summary>
    [Fact]
    public async Task WaitReleasedAsync_ShiftHeldAndIgnored_ReturnsTrueImmediately()
    {
        _held.Add(VirtualKey.LShift);

        Assert.True(await _waiter.WaitReleasedAsync(ignoreShift: true, TestContext.Current.CancellationToken));
        _held.Add(VirtualKey.LControl);
        var task = _waiter.WaitReleasedAsync(ignoreShift: true, TestContext.Current.CancellationToken);
        await WaitForPollTimerAsync();
        Assert.False(task.IsCompleted);
        _held.Remove(VirtualKey.LControl);
        _time.Advance(TimeSpan.FromMilliseconds(10));
        Assert.True(await task);
    }

    /// <summary>Модификатор отпущен во время ожидания: ответ «да».</summary>
    [Fact]
    public async Task WaitReleasedAsync_ReleasedWhileWaiting_ReturnsTrue()
    {
        _held.Add(VirtualKey.RControl);
        var task = _waiter.WaitReleasedAsync(ignoreShift: false, TestContext.Current.CancellationToken);
        await WaitForPollTimerAsync();

        _held.Remove(VirtualKey.RControl);
        _time.Advance(ModifierWaiter.PollInterval);

        Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    /// <summary>Модификатор не отпущен за секунду: ответ «нет».</summary>
    [Fact]
    public async Task WaitReleasedAsync_HeldLongerThanTimeout_ReturnsFalse()
    {
        _held.Add(VirtualKey.LShift);
        var task = _waiter.WaitReleasedAsync(ignoreShift: false, TestContext.Current.CancellationToken);

        for (var i = 0; i < 150 && !task.IsCompleted; i++)
        {
            await WaitForPollTimerAsync();
            _time.Advance(ModifierWaiter.PollInterval);
        }

        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    /// <summary>Отмена прерывает ожидание.</summary>
    [Fact]
    public async Task WaitReleasedAsync_Cancelled_Throws()
    {
        _held.Add(VirtualKey.LMenu);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var task = _waiter.WaitReleasedAsync(ignoreShift: false, cts.Token);
        await WaitForPollTimerAsync();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}
