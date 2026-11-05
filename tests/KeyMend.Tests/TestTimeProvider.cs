namespace KeyMend.Tests;

/// <summary>
/// Управляемые часы для тестов: местное время совпадает с UTC, время и таймеры двигаются только вручную
/// (<see cref="Advance"/>). Подходит для <c>Task.Delay(..., timeProvider)</c>.
/// </summary>
/// <param name="start">Начальный момент.</param>
internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    /// <summary>Таймеры, созданные через <see cref="CreateTimer"/> и ещё не освобождённые.</summary>
    private readonly List<FakeTimer> _timers = [];

    /// <summary>Блокировка списка таймеров: тест и тестируемый код работают в разных потоках.</summary>
    private readonly Lock _lock = new();

    /// <summary>Текущий момент часов.</summary>
    private DateTimeOffset _now = start;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>Сколько таймеров сейчас ждёт срабатывания.</summary>
    public int PendingTimerCount
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count(t => t.Due is not null);
            }
        }
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_lock)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Сдвигает часы вперёд и запускает созревшие таймеры в порядке сроков.</summary>
    /// <param name="delta">На сколько сдвинуть.</param>
    public void Advance(TimeSpan delta)
    {
        lock (_lock)
        {
            _now += delta;
        }

        while (true)
        {
            FakeTimer? next;
            lock (_lock)
            {
                next = _timers.Where(t => t.Due is { } due && due <= _now).OrderBy(t => t.Due).FirstOrDefault();
            }

            if (next is null)
            {
                return;
            }

            next.Fire();
        }
    }

    /// <summary>Таймер на ручных часах.</summary>
    private sealed class FakeTimer(TestTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        /// <summary>Период повтора; ноль или бесконечность — одноразовый.</summary>
        private TimeSpan _period;

        /// <summary>Момент ближайшего срабатывания или <c>null</c>.</summary>
        public DateTimeOffset? Due { get; private set; }

        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._lock)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
                _period = period;
            }

            return true;
        }

        /// <summary>Запускает обратный вызов и переносит или снимает срок.</summary>
        public void Fire()
        {
            lock (owner._lock)
            {
                Due = _period > TimeSpan.Zero && _period != Timeout.InfiniteTimeSpan ? Due + _period : null;
            }

            callback(state);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (owner._lock)
            {
                Due = null;
                owner._timers.Remove(this);
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
