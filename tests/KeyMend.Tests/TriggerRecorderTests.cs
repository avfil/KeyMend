using KeyMend.Input;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TriggerRecorder"/>: порядки нажатия и отпускания, разделение «держать» и «вместе», Esc, подавление.</summary>
public sealed class TriggerRecorderTests
{
    /// <summary>Клавиша F18.</summary>
    internal const VirtualKey F18 = (VirtualKey)0x81;

    /// <summary>Автомат под тестом.</summary>
    private readonly TriggerRecorder _recorder = new();

    /// <summary>Результаты записи в порядке получения.</summary>
    private readonly List<RecordedGesture> _gestures = [];

    /// <summary>Подавлено ли последнее событие.</summary>
    private bool _lastSuppressed;

    /// <summary>Подаёт нажатие и запоминает результат.</summary>
    private void Down(VirtualKey key, uint time) => Feed(key, isUp: false, time);

    /// <summary>Подаёт отпускание и запоминает результат.</summary>
    private void Up(VirtualKey key, uint time) => Feed(key, isUp: true, time);

    /// <summary>Подаёт событие автомату.</summary>
    private void Feed(VirtualKey key, bool isUp, uint time)
    {
        var outcome = _recorder.OnKey(new KeyEvent(key, 0, isUp, false, false, time));
        _lastSuppressed = outcome.Suppress;
        if (outcome.Gesture is { } gesture)
        {
            _gestures.Add(gesture);
        }
    }

    /// <summary>Автомат, которому запись ещё не запускали, ничего не подавляет.</summary>
    [Fact]
    public void OnKey_Idle_DoesNotSuppress()
    {
        Down(VirtualKey.RControl, 0);

        Assert.False(_lastSuppressed);
        Assert.False(_recorder.IsActive);
        Assert.Empty(_gestures);
    }

    /// <summary>Одиночное нажатие модификатора записывается на отпускании, оба события подавлены.</summary>
    [Fact]
    public void Tap_SingleModifier_RecordsKeys()
    {
        _recorder.Start();

        Down(VirtualKey.RControl, 0);
        Assert.True(_lastSuppressed);
        Assert.Empty(_gestures);
        Up(VirtualKey.RControl, 60);

        Assert.True(_lastSuppressed);
        Assert.Equal(new RecordedGesture(ModifierSet.RCtrl, ModifierSet.None, VirtualKey.None), Assert.Single(_gestures));
        Assert.False(_recorder.IsActive);
    }

    /// <summary>«Держать RCtrl, нажать RShift»: RCtrl нажат заметно раньше и отпущен позже — удерживаемый.</summary>
    [Fact]
    public void Tap_HeldThenPressed_RecordsHold()
    {
        _recorder.Start();

        Down(VirtualKey.RControl, 0);
        Down(VirtualKey.RShift, 500);
        Up(VirtualKey.RShift, 550);
        Up(VirtualKey.RControl, 700);

        Assert.Equal(
            new RecordedGesture(ModifierSet.RShift, ModifierSet.RCtrl, VirtualKey.None), Assert.Single(_gestures));
    }

    /// <summary>Небрежное «вместе» (нажаты почти одновременно) в любом порядке отпускания — оба нажимаемые.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tap_PressedTogether_RecordsBothAsKeys(bool ctrlReleasedFirst)
    {
        _recorder.Start();

        Down(VirtualKey.RControl, 0);
        Down(VirtualKey.RShift, TriggerRecorder.HoldGapMs - 1u);
        if (ctrlReleasedFirst)
        {
            Up(VirtualKey.RControl, 200);
            Up(VirtualKey.RShift, 230);
        }
        else
        {
            Up(VirtualKey.RShift, 200);
            Up(VirtualKey.RControl, 230);
        }

        Assert.Equal(
            new RecordedGesture(ModifierSet.RCtrl | ModifierSet.RShift, ModifierSet.None, VirtualKey.None),
            Assert.Single(_gestures));
    }

    /// <summary>Если удерживаемый модификатор отпущен раньше второго, это уже не «держать»: оба нажимаемые.</summary>
    [Fact]
    public void Tap_FirstReleasedBeforeSecond_RecordsBothAsKeys()
    {
        _recorder.Start();

        Down(VirtualKey.RControl, 0);
        Down(VirtualKey.RShift, 500);
        Up(VirtualKey.RControl, 550);
        Up(VirtualKey.RShift, 600);

        Assert.Equal(
            new RecordedGesture(ModifierSet.RCtrl | ModifierSet.RShift, ModifierSet.None, VirtualKey.None),
            Assert.Single(_gestures));
    }

    /// <summary>Левый Ctrl держится, правый Ctrl и Shift нажаты вместе: держится только левый.</summary>
    [Fact]
    public void Tap_ThreeModifiers_SplitsHoldFromKeys()
    {
        _recorder.Start();

        Down(VirtualKey.LControl, 0);
        Down(VirtualKey.RControl, 500);
        Down(VirtualKey.RShift, 520);
        Up(VirtualKey.RShift, 600);
        Up(VirtualKey.RControl, 610);
        Up(VirtualKey.LControl, 700);

        Assert.Equal(
            new RecordedGesture(ModifierSet.RCtrl | ModifierSet.RShift, ModifierSet.LCtrl, VirtualKey.None),
            Assert.Single(_gestures));
    }

