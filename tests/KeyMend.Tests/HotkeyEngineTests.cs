using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="HotkeyEngine"/>: детектор tap и press, стороны модификаторов, двойное нажатие, подавление и сбросы.</summary>
public sealed class HotkeyEngineTests
{
    /// <summary>Создаёт триггер tap из строки клавиш.</summary>
    private static TapTrigger Tap(string keys, string? hold = null, int maxHold = TapTrigger.DefaultMaxHoldMs, int? doubleMs = null)
    {
        Assert.True(KeyChordParser.TryParse(keys, out var chord));
        var holdSet = ModifierSet.None;
        if (hold is not null)
        {
            Assert.True(KeyChordParser.TryParse(hold, out var holdChord));
            holdSet = holdChord.Modifiers;
        }

        return new TapTrigger { Keys = chord.Modifiers, Hold = holdSet, MaxHoldMs = maxHold, DoubleMs = doubleMs };
    }

    /// <summary>Создаёт триггер press из строки клавиш.</summary>
    private static PressTrigger Press(string keys, int? doubleMs = null)
    {
        Assert.True(KeyChordParser.TryParse(keys, out var chord));
        return new PressTrigger { Hold = chord.Modifiers, Key = chord.Key, DoubleMs = doubleMs };
    }

    /// <summary>Проверяет, что событие пропущено без действий.</summary>
    private static void AssertPass(HookDecision d)
    {
        Assert.False(d.Suppress);
        Assert.Null(d.Rule);
    }

    /// <summary>Проверяет, что сработало правило с заданным именем.</summary>
    private static void AssertFired(HookDecision d, string name = "r0", bool suppress = false)
    {
        Assert.Equal(suppress, d.Suppress);
        Assert.Equal(name, d.Rule?.Name);
    }

    /// <summary>Стенд: движок с привязками и «часами», которые двигаются на каждое событие. Правила называются r0, r1, …</summary>
    private sealed class Rig
    {
        /// <summary>Текущее время стенда, мс.</summary>
        private uint _time = 1000;

        /// <summary>Создаёт стенд с одной привязкой.</summary>
        public Rig(Trigger trigger, Func<VirtualKey, bool>? physical = null)
            : this([trigger], physical)
        {
        }

        /// <summary>Создаёт стенд с несколькими привязками; имя правила — «r» и его номер.</summary>
        public Rig(Trigger[] triggers, Func<VirtualKey, bool>? physical = null)
        {
            Engine = new HotkeyEngine(physical);
            Engine.SetBindings(BindingTable.Build(triggers.Select((t, i) => new Rule { Name = "r" + i, Trigger = t })));
        }

        /// <summary>Движок под тестом.</summary>
        public HotkeyEngine Engine { get; }

        /// <summary>Текущее время стенда.</summary>
        public uint Now => _time;

        /// <summary>Нажимает клавишу через <paramref name="after"/> мс.</summary>
        public HookDecision Down(VirtualKey key, uint after = 10) => Send(key, isUp: false, after);

        /// <summary>Отпускает клавишу через <paramref name="after"/> мс.</summary>
        public HookDecision Up(VirtualKey key, uint after = 10) => Send(key, isUp: true, after);

        /// <summary>Нажимает и отпускает клавишу; возвращает решение на отпускание.</summary>
        public HookDecision TapKey(VirtualKey key, uint hold = 10, uint gap = 10)
        {
            Down(key, gap);
            return Up(key, hold);
        }

        /// <summary>Клик мышью.</summary>
        public void Click() => Engine.OnMouseDown(_time += 10);

        /// <summary>Ждёт без событий.</summary>
        public void Wait(uint ms) => _time += ms;

        /// <summary>Забирает созревшее правило на текущий момент.</summary>
        public string? TakeDue()
        {
            return Engine.TryTakeDue(_time, out var rule) ? rule.Name : null;
        }

        /// <summary>Отправляет событие.</summary>
        private HookDecision Send(VirtualKey key, bool isUp, uint after)
        {
            _time += after;
            return Engine.OnKey(new KeyEvent(key, 0, isUp, false, false, _time));
        }
    }

