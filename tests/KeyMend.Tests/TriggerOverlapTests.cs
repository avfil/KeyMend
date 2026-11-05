using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TriggerOverlap"/> и <see cref="SystemShortcuts"/>: обязательные случаи SPEC 3.1, п. 3.</summary>
public sealed class TriggerOverlapTests
{
    /// <summary>Клавиша F17.</summary>
    internal const VirtualKey F17 = (VirtualKey)0x80;

    /// <summary>Клавиша F18.</summary>
    internal const VirtualKey F18 = (VirtualKey)0x81;

    /// <summary>Создаёт tap.</summary>
    private static TapTrigger Tap(ModifierSet keys, ModifierSet hold = ModifierSet.None, int? doubleMs = null) =>
        new() { Keys = keys, Hold = hold, DoubleMs = doubleMs };

    /// <summary>Создаёт сочетание.</summary>
    private static PressTrigger Press(VirtualKey key, ModifierSet hold = ModifierSet.None) =>
        new() { Key = key, Hold = hold };

    /// <summary>RCtrl, RCtrl+RShift и LCtrl+RShift — три разных жеста.</summary>
    [Theory]
    [InlineData((int)ModifierSet.RCtrl, (int)(ModifierSet.RCtrl | ModifierSet.RShift))]
    [InlineData((int)ModifierSet.RShift, (int)(ModifierSet.RCtrl | ModifierSet.RShift))]
    [InlineData((int)ModifierSet.RShift, (int)(ModifierSet.LCtrl | ModifierSet.RShift))]
    [InlineData((int)ModifierSet.RCtrl, (int)ModifierSet.LCtrl)]
    public void Overlaps_DifferentTaps_ReturnsFalse(int first, int second)
    {
        Assert.False(TriggerOverlap.Overlaps(Tap((ModifierSet)first), Tap((ModifierSet)second)));
    }

    /// <summary>Два одинаковых tap пересекаются, двойное нажатие на пересечение не влияет.</summary>
    [Fact]
    public void Overlaps_SameTapSingleAndDouble_ReturnsTrue()
    {
        Assert.True(TriggerOverlap.Overlaps(Tap(ModifierSet.RCtrl), Tap(ModifierSet.RCtrl, doubleMs: 200)));
    }

    /// <summary>«Держать RCtrl, нажать RShift» и «RCtrl+RShift» срабатывают на один жест.</summary>
    [Fact]
    public void Overlaps_HoldTapAndCombinedTap_ReturnsTrue()
    {
        var hold = Tap(ModifierSet.RShift, ModifierSet.RCtrl);
        var both = Tap(ModifierSet.RCtrl | ModifierSet.RShift);

        Assert.True(TriggerOverlap.Overlaps(hold, both));
    }

    /// <summary>Удержание «любой Ctrl» пересекается с левым и правым Ctrl, но не со своим отсутствием.</summary>
    [Fact]
    public void Overlaps_AnyCtrlHold_CoversBothSidesButNotNone()
    {
        var any = Tap(ModifierSet.RShift, ModifierSet.AnyCtrl);

        Assert.True(TriggerOverlap.Overlaps(any, Tap(ModifierSet.RShift, ModifierSet.LCtrl)));
        Assert.True(TriggerOverlap.Overlaps(any, Tap(ModifierSet.RShift, ModifierSet.RCtrl)));
        Assert.False(TriggerOverlap.Overlaps(any, Tap(ModifierSet.RShift)));
    }

    /// <summary>Сочетания с разными модификаторами сторон не пересекаются, с тем же набором — пересекаются.</summary>
    [Fact]
    public void Overlaps_Presses_ComparesKeyAndModifiers()
    {
        const VirtualKey f18 = F18;
        Assert.True(TriggerOverlap.Overlaps(Press(f18), Press(f18)));
        Assert.False(TriggerOverlap.Overlaps(Press(f18), Press(F17)));
        Assert.False(TriggerOverlap.Overlaps(Press(f18), Press(f18, ModifierSet.LCtrl)));
        Assert.False(TriggerOverlap.Overlaps(Press(f18, ModifierSet.LCtrl), Press(f18, ModifierSet.RCtrl)));
        Assert.True(TriggerOverlap.Overlaps(Press(f18, ModifierSet.AnyCtrl), Press(f18, ModifierSet.RCtrl)));
    }

    /// <summary>Tap и сочетание не пересекаются: у первого нет основной клавиши.</summary>
    [Fact]
    public void Overlaps_TapAndPress_ReturnsFalse()
    {
        Assert.False(TriggerOverlap.Overlaps(Tap(ModifierSet.RCtrl), Press(F18, ModifierSet.RCtrl)));
    }

    /// <summary>Win+L с любой Win и Ctrl+Shift+Esc с любыми сторонами — системные.</summary>
    [Theory]
    [InlineData(0x4C, (int)ModifierSet.LWin)]
    [InlineData(0x4C, (int)ModifierSet.RWin)]
    [InlineData(0x1B, (int)(ModifierSet.LCtrl | ModifierSet.RShift))]
    [InlineData(0x09, (int)ModifierSet.LAlt)]
    [InlineData(0x2E, (int)(ModifierSet.RCtrl | ModifierSet.LAlt))]
    [InlineData(0x73, (int)ModifierSet.RAlt)]
    public void IsReserved_SystemShortcut_ReturnsTrue(int key, int hold)
    {
        Assert.True(SystemShortcuts.IsReserved(Press((VirtualKey)key, (ModifierSet)hold)));
    }

    /// <summary>Обычные клавиши и сочетания не зарезервированы.</summary>
    [Fact]
    public void IsReserved_OrdinaryTriggers_ReturnsFalse()
    {
        Assert.False(SystemShortcuts.IsReserved(Press(F18)));
        Assert.False(SystemShortcuts.IsReserved(Press(VirtualKey.Tab)));
        Assert.False(SystemShortcuts.IsReserved(Press(VirtualKey.Escape, ModifierSet.LShift)));
        Assert.False(SystemShortcuts.IsReserved(Tap(ModifierSet.RCtrl)));
    }
}