    /// <summary>Автоповтор модификатора не меняет время нажатия и не мешает записи.</summary>
    [Fact]
    public void Tap_ModifierAutoRepeat_IsSuppressedAndIgnored()
    {
        _recorder.Start();

        Down(VirtualKey.RControl, 0);
        Down(VirtualKey.RControl, 400);
        Assert.True(_lastSuppressed);
        Up(VirtualKey.RControl, 800);

        Assert.Equal(new RecordedGesture(ModifierSet.RCtrl, ModifierSet.None, VirtualKey.None), Assert.Single(_gestures));
    }

    /// <summary>Обычная клавиша завершает запись сразу на нажатии; отпускание потом подавляется.</summary>
    [Fact]
    public void Press_PlainKey_RecordsOnDownAndSuppressesRelease()
    {
        _recorder.Start();

        Down(F18, 0);
        Assert.Equal(new RecordedGesture(ModifierSet.None, ModifierSet.None, F18), Assert.Single(_gestures));
        Assert.True(_recorder.IsActive);

        Down(F18, 500);
        Assert.True(_lastSuppressed);
        Assert.Single(_gestures);

        Up(F18, 900);
        Assert.True(_lastSuppressed);
        Assert.False(_recorder.IsActive);
    }

    /// <summary>Сочетание: зажатые модификаторы становятся удерживаемыми, их отпускание подавляется до конца.</summary>
    [Fact]
    public void Press_WithModifiers_RecordsHoldAndDrainsReleases()
    {
        _recorder.Start();

        Down(VirtualKey.LControl, 0);
        Down(VirtualKey.LShift, 30);
        Down((VirtualKey)0x31, 60);
        Up((VirtualKey)0x31, 120);
        Up(VirtualKey.LShift, 150);
        Assert.True(_recorder.IsActive);
        Up(VirtualKey.LControl, 170);

        Assert.True(_lastSuppressed);
        Assert.False(_recorder.IsActive);
        Assert.Equal(
            new RecordedGesture(ModifierSet.None, ModifierSet.LCtrl | ModifierSet.LShift, (VirtualKey)0x31),
            Assert.Single(_gestures));
    }

    /// <summary>Esc без модификаторов отменяет запись.</summary>
    [Fact]
    public void Escape_Alone_CancelsRecording()
    {
        _recorder.Start();

        Down(VirtualKey.Escape, 0);
        Up(VirtualKey.Escape, 50);

        Assert.True(Assert.Single(_gestures).Cancelled);
        Assert.False(_recorder.IsActive);
    }

    /// <summary>Esc с модификатором — обычное сочетание (позже проверка назовёт его системным).</summary>
    [Fact]
    public void Escape_WithCtrl_RecordsAsPress()
    {
        _recorder.Start();

        Down(VirtualKey.LControl, 0);
        Down(VirtualKey.Escape, 40);

        Assert.Equal(
            new RecordedGesture(ModifierSet.None, ModifierSet.LCtrl, VirtualKey.Escape), Assert.Single(_gestures));
    }

    /// <summary>Отпускание клавиши, нажатой до записи, доходит до окна и записи не мешает.</summary>
    [Fact]
    public void Up_OfKeyHeldBeforeRecording_PassesThrough()
    {
        _recorder.Start();

        Up(VirtualKey.Return, 0);

        Assert.False(_lastSuppressed);
        Assert.True(_recorder.IsActive);
        Assert.Empty(_gestures);
    }

    /// <summary>Прерывание без нажатых клавиш сразу освобождает автомат.</summary>
    [Fact]
    public void Abort_NothingPressed_BecomesIdle()
    {
        _recorder.Start();

        _recorder.Abort();

        Assert.False(_recorder.IsActive);
    }

    /// <summary>Прерывание с зажатой клавишей подавляет её отпускание и только потом освобождает автомат.</summary>
    [Fact]
    public void Abort_KeyHeld_SuppressesItsRelease()
    {
        _recorder.Start();
        Down(VirtualKey.RControl, 0);

        _recorder.Abort();
        Assert.True(_recorder.IsActive);
        Up(VirtualKey.RControl, 100);

        Assert.True(_lastSuppressed);
        Assert.False(_recorder.IsActive);
        Assert.Empty(_gestures);
    }

    /// <summary>Потерянное отпускание не оставляет хук в режиме записи навсегда.</summary>
    [Fact]
    public void Draining_LostRelease_EndsByTimeout()
    {
        _recorder.Start();
        Down(F18, 0);

        Down((VirtualKey)0x41, TriggerRecorder.DrainTimeoutMs + 1u);

        Assert.False(_lastSuppressed);
        Assert.False(_recorder.IsActive);
    }

    /// <summary>Новая запись после завершения прежней начинается с чистого состояния.</summary>
    [Fact]
    public void Start_AfterFinished_RecordsAgain()
    {
        _recorder.Start();
        Down(VirtualKey.RControl, 0);
        Up(VirtualKey.RControl, 50);

        _recorder.Start();
        Down(VirtualKey.RShift, 100);
        Up(VirtualKey.RShift, 150);

        Assert.Equal(
            [
                new RecordedGesture(ModifierSet.RCtrl, ModifierSet.None, VirtualKey.None),
                new RecordedGesture(ModifierSet.RShift, ModifierSet.None, VirtualKey.None),
            ],
            _gestures);
    }
}
