namespace KeyMend.Input;

/// <summary>
/// Опрашивает физическое состояние модификаторов каждые 10 мс, не блокируя поток. Время берёт из <see cref="TimeProvider"/>,
/// поэтому тестируется на ручных часах.
/// </summary>
/// <param name="isPhysicallyDown">Проверка физического состояния клавиши (<c>GetAsyncKeyState</c>).</param>
/// <param name="time">Источник времени и таймеров.</param>
internal sealed class ModifierWaiter(Func<VirtualKey, bool> isPhysicallyDown, TimeProvider time) : IModifierWaiter
{
    /// <summary>Сколько ждать отпускания модификаторов (SPEC 3.2).</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    /// <summary>Как часто проверять состояние клавиш.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Модификаторы, которые должны быть отпущены.</summary>
    private static readonly VirtualKey[] _modifiers =
    [
        VirtualKey.LShift, VirtualKey.RShift, VirtualKey.LControl, VirtualKey.RControl,
        VirtualKey.LMenu, VirtualKey.RMenu, VirtualKey.LWin, VirtualKey.RWin,
    ];

    /// <inheritdoc />
    public async Task<bool> WaitReleasedAsync(bool ignoreShift, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow() + Timeout;
        while (AnyDown(ignoreShift))
        {
            if (time.GetUtcNow() >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, time, cancellationToken);
        }

        return true;
    }

    /// <summary>Зажат ли хотя бы один из ожидаемых модификаторов.</summary>
    /// <param name="ignoreShift">Не учитывать Shift.</param>
    /// <returns><c>true</c>, если зажат.</returns>
    private bool AnyDown(bool ignoreShift)
    {
        foreach (var key in _modifiers)
        {
            if (!(ignoreShift && key is VirtualKey.LShift or VirtualKey.RShift) && isPhysicallyDown(key))
            {
                return true;
            }
        }

        return false;
    }
}
