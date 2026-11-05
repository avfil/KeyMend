using KeyMend.Infrastructure;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SingleInstance"/> на уникальных именах, чтобы не мешать запущенному приложению.</summary>
public sealed class SingleInstanceTests
{
    private readonly string _mutex = @"Local\KeyMendTests.Mutex." + Guid.NewGuid().ToString("N");
    private readonly string _event = @"Local\KeyMendTests.Event." + Guid.NewGuid().ToString("N");

    private SingleInstance? Acquire(Action onActivate, out AcquireResult result, TimeSpan wait = default) =>
        SingleInstance.TryAcquire(onActivate, out result, wait, _mutex, _event);

    /// <summary>Первый вызов становится первым экземпляром.</summary>
    [Fact]
    public void FirstCall_BecomesFirstInstance()
    {
        using var first = Acquire(() => { }, out var result);

        Assert.NotNull(first);
        Assert.Equal(AcquireResult.First, result);
    }

    /// <summary>Второй вызов отклоняется и подаёт сигнал первому.</summary>
    [Fact]
    public void SecondCall_FromAnotherThread_IsRejected_AndSignalsFirst()
    {
        // Мьютекс принадлежит потоку: второй экземпляр моделируем отдельным потоком, как другой процесс.
        using var activated = new ManualResetEventSlim();
        using var first = Acquire(activated.Set, out _);

        AcquireResult secondResult = default;
        SingleInstance? second = null;
        var thread = new Thread(() => second = Acquire(() => { }, out secondResult));
        thread.Start();
        thread.Join();

        Assert.Null(second);
        Assert.Equal(AcquireResult.AlreadyRunning, secondResult);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken), "первый экземпляр не получил сигнал активации");
    }

    /// <summary>После освобождения первого может стартовать новый.</summary>
    [Fact]
    public void AfterFirstIsDisposed_NewInstanceCanStart()
    {
        var first = Acquire(() => { }, out _);
        first!.Dispose();

        using var again = Acquire(() => { }, out var result);

        Assert.NotNull(again);
        Assert.Equal(AcquireResult.First, result);
    }

    /// <summary>Ожидание предыдущего экземпляра успешно, если он освободился вовремя.</summary>
    [Fact]
    public void WaitForPrevious_AcquiresAfterPreviousReleases()
    {
        var firstStarted = new ManualResetEventSlim();
        var releaseFirst = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            using var first = Acquire(() => { }, out _);
            firstStarted.Set();
            releaseFirst.Wait(TestContext.Current.CancellationToken);
        });
        holder.Start();
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        // Предыдущий экземпляр завершится через 300 мс; ожидание в 3 с должно это дождаться.
        _ = Task.Run(async () =>
        {
            await Task.Delay(300, TestContext.Current.CancellationToken);
            releaseFirst.Set();
        }, TestContext.Current.CancellationToken);
        using var second = Acquire(() => { }, out var result, TimeSpan.FromSeconds(3));
        holder.Join();

        Assert.NotNull(second);
        Assert.Equal(AcquireResult.First, result);
    }

    /// <summary>Ожидание заканчивается отказом, если предыдущий не освободился.</summary>
    [Fact]
    public void WaitForPrevious_GivesUpWhenPreviousStays()
    {
        using var first = Acquire(() => { }, out _);

        AcquireResult secondResult = default;
        SingleInstance? second = null;
        var thread = new Thread(() => second = Acquire(() => { }, out secondResult, TimeSpan.FromMilliseconds(300)));
        thread.Start();
        thread.Join();

        Assert.Null(second);
        Assert.Equal(AcquireResult.AlreadyRunning, secondResult);
    }
}
