using KeyMend.Input;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TriggerRecording"/>: один результат на запись, отмена, тайм-аут, устаревшие результаты.</summary>
public sealed class TriggerRecordingTests
{
    /// <summary>Сколько раз попросили хук начать запись.</summary>
    private int _starts;

    /// <summary>Сколько раз попросили хук прервать запись.</summary>
    private int _cancels;

    /// <summary>Часы теста.</summary>
    private readonly TestTimeProvider _time = new(DateTimeOffset.UnixEpoch);

    /// <summary>Объект под тестом; «поток UI» выполняет действия сразу.</summary>
    private readonly TriggerRecording _recording;

    /// <summary>Результаты, дошедшие до окна.</summary>
    private readonly List<RecordedGesture> _results = [];

    /// <summary>Создаёт запись на заглушках.</summary>
    public TriggerRecordingTests()
    {
        _recording = new TriggerRecording(() => _starts++, () => _cancels++, action => action(), _time, new TestLog());
    }

    /// <summary>Жест, пришедший в ответ на запись, доходит до окна один раз.</summary>
    [Fact]
    public void Deliver_WhileRecording_CallsBackOnce()
    {
        var gesture = new RecordedGesture(ModifierSet.RCtrl, ModifierSet.None, VirtualKey.None);
        _recording.Start(_results.Add);

        _recording.Deliver(gesture);
        _recording.Deliver(gesture);

        Assert.Equal([gesture], _results);
        Assert.False(_recording.IsRecording);
        Assert.Equal(1, _starts);
    }

    /// <summary>Отмена из окна прерывает хук и не вызывает обратный вызов.</summary>
    [Fact]
    public void Cancel_WhileRecording_StopsHookWithoutCallback()
    {
        _recording.Start(_results.Add);

        _recording.Cancel();
        _recording.Deliver(RecordedGesture.Cancel);

        Assert.Equal(1, _cancels);
        Assert.Empty(_results);
        Assert.False(_recording.IsRecording);
    }

    /// <summary>Отмена без записи ничего не делает.</summary>
    [Fact]
    public void Cancel_WhenIdle_DoesNothing()
    {
        _recording.Cancel();

        Assert.Equal(0, _cancels);
    }

    /// <summary>Новая запись молча отменяет прежнюю.</summary>
    [Fact]
    public void Start_WhileRecording_CancelsPrevious()
    {
        List<RecordedGesture> first = [];
        _recording.Start(first.Add);

        _recording.Start(_results.Add);
        _recording.Deliver(new RecordedGesture(ModifierSet.RShift, ModifierSet.None, VirtualKey.None));

        Assert.Empty(first);
        Assert.Single(_results);
        Assert.Equal(1, _cancels);
        Assert.Equal(2, _starts);
    }

    /// <summary>Без жеста запись отменяется через минуту: окно получает отмену, хук освобождён.</summary>
    [Fact]
    public void Timeout_NoGesture_CancelsRecording()
    {
        _recording.Start(_results.Add);

        _time.Advance(TriggerRecording.Timeout + TimeSpan.FromSeconds(1));

        Assert.True(Assert.Single(_results).Cancelled);
        Assert.Equal(1, _cancels);
        Assert.False(_recording.IsRecording);
    }

    /// <summary>Жест до тайм-аута тайм-аут отменяет.</summary>
    [Fact]
    public void Deliver_BeforeTimeout_DisarmsTimer()
    {
        _recording.Start(_results.Add);
        _recording.Deliver(new RecordedGesture(ModifierSet.RCtrl, ModifierSet.None, VirtualKey.None));

        _time.Advance(TriggerRecording.Timeout + TimeSpan.FromSeconds(1));

        Assert.Single(_results);
        Assert.Equal(0, _cancels);
    }
}
