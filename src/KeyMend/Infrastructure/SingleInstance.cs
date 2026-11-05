namespace KeyMend.Infrastructure;

/// <summary>Итог попытки стать единственным экземпляром.</summary>
internal enum AcquireResult
{
    /// <summary>Это первый экземпляр.</summary>
    First,

    /// <summary>Уже работает другой экземпляр того же уровня прав; ему подан сигнал активации.</summary>
    AlreadyRunning,

    /// <summary>Уже работает экземпляр с правами администратора: обычный процесс не может открыть его мьютекс (2.6).</summary>
    ElevatedInstanceRunning,
}

/// <summary>
/// Защита от второго экземпляра (SPEC F11). Первый экземпляр владеет именованным мьютексом и ждёт именованное событие;
/// второй экземпляр подаёт это событие и завершается. Экземпляр с правами администратора создаёт мьютекс, недоступный
/// обычному процессу: тот получает отказ доступа и сообщает об этом (2.6).
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    /// <summary>Имя мьютекса по умолчанию, который занимает первый экземпляр (область — текущий сеанс).</summary>
    internal const string DefaultMutexName = @"Local\KeyMend.SingleInstance";

    /// <summary>Имя события по умолчанию, которым второй экземпляр просит первый показать окно настроек.</summary>
    internal const string DefaultActivateEventName = @"Local\KeyMend.Activate";

    /// <summary>Мьютекс, удерживаемый всё время работы первого экземпляра.</summary>
    private readonly Mutex _mutex;

    /// <summary>Событие активации, которое подаёт второй экземпляр.</summary>
    private readonly EventWaitHandle _activateEvent;

    /// <summary>Регистрация ожидания события в пуле потоков; <c>null</c>, если обработчик не задан.</summary>
    private readonly RegisteredWaitHandle? _registration;

    /// <summary>Создаёт защиту и подписывает <paramref name="onActivate"/> на событие активации.</summary>
    private SingleInstance(Mutex mutex, EventWaitHandle activateEvent, Action? onActivate)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
        if (onActivate is not null)
        {
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
        }
    }

    /// <summary>
    /// Возвращает защиту, если это первый экземпляр; иначе подаёт сигнал уже работающему и возвращает <c>null</c>.
    /// <paramref name="onActivate"/> вызывается в потоке пула потоков.
    /// </summary>
    /// <param name="onActivate">Что делать, когда второй экземпляр просит активацию.</param>
    /// <param name="result">Итог попытки.</param>
    /// <param name="waitForPrevious">
    /// Сколько ждать, пока предыдущий экземпляр освободит мьютекс. Нужно администраторскому экземпляру, который стартует
    /// сразу после обычного, завершающегося при включении режима администратора (2.6).
    /// </param>
    /// <param name="mutexName">Имя мьютекса; другое имя нужно тестам.</param>
    /// <param name="activateEventName">Имя события; другое имя нужно тестам.</param>
    public static SingleInstance? TryAcquire(
        Action onActivate,
        out AcquireResult result,
        TimeSpan waitForPrevious = default,
        string? mutexName = null,
        string? activateEventName = null)
    {
        Mutex? toDispose = null;
        try
        {
            Mutex mutex;
            try
            {
                mutex = new Mutex(initiallyOwned: false, mutexName ?? DefaultMutexName);
                toDispose = mutex;
            }
            catch (UnauthorizedAccessException)
            {
                // Мьютекс создан процессом с более высокими правами.
                result = AcquireResult.ElevatedInstanceRunning;
                return null;
            }

            if (!WaitOwnership(mutex, waitForPrevious))
            {
                SignalActivation(activateEventName ?? DefaultActivateEventName);
                result = AcquireResult.AlreadyRunning;
                return null;
            }

            var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activateEventName ?? DefaultActivateEventName);
            var instance = new SingleInstance(mutex, activateEvent, onActivate);

            // Мьютекс и событие теперь принадлежат экземпляру.
            toDispose = null;
            result = AcquireResult.First;
            return instance;
        }
        finally
        {
            toDispose?.Dispose();
        }
    }

    /// <summary>Снимает регистрацию ожидания, освобождает событие и мьютекс. Вызывать нужно в потоке, который занял мьютекс.</summary>
    public void Dispose()
    {
        _registration?.Unregister(null);
        _activateEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    /// <summary>Ждёт владения мьютексом; брошенный (процесс-владелец завершился аварийно) мьютекс тоже считается полученным.</summary>
    private static bool WaitOwnership(Mutex mutex, TimeSpan wait)
    {
        try
        {
            return mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    /// <summary>Подаёт сигнал активации уже работающему экземпляру; если событие недоступно, молча ничего не делает.</summary>
    private static void SignalActivation(string eventName)
    {
        try
        {
            using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            activateEvent.Set();
        }
        catch (UnauthorizedAccessException)
        {
            // Событие принадлежит экземпляру с другим уровнем прав: сигнал подать нельзя.
        }
    }
}