    /// <summary>Одиночное нажатие срабатывает на отпускание и клавишу не подавляет.</summary>
    [Fact]
    public void Tap_SingleKey_FiresOnRelease_WithoutSuppressing()
    {
        var rig = new Rig(Tap("RCtrl"));

        AssertPass(rig.Down(VirtualKey.RControl));
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Каждое новое нажатие срабатывает отдельно.</summary>
    [Fact]
    public void Tap_RepeatedTaps_FireEachTime()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl));
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Автоповтор не обновляет время начала удержания.</summary>
    [Fact]
    public void Tap_AutoRepeatDown_DoesNotRestartTimer()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        for (var i = 0; i < 5; i++)
        {
            AssertPass(rig.Down(VirtualKey.RControl, after: 100));
        }

        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Слишком долгое удержание не считается tap.</summary>
    [Fact]
    public void Tap_HeldLongerThanMaxHold_DoesNotFire()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        AssertPass(rig.Up(VirtualKey.RControl, after: 500));
    }

    /// <summary>Удержание ровно на пороге ещё считается tap.</summary>
    [Fact]
    public void Tap_AtExactMaxHold_Fires()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl, after: TapTrigger.DefaultMaxHoldMs));
    }

    /// <summary>У каждой привязки свой порог удержания.</summary>
    [Fact]
    public void Tap_PerBindingMaxHold_IsRespected()
    {
        var rig = new Rig([Tap("RCtrl", maxHold: 100), Tap("RShift", maxHold: 300)]);

        rig.Down(VirtualKey.RControl);
        AssertPass(rig.Up(VirtualKey.RControl, after: 200));
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift, after: 200), "r1");
    }

    /// <summary>Другая клавиша между нажатием и отпусканием отменяет tap (Ctrl+C с правым Ctrl не переключает язык).</summary>
    [Fact]
    public void Tap_WithOtherKeyBetween_DoesNotFire()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        rig.Down((VirtualKey)'C');
        rig.Up((VirtualKey)'C');
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Клавиша, нажатая и отпущенная до tap, ему не мешает.</summary>
    [Fact]
    public void Tap_KeyPressedBeforeAndReleasedBefore_DoesNotBlock()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.TapKey((VirtualKey)'A');
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Клик мышью между нажатием и отпусканием отменяет tap.</summary>
    [Fact]
    public void Tap_WithMouseClickBetween_DoesNotFire()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        rig.Click();
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Клик до tap ему не мешает.</summary>
    [Fact]
    public void Tap_MouseClickBeforePress_DoesNotBlock()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Click();
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Модификатор другой стороны не запускает привязку.</summary>
    [Fact]
    public void Tap_OtherSideModifier_DoesNotFire()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.LControl);
        AssertPass(rig.Up(VirtualKey.LControl));
    }

    /// <summary>Лишний зажатый модификатор отменяет tap.</summary>
    [Fact]
    public void Tap_WithExtraModifierHeld_DoesNotFire()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.LShift);
        rig.Down(VirtualKey.RControl);
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Tap из двух модификаторов срабатывает на первое отпускание в любом порядке.</summary>
    [Fact]
    public void Tap_Combo_FiresOnFirstRelease_InEitherOrder()
    {
        foreach (var (first, second) in new[]
                 {
                     (VirtualKey.LShift, VirtualKey.RShift), (VirtualKey.RShift, VirtualKey.LShift),
                 })
        {
            var rig = new Rig(Tap("LShift+RShift"));
            AssertPass(rig.Down(VirtualKey.LShift));
            AssertPass(rig.Down(VirtualKey.RShift));
            AssertFired(rig.Up(first));
            AssertPass(rig.Up(second));
        }
    }

    /// <summary>Один из двух модификаторов сам по себе комбинацию не запускает.</summary>
    [Fact]
    public void Tap_Combo_SingleShiftAlone_DoesNotFire()
    {
        var rig = new Rig(Tap("LShift+RShift"));

        rig.Down(VirtualKey.LShift);
        AssertPass(rig.Up(VirtualKey.LShift));
    }

    /// <summary>SPEC 3.1: RCtrl ≠ LCtrl+RCtrl — жест из двух Ctrl не запускает привязку на один.</summary>
    [Fact]
    public void Tap_RCtrl_IsNotTriggeredByBothCtrlTap()
    {
        var rig = new Rig([Tap("LCtrl+RCtrl"), Tap("RCtrl")]);

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.LControl), "r0");
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>После жеста из двух модификаторов одиночный снова работает.</summary>
    [Fact]
    public void Tap_Single_StillWorksAfterCombo()
    {
        var rig = new Rig([Tap("LShift+RShift"), Tap("RShift")]);

        rig.Down(VirtualKey.LShift);
        rig.Down(VirtualKey.RShift);
        rig.Up(VirtualKey.LShift);
        rig.Up(VirtualKey.RShift);

        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift), "r1");
    }

    /// <summary>SPEC 3.1: LCtrl+RCtrl+1 не вызывает привязки на RCtrl и на LCtrl+RCtrl.</summary>
    [Fact]
    public void Press_CtrlCtrlDigit_DoesNotTriggerTapsOnRelease()
    {
        var rig = new Rig([Press("LCtrl+RCtrl+1"), Tap("LCtrl+RCtrl"), Tap("RCtrl")]);

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Down((VirtualKey)'1'), "r0", suppress: true);
        rig.Up((VirtualKey)'1');
        AssertPass(rig.Up(VirtualKey.LControl));
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>SPEC 3.1: сочетание с левым Ctrl вместо двух не срабатывает.</summary>
    [Fact]
    public void Press_CtrlCtrlDigit_NeedsBothCtrls()
    {
        var rig = new Rig(Press("LCtrl+RCtrl+1"));

        rig.Down(VirtualKey.LControl);
        AssertPass(rig.Down((VirtualKey)'1'));
    }

    /// <summary>SPEC 3.1: RShift ≠ «держать RCtrl + RShift» ≠ «держать LCtrl + RShift».</summary>
    [Fact]
    public void Tap_RShift_DistinguishedByHeldCtrlSide()
    {
        var rig = new Rig([Tap("RShift"), Tap("RShift", hold: "RCtrl"), Tap("RShift", hold: "LCtrl")]);

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift), "r1");
        rig.Up(VirtualKey.RControl);

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift), "r2");
        rig.Up(VirtualKey.LControl);

        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift), "r0");
    }

    /// <summary>Набор правил по умолчанию для проверки порядка нажатий: слово, строка, отмена, выделенное, язык.</summary>
    private static Trigger[] CorrectionTriggers() =>
    [
        Tap("RShift"), Tap("RShift", hold: "RCtrl"), Tap("RShift", hold: "LCtrl"), Tap("LCtrl+RCtrl"), Tap("RCtrl"),
    ];

    /// <summary>Человек нажимает RCtrl чуть позже RShift: «строка» всё равно срабатывает.</summary>
    [Fact]
    public void Tap_HoldPressedAfterTapKey_StillFires()
    {
        var rig = new Rig(CorrectionTriggers());

        rig.Down(VirtualKey.RShift);
        rig.Down(VirtualKey.RControl, after: 4);
        AssertFired(rig.Up(VirtualKey.RShift, after: 70), "r1");
    }

    /// <summary>Человек отпускает RCtrl раньше RShift: «строка» срабатывает при отпускании RCtrl.</summary>
    [Fact]
    public void Tap_HoldReleasedBeforeTapKey_StillFires()
    {
        var rig = new Rig(CorrectionTriggers());

        rig.Down(VirtualKey.RShift);
        rig.Down(VirtualKey.RControl, after: 0);
        AssertFired(rig.Up(VirtualKey.RControl, after: 100), "r1");
        AssertPass(rig.Up(VirtualKey.RShift, after: 15));
    }

    /// <summary>Правила исправления по умолчанию (порог 300 мс) и язык (150 мс): для матрицы порядков нажатия.</summary>
    private static Trigger[] GestureTriggers() =>
    [
        Tap("RShift", maxHold: 300), Tap("RShift", hold: "RCtrl", maxHold: 300), Tap("RCtrl"),
    ];

    /// <summary>SPEC 3.1 п. 7: «RCtrl + RShift» в любом порядке нажатия и отпускания даёт ровно одно срабатывание строки.</summary>
    [Theory]
    [InlineData(true, true, 0u)]
    [InlineData(true, false, 0u)]
    [InlineData(false, true, 0u)]
    [InlineData(false, false, 0u)]
    [InlineData(true, true, 40u)]
    [InlineData(false, false, 40u)]
    public void Gesture_HoldPlusTap_AnyOrder_FiresLineOnce(bool shiftFirstDown, bool shiftFirstUp, uint gap)
    {
        var rig = new Rig(GestureTriggers());
        var (down1, down2) = shiftFirstDown
            ? (VirtualKey.RShift, VirtualKey.RControl)
            : (VirtualKey.RControl, VirtualKey.RShift);
        var (up1, up2) = shiftFirstUp
            ? (VirtualKey.RShift, VirtualKey.RControl)
            : (VirtualKey.RControl, VirtualKey.RShift);

        AssertPass(rig.Down(down1));
        AssertPass(rig.Down(down2, after: gap));
        var first = rig.Up(up1, after: 30);
        var second = rig.Up(up2, after: 10);

        var fired = new[] { first, second }.Where(d => d.Rule is not null).ToArray();
        Assert.Single(fired);
        Assert.Equal("r1", fired[0].Rule?.Name);
    }

    /// <summary>Жест собирают медленно: от первой клавиши больше порога, но от последней — меньше.</summary>
    [Fact]
    public void Gesture_SlowAssembly_FiresWhenLastKeyIsWithinThreshold()
    {
        var rig = new Rig(GestureTriggers());

        rig.Down(VirtualKey.RShift);
        rig.Down(VirtualKey.RControl, after: 250);
        AssertFired(rig.Up(VirtualKey.RShift, after: 100), "r1");
    }

    /// <summary>Удержание после сборки жеста дольше порога жест отменяет.</summary>
    [Fact]
    public void Gesture_HeldLongerThanThresholdAfterLastKey_DoesNotFire()
    {
        var rig = new Rig(GestureTriggers());

        rig.Down(VirtualKey.RShift);
        rig.Down(VirtualKey.RControl, after: 50);
        AssertPass(rig.Up(VirtualKey.RShift, after: 400));
    }

    /// <summary>Модификатор, который держали задолго до жеста, в отсчёт порога не входит.</summary>
    [Fact]
    public void Gesture_HoldPressedLongBefore_DoesNotCountTowardThreshold()
    {
        var rig = new Rig(GestureTriggers());

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.RShift, after: 5000);
        AssertFired(rig.Up(VirtualKey.RShift, after: 100), "r1");
    }

    /// <summary>Отпускание второй клавиши жеста после срабатывания не запускает язык (RCtrl) и выделенное (RShift).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gesture_SecondRelease_DoesNotFireOtherRules(bool shiftFirstUp)
    {
        var rig = new Rig(GestureTriggers());
        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.RShift);
        var (up1, up2) = shiftFirstUp
            ? (VirtualKey.RShift, VirtualKey.RControl)
            : (VirtualKey.RControl, VirtualKey.RShift);

        Assert.NotNull(rig.Up(up1).Rule);
        AssertPass(rig.Up(up2));
    }

    /// <summary>Клик между нажатием и отпусканием отменяет жест.</summary>
    [Fact]
    public void Gesture_ClickInside_DoesNotFire()
    {
        var rig = new Rig(GestureTriggers());

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.RShift);
        rig.Click();
        AssertPass(rig.Up(VirtualKey.RShift));
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Лишняя клавиша внутри жеста отменяет его.</summary>
    [Fact]
    public void Gesture_ForeignKeyInside_DoesNotFire()
    {
        var rig = new Rig(GestureTriggers());

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.RShift);
        rig.Down((VirtualKey)'A');
        AssertPass(rig.Up(VirtualKey.RShift));
    }

    /// <summary>Сочетание из двух Ctrl и клавиши: порядок нажатия модификаторов и отпускания не важен.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Gesture_CtrlCtrlDigit_AnyModifierOrder_Fires(bool leftFirstDown, bool leftFirstUp)
    {
        var rig = new Rig([Press("LCtrl+RCtrl+1"), Tap("LCtrl+RCtrl"), Tap("RCtrl")]);
        var (d1, d2) = leftFirstDown
            ? (VirtualKey.LControl, VirtualKey.RControl)
            : (VirtualKey.RControl, VirtualKey.LControl);
        var (u1, u2) = leftFirstUp
            ? (VirtualKey.LControl, VirtualKey.RControl)
            : (VirtualKey.RControl, VirtualKey.LControl);

        rig.Down(d1);
        rig.Down(d2);
        AssertFired(rig.Down((VirtualKey)'1'), "r0", suppress: true);
        rig.Up((VirtualKey)'1');
        AssertPass(rig.Up(u1));
        AssertPass(rig.Up(u2));
    }

    /// <summary>Жест из двух модификаторов собирают медленно: порог считается от последней клавиши.</summary>
    [Fact]
    public void Gesture_ComboSlowAssembly_FiresFromLastKey()
    {
        var rig = new Rig(Tap("LCtrl+RCtrl", maxHold: 300));

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.RControl, after: 250);
        AssertFired(rig.Up(VirtualKey.LControl, after: 100));
    }

    /// <summary>«Держать любой Ctrl» принимает левый и правый, но не отсутствие.</summary>
    [Fact]
    public void Tap_HoldAnyCtrl_AcceptsEitherSide()
    {
        var rig = new Rig(Tap("RShift", hold: "Ctrl"));

        rig.Down(VirtualKey.LControl, after: 2000);
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift));
        rig.Up(VirtualKey.LControl);

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift));
        rig.Up(VirtualKey.RControl);

        rig.Down(VirtualKey.RShift);
        AssertPass(rig.Up(VirtualKey.RShift));
    }

    /// <summary>Пока удерживается нужный модификатор, tap можно повторять.</summary>
    [Fact]
    public void Tap_WithHeldModifier_RepeatsWhileHeld()
    {
        var rig = new Rig(Tap("RShift", hold: "Ctrl"));

        rig.Down(VirtualKey.RControl, after: 2000);
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift));
        rig.Down(VirtualKey.RShift);
        AssertFired(rig.Up(VirtualKey.RShift));
    }

    /// <summary>Правый Shift при зажатом левом не запускает одиночную привязку.</summary>
    [Fact]
    public void Tap_RShiftWithLShiftHeld_DoesNotFireSingle()
    {
        var rig = new Rig(Tap("RShift"));

        rig.Down(VirtualKey.LShift);
        rig.Down(VirtualKey.RShift);
        AssertPass(rig.Up(VirtualKey.RShift));
    }

    /// <summary>Одиночный Alt при отпускании подавляется и просит маску.</summary>
    [Fact]
    public void Tap_Alt_SuppressesRelease()
    {
        var rig = new Rig(Tap("RAlt"));

        AssertPass(rig.Down(VirtualKey.RMenu));
        var d = rig.Up(VirtualKey.RMenu);
        AssertFired(d, suppress: true);
        Assert.True(d.SendMask);
        Assert.Equal(VirtualKey.RMenu, d.ReleaseKey);
    }

    /// <summary>Одиночный Ctrl маски не требует.</summary>
    [Fact]
    public void Tap_Ctrl_DoesNotAskForMask()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        var d = rig.Up(VirtualKey.RControl);

        Assert.False(d.SendMask);
        Assert.Equal(VirtualKey.None, d.ReleaseKey);
    }

    /// <summary>Сочетание с Alt просит маску, но отпускание клавиши не подавляет.</summary>
    [Fact]
    public void Press_WithAlt_AsksForMask_WithoutRelease()
    {
        var rig = new Rig(Press("Alt+Space"));

        rig.Down(VirtualKey.LMenu);
        var d = rig.Down(VirtualKey.Space);

        AssertFired(d, suppress: true);
        Assert.True(d.SendMask);
        Assert.Equal(VirtualKey.None, d.ReleaseKey);
    }

    /// <summary>Сочетание с Ctrl маски не требует.</summary>
    [Fact]
    public void Press_WithCtrl_DoesNotAskForMask()
    {
        var rig = new Rig(Press("Ctrl+Space"));

        rig.Down(VirtualKey.LControl);

        Assert.False(rig.Down(VirtualKey.Space).SendMask);
    }

    /// <summary>Одиночная Win при отпускании подавляется.</summary>
    [Fact]
    public void Tap_Win_SuppressesRelease()
    {
        var rig = new Rig(Tap("LWin"));

        rig.Down(VirtualKey.LWin);
        AssertFired(rig.Up(VirtualKey.LWin), suppress: true);
    }

    /// <summary>Переполнение 32-битного счётчика времени не ломает распознавание.</summary>
    [Fact]
    public void Tap_TimeCounterWraparound_StillWorks()
    {
        var engine = new HotkeyEngine();
        engine.SetBindings(BindingTable.Build([new Rule { Name = "r0", Trigger = Tap("RCtrl") }]));

        engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, false, false, false, uint.MaxValue - 50));
        var d = engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, true, false, false, 90));

        AssertFired(d);
    }

    /// <summary>Press срабатывает на нажатие и подавляет нажатие, автоповтор и отпускание.</summary>
    [Fact]
    public void Press_FiresOnDown_SuppressesDownRepeatsAndUp()
    {
        var rig = new Rig(Press("Ctrl+Shift+Space"));

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.LShift);
        AssertFired(rig.Down(VirtualKey.Space), suppress: true);
        var repeat = rig.Down(VirtualKey.Space);
        Assert.True(repeat.Suppress);
        Assert.Null(repeat.Rule);
        var up = rig.Up(VirtualKey.Space);
        Assert.True(up.Suppress);
        Assert.Null(up.Rule);
        AssertPass(rig.Up(VirtualKey.LShift));
    }

    /// <summary>Автоповтор press второй раз правило не запускает.</summary>
    [Fact]
    public void Press_Repeat_DoesNotFireAgain()
    {
        var rig = new Rig(Press("Ctrl+Space"));

        rig.Down(VirtualKey.LControl);
        AssertFired(rig.Down(VirtualKey.Space), suppress: true);
        Assert.Null(rig.Down(VirtualKey.Space).Rule);
    }

    /// <summary>Лишний модификатор отменяет press.</summary>
    [Fact]
    public void Press_ExtraModifier_DoesNotFire()
    {
        var rig = new Rig(Press("Ctrl+Space"));

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.LShift);
        AssertPass(rig.Down(VirtualKey.Space));
    }

    /// <summary>Недостающий модификатор отменяет press.</summary>
    [Fact]
    public void Press_MissingModifier_DoesNotFire()
    {
        var rig = new Rig(Press("Ctrl+Shift+Space"));

        rig.Down(VirtualKey.LControl);
        AssertPass(rig.Down(VirtualKey.Space));
    }

    /// <summary>Клавиша без модификаторов подавляется целиком.</summary>
    [Fact]
    public void Press_NoModifiers_SuppressesKey()
    {
        var rig = new Rig(Press("CapsLock"));

        AssertFired(rig.Down(VirtualKey.CapsLock), suppress: true);
        Assert.True(rig.Up(VirtualKey.CapsLock).Suppress);
    }

    /// <summary>Отпускание клавиши после отпускания модификатора всё равно подавляется.</summary>
    [Fact]
    public void Press_UpAfterModifierReleased_StillSuppressed()
    {
        var rig = new Rig(Press("Ctrl+Space"));

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.Space);
        rig.Up(VirtualKey.RControl);
        Assert.True(rig.Up(VirtualKey.Space).Suppress);
    }

    /// <summary>После отпускания press срабатывает снова.</summary>
    [Fact]
    public void Press_AfterRelease_FiresAgain()
    {
        var rig = new Rig(Press("F18"));

        AssertFired(rig.Down((VirtualKey)0x81), suppress: true);
        rig.Up((VirtualKey)0x81);
        AssertFired(rig.Down((VirtualKey)0x81), suppress: true);
    }

    /// <summary>Press во время tap отменяет tap.</summary>
    [Fact]
    public void Press_CancelsTapInProgress()
    {
        var rig = new Rig([Tap("RCtrl"), Press("Ctrl+Space")]);

        rig.Down(VirtualKey.RControl);
        rig.Down(VirtualKey.Space);
        rig.Up(VirtualKey.Space);
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Сброс забывает нажатые клавиши.</summary>
    [Fact]
    public void Reset_ClearsHeldKeys()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down(VirtualKey.RControl);
        rig.Engine.Reset();
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Сброс забывает подавление отпускания.</summary>
    [Fact]
    public void Reset_ClearsPressSuppression()
    {
        var rig = new Rig(Press("Ctrl+Space"));

        rig.Down(VirtualKey.LControl);
        rig.Down(VirtualKey.Space);
        rig.Engine.Reset();
        AssertPass(rig.Up(VirtualKey.Space));
    }

    /// <summary>Сброс забывает «чужие» клавиши.</summary>
    [Fact]
    public void Reset_ForgetsForeignKeys()
    {
        var rig = new Rig(Tap("RCtrl"));

        rig.Down((VirtualKey)'X');
        rig.Engine.Reset();
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>«Залипший» модификатор очищается по физическому состоянию клавиатуры.</summary>
    [Fact]
    public void StaleHeldModifier_IsClearedUsingPhysicalState()
    {
        var rig = new Rig(Tap("RCtrl"), physical: key => key != VirtualKey.LShift);

        rig.Down(VirtualKey.LShift);
        rig.Engine.Reset();
        rig.Down(VirtualKey.LShift);
        rig.Down(VirtualKey.RControl);
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Физическое состояние текущей клавиши не запрашивается: в колбэке хука оно ещё не обновлено.</summary>
    [Fact]
    public void PhysicalState_IsNotQueriedForCurrentKey()
    {
        var queried = new List<VirtualKey>();
        var rig = new Rig(Tap("RCtrl"), physical: key =>
        {
            queried.Add(key);
            return true;
        });

        rig.Down(VirtualKey.RControl);
        rig.Up(VirtualKey.RControl);

        Assert.DoesNotContain(VirtualKey.RControl, queried);
    }

    /// <summary>Без привязок движок пропускает всё.</summary>
    [Fact]
    public void NoBindings_PassesEverything()
    {
        var engine = new HotkeyEngine();

        AssertPass(engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, false, false, false, 1)));
        AssertPass(engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, true, false, false, 2)));
    }

    /// <summary>Только двойное: первое нажатие ничего не запускает и не подавляется, второе в срок запускает.</summary>
    [Fact]
    public void Double_Tap_OnlyDouble_FiresOnSecondTap()
    {
        var rig = new Rig(Tap("RCtrl", doubleMs: 200));

        rig.Down(VirtualKey.RControl);
        AssertPass(rig.Up(VirtualKey.RControl));
        Assert.Null(rig.Engine.NextDeadline);
        rig.Down(VirtualKey.RControl, after: 50);
        AssertFired(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Только двойное: второе нажатие после интервала считается первым.</summary>
    [Fact]
    public void Double_Tap_SecondTapTooLate_DoesNotFire()
    {
        var rig = new Rig(Tap("RCtrl", doubleMs: 200));

        rig.TapKey(VirtualKey.RControl);
        rig.Wait(300);
        rig.Down(VirtualKey.RControl);
        AssertPass(rig.Up(VirtualKey.RControl));
    }

    /// <summary>Пара: одиночное откладывается до конца интервала и срабатывает по сроку.</summary>
    [Fact]
    public void Double_Tap_SingleAndDouble_SingleFiresAfterInterval()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        AssertPass(rig.TapKey(VirtualKey.RControl));
        var deadline = rig.Engine.NextDeadline;
        Assert.NotNull(deadline);
        Assert.Null(rig.TakeDue());

        rig.Wait(250);
        Assert.Equal("r0", rig.TakeDue());
        Assert.Null(rig.TakeDue());
        Assert.Null(rig.Engine.NextDeadline);
    }

    /// <summary>Пара: срок созревания равен времени первого отпускания плюс интервал.</summary>
    [Fact]
    public void Double_Tap_Deadline_IsFirstReleasePlusInterval()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        rig.TapKey(VirtualKey.RControl);

        Assert.Equal(rig.Now + 200, rig.Engine.NextDeadline);
    }

    /// <summary>Пара: второе нажатие в срок запускает двойное, одиночное не срабатывает.</summary>
    [Fact]
    public void Double_Tap_SingleAndDouble_SecondTapFiresDoubleOnly()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        rig.TapKey(VirtualKey.RControl);
        rig.Down(VirtualKey.RControl, after: 50);
        AssertFired(rig.Up(VirtualKey.RControl), "r1");

        rig.Wait(500);
        Assert.Null(rig.TakeDue());
    }

    /// <summary>Пара: чужая клавиша сразу завершает ожидание, одиночное срабатывает без паузы.</summary>
    [Fact]
    public void Double_Tap_ForeignKey_ReleasesSingleImmediately()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        rig.TapKey(VirtualKey.RControl);
        rig.Down((VirtualKey)'A');

        Assert.Equal("r0", rig.TakeDue());
    }

    /// <summary>Пара: клик мышью сразу завершает ожидание.</summary>
    [Fact]
    public void Double_Tap_MouseClick_ReleasesSingleImmediately()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        rig.TapKey(VirtualKey.RControl);
        rig.Click();

        Assert.Equal("r0", rig.TakeDue());
    }

    /// <summary>Пара: после просрочки очередной tap — новое первое нажатие, а прежнее одиночное не теряется.</summary>
    [Fact]
    public void Double_Tap_LateSecondTap_KeepsFirstSingleAndStartsNewPending()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        rig.TapKey(VirtualKey.RControl);
        rig.Wait(300);
        AssertPass(rig.TapKey(VirtualKey.RControl));

        Assert.Equal("r0", rig.TakeDue());
        Assert.NotNull(rig.Engine.NextDeadline);
    }

    /// <summary>Пара: переполнение счётчика времени не ломает двойное нажатие.</summary>
    [Fact]
    public void Double_Tap_TimeCounterWraparound_StillWorks()
    {
        var engine = new HotkeyEngine();
        engine.SetBindings(BindingTable.Build([new Rule { Name = "r0", Trigger = Tap("RCtrl", doubleMs: 200) }]));

        engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, false, false, false, uint.MaxValue - 50));
        engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, true, false, false, uint.MaxValue - 40));
        engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, false, false, false, 20));
        var d = engine.OnKey(new KeyEvent(VirtualKey.RControl, 0, true, false, false, 30));

        AssertFired(d);
    }

    /// <summary>Двойное на одиночном Alt: первое отпускание подавляется с маской, но правила ещё нет.</summary>
    [Fact]
    public void Double_Tap_Alt_FirstTapSuppressesAndAsksForMaskWithoutRule()
    {
        var rig = new Rig(Tap("RAlt", doubleMs: 300));

        rig.Down(VirtualKey.RMenu);
        var d = rig.Up(VirtualKey.RMenu);

        Assert.True(d.Suppress);
        Assert.True(d.SendMask);
        Assert.Equal(VirtualKey.RMenu, d.ReleaseKey);
        Assert.Null(d.Rule);
    }

    /// <summary>Двойное нажатие клавиши: первое нажатие и отпускание подавляются, второе в срок запускает правило.</summary>
    [Fact]
    public void Double_Press_OnlyDouble_SuppressesFirstAndFiresOnSecond()
    {
        var rig = new Rig(Press("F17", doubleMs: 400));
        var f17 = (VirtualKey)0x80;

        var first = rig.Down(f17);
        Assert.True(first.Suppress);
        Assert.Null(first.Rule);
        Assert.True(rig.Up(f17).Suppress);

        AssertFired(rig.Down(f17, after: 100), suppress: true);
        Assert.True(rig.Up(f17).Suppress);
    }

    /// <summary>Пара для клавиши: одиночное срабатывает по сроку, двойное — на втором нажатии.</summary>
    [Fact]
    public void Double_Press_SingleAndDouble_BothPathsWork()
    {
        var rig = new Rig([Press("F17"), Press("F17", doubleMs: 400)]);
        var f17 = (VirtualKey)0x80;

        AssertPass(rig.Down(f17) with { Suppress = false });
        rig.Up(f17);
        rig.Wait(500);
        Assert.Equal("r0", rig.TakeDue());

        rig.Down(f17);
        rig.Up(f17);
        AssertFired(rig.Down(f17, after: 100), "r1", suppress: true);
    }

    /// <summary>Подмена привязок сбрасывает ожидающее нажатие.</summary>
    [Fact]
    public void SetBindings_ClearsPendingDouble()
    {
        var rig = new Rig([Tap("RCtrl"), Tap("RCtrl", doubleMs: 200)]);

        rig.TapKey(VirtualKey.RControl);
        rig.Engine.SetBindings(BindingTable.Empty);
        rig.Wait(500);

        Assert.Null(rig.TakeDue());
        Assert.Null(rig.Engine.NextDeadline);
    }
}
