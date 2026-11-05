using KeyMend.Input;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="ModifierSets"/>: сопоставление требований со сторонами и «любым» модификатором.</summary>
public sealed class ModifierSetsTests
{
    /// <summary>Требование «левый» принимает только левый, «правый» — только правый.</summary>
    [Theory]
    [InlineData((int)(ModifierSet.LCtrl), (int)(ModifierSet.LCtrl), true)]
    [InlineData((int)(ModifierSet.LCtrl), (int)(ModifierSet.RCtrl), false)]
    [InlineData((int)(ModifierSet.LCtrl), (int)(ModifierSet.LCtrl | ModifierSet.RCtrl), false)]
    [InlineData((int)(ModifierSet.RCtrl), (int)(ModifierSet.RCtrl), true)]
    [InlineData((int)(ModifierSet.RCtrl), (int)(ModifierSet.None), false)]
    public void Matches_SidedRequirement_ChecksExactSide(int required, int held, bool expected)
    {
        Assert.Equal(expected, ModifierSets.Matches((ModifierSet)required, (ModifierSet)held));
    }

    /// <summary>Требование «оба» принимает только оба.</summary>
    [Fact]
    public void Matches_BothSides_RequiresBoth()
    {
        const ModifierSet Both = ModifierSet.LCtrl | ModifierSet.RCtrl;

        Assert.True(ModifierSets.Matches(Both, Both));
        Assert.False(ModifierSets.Matches(Both, ModifierSet.LCtrl));
    }

    /// <summary>«Любой Ctrl» принимает левый, правый или оба, но не отсутствие.</summary>
    [Theory]
    [InlineData((int)(ModifierSet.LCtrl), true)]
    [InlineData((int)(ModifierSet.RCtrl), true)]
    [InlineData((int)(ModifierSet.LCtrl | ModifierSet.RCtrl), true)]
    [InlineData((int)(ModifierSet.None), false)]
    [InlineData((int)(ModifierSet.LShift), false)]
    public void Matches_AnyCtrl_AcceptsEitherSide(int held, bool expected)
    {
        Assert.Equal(expected, ModifierSets.Matches(ModifierSet.AnyCtrl, (ModifierSet)held));
    }

    /// <summary>Пустое требование не принимает лишних зажатых модификаторов.</summary>
    [Fact]
    public void Matches_NoRequirement_RejectsExtraModifier()
    {
        Assert.True(ModifierSets.Matches(ModifierSet.None, ModifierSet.None));
        Assert.False(ModifierSets.Matches(ModifierSet.None, ModifierSet.LAlt));
    }

    /// <summary>«Любой» и сторона одного семейства вместе недопустимы.</summary>
    [Fact]
    public void IsValid_AnyMixedWithSide_IsFalse()
    {
        Assert.False(ModifierSets.IsValid(ModifierSet.AnyCtrl | ModifierSet.LCtrl));
        Assert.True(ModifierSets.IsValid(ModifierSet.AnyCtrl | ModifierSet.LShift));
    }

    /// <summary>Перевод клавиши в бит и обратно согласован.</summary>
    [Fact]
    public void ToKey_FromKey_RoundTrips()
    {
        foreach (var key in ModifierSets.SidedKeys)
        {
            Assert.Equal(key, ModifierSets.ToKey(ModifierSets.FromKey(key)));
        }

        Assert.False(ModifierSets.IsModifier(VirtualKey.Space));
    }
}
