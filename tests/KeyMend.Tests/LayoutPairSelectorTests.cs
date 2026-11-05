using KeyMend.Layouts;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="LayoutPairSelector"/> и <see cref="LayoutTags"/>: выбор раскладки без обращения к WinAPI.</summary>
public sealed class LayoutPairSelectorTests
{
    /// <summary>HKL русской раскладки (устройство 0x0419, язык 0x0419).</summary>
    private static readonly nint _ru = 0x04190419;

    /// <summary>HKL английской (США) раскладки.</summary>
    private static readonly nint _en = 0x04090409;

    /// <summary>HKL украинской раскладки.</summary>
    private static readonly nint _uk = 0x04220422;

    /// <summary>Идентификатор русского языка.</summary>
    internal const int Ru = 0x0419;

    /// <summary>Идентификатор английского (США) языка.</summary>
    internal const int En = 0x0409;

    /// <summary>Из русской переключаемся на английскую.</summary>
    [Fact]
    public void PickPair_CurrentIsA_ReturnsB()
    {
        Assert.Equal(_en, LayoutPairSelector.PickPair([_ru, _en], _ru, Ru, En));
    }

    /// <summary>Из английской переключаемся на русскую.</summary>
    [Fact]
    public void PickPair_CurrentIsB_ReturnsA()
    {
        Assert.Equal(_ru, LayoutPairSelector.PickPair([_ru, _en], _en, Ru, En));
    }

    /// <summary>Из постороннего языка переходим на первый язык пары.</summary>
    [Fact]
    public void PickPair_CurrentIsOther_ReturnsA()
    {
        Assert.Equal(_ru, LayoutPairSelector.PickPair([_ru, _en, _uk], _uk, Ru, En));
    }

    /// <summary>Если первого языка пары нет, из постороннего переходим на второй.</summary>
    [Fact]
    public void PickPair_CurrentIsOtherAndANotInstalled_ReturnsB()
    {
        Assert.Equal(_en, LayoutPairSelector.PickPair([_en, _uk], _uk, Ru, En));
    }

    /// <summary>Нужной раскладки нет — переключать некуда.</summary>
    [Fact]
    public void PickPair_TargetNotInstalled_ReturnsZero()
    {
        Assert.Equal(0, LayoutPairSelector.PickPair([_ru], _ru, Ru, En));
    }

    /// <summary>Конкретная раскладка берётся из установленных.</summary>
    [Fact]
    public void PickSpecific_Installed_ReturnsIt()
    {
        Assert.Equal(_uk, LayoutPairSelector.PickSpecific([_ru, _en, _uk], 0x0422));
        Assert.Equal(0, LayoutPairSelector.PickSpecific([_ru, _en], 0x0422));
    }

    /// <summary>Из нескольких раскладок одного языка берётся первая в порядке системы.</summary>
    [Fact]
    public void PickSpecific_SeveralOfSameLanguage_ReturnsFirst()
    {
        nint ruMnemonic = 0x0002_0419;

        Assert.Equal(_ru, LayoutPairSelector.PickSpecific([_ru, ruMnemonic], Ru));
    }

    /// <summary>Теги языков переводятся в идентификаторы языка раскладок.</summary>
    [Theory]
    [InlineData("ru-RU", 0x0419)]
    [InlineData("en-US", 0x0409)]
    [InlineData("uk-UA", 0x0422)]
    public void TryGetLanguageId_SpecificCulture_ReturnsLanguageId(string tag, int expected)
    {
        Assert.True(LayoutTags.TryGetLanguageId(tag, out var id));
        Assert.Equal(expected, id);
    }

    /// <summary>Пустые, нейтральные и неизвестные теги отвергаются.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ru")]
    [InlineData("xx-YY-nonsense")]
    public void TryGetLanguageId_Invalid_ReturnsFalse(string? tag)
    {
        Assert.False(LayoutTags.TryGetLanguageId(tag, out _));
    }
}
