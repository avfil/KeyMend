using KeyMend.Infrastructure;

namespace KeyMend.Input;

/// <summary>Запись триггера для окна настроек: одна запись за раз, результат приходит в поток UI.</summary>
internal interface ITriggerRecording
{
    /// <summary>Идёт ли запись.</summary>
    bool IsRecording { get; }

    /// <summary>Начинает запись; предыдущая, если шла, молча отменяется.</summary>
    /// <param name="onFinished">Вызывается в потоке UI один раз: с жестом или с отменой (Esc, тайм-аут). При <see cref="Cancel"/> не вызывается.</param>
    void Start(Action<RecordedGesture> onFinished);

    /// <summary>Прерывает запись без вызова обратного вызова.</summary>
    void Cancel();
}

/// <summary>
/// Связывает окно настроек с хуком: просит хук собирать жест вместо правил и возвращает результат в поток UI. Если за
/// <see cref="Timeout"/> жеста нет, запись отменяется сама, чтобы правила не остались выключенными.
/// </summary>
/// <param name="startHook">Просит хук начать запись.</param>
/// <param name="cancelHook">Просит хук прервать запись.</param>
/// <param name="postToUi">Выполняет действие в потоке UI.</param>
/// <param name="time">Часы для тайм-аута.</param>
/// <param name="log">Журнал.</param>
internal sealed class TriggerRecording(
    Action startHook, Action cancelHook, Action<Action> postToUi, TimeProvider time, ILog log) : ITriggerRecording
{
    /// <summary>Сколько ждать жест, прежде чем отменить запись.</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Что вызвать по окончании записи; <c>null</c> — запись не идёт. Трогается только в потоке UI.</summary>
    private Action<RecordedGesture>? _onFinished;

    /// <summary>Таймер тайм-аута.</summary>
    private ITimer? _timer;

    /// <summary>Номер записи: опоздавший результат прежней записи не должен попасть в новую.</summary>
    private int _generation;

    /// <inheritdoc />
    public bool IsRecording => _onFinished is not null;

    /// <inheritdoc />
    public void Start(Action<RecordedGesture> onFinished)
    {
        Cancel();
        _onFinished = onFinished;
        var generation = ++_generation;
        _timer = time.CreateTimer(_ => postToUi(() => OnTimeout(generation)), null, Timeout, System.Threading.Timeout.InfiniteTimeSpan);
        startHook();
    }

    /// <inheritdoc />
    public void Cancel()
    {
        if (_onFinished is null)
        {
            return;
        }

        _onFinished = null;
        _timer?.Dispose();
        _timer = null;
        cancelHook();
    }

    /// <summary>
    /// Принимает результат записи от потока функций (вызывается из любого потока) и передаёт в поток UI.
    /// </summary>
    /// <param name="gesture">Записанный жест или отмена.</param>
    public void Deliver(RecordedGesture gesture)
    {
        var generation = Volatile.Read(ref _generation);
        postToUi(() => Finish(gesture, generation));
    }

    /// <summary>Заканчивает запись жестом или отменой. Поток UI.</summary>
    private void Finish(RecordedGesture gesture, int generation)
    {
        if (generation != _generation || _onFinished is not { } callback)
        {
            return;
        }

        _onFinished = null;
        _timer?.Dispose();
        _timer = null;
        log.Info(gesture.Cancelled ? "trigger recording cancelled by Esc" : "trigger recording finished");
        callback(gesture);
    }

    /// <summary>Истёк срок записи: хук отпускается, окно получает отмену. Поток UI.</summary>
    private void OnTimeout(int generation)
    {
        if (generation != _generation || _onFinished is not { } callback)
        {
            return;
        }

        _onFinished = null;
        _timer?.Dispose();
        _timer = null;
        cancelHook();
        log.Info("trigger recording timed out");
        callback(RecordedGesture.Cancel);
    }
}
