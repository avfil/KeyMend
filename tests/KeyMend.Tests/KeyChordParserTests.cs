using KeyMend.Input;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="KeyChordParser"/>: разбор, каноническая запись и отказы на некорректных строках.</summary>
public sealed class KeyChordParserTests
{
    /// <summary>Разбор и обратная запись дают каноническую форму.</summary>
    [Theory]
    [InlineData("RCtrl", "RCtrl")]
    [InlineData("rctrl", "RCtrl")]
    [InlineData("RShift+LShift", "LShift+RShift")]
    [InlineData("RCtrl+LCtrl", "LCtrl+RCtrl")]
    [InlineData("Ctrl+RShift", "Ctrl+RShift")]
    [InlineData("shift+ctrl+space", "Ctrl+Shift+Space")]
    [InlineData("CapsLock", "CapsLock")]
    [InlineData("f18", "F18")]
    [InlineData("Win+a", "Win+A")]
    [InlineData("Alt+Return", "Alt+Enter")]
    [InlineData("Ctrl + Alt + 5", "Ctrl+Alt+5")]
    [InlineData("LCtrl+RCtrl+1", "LCtrl+RCtrl+1")]
    [InlineData("RCtrl+PgUp", "RCtrl+PgUp")]
    [InlineData("0x7C", "F13")]
    [InlineData("0xFD", "0xFD")]
    public void RoundTrip_ValidInput_ProducesCanonicalForm(string input, string expected)
    {
        Assert.True(KeyChordParser.TryParse(input, out var chord));
        Assert.Equal(expected, KeyChordParser.Format(chord));
    }

    /// <summary>Модификаторы и основная клавиша разделяются.</summary>
    [Fact]
    public void TryParse_ModifiersAndKey_SplitsThem()
    {
        Assert.True(KeyChordParser.TryParse("LCtrl+RCtrl+1", out var chord));

        Assert.Equal(ModifierSet.LCtrl | ModifierSet.RCtrl, chord.Modifiers);
        Assert.Equal((VirtualKey)'1', chord.Key);
    }

    /// <summary>Строка из одних модификаторов не имеет основной клавиши.</summary>
    [Fact]
    public void TryParse_OnlyModifiers_HasNoKey()
    {
        Assert.True(KeyChordParser.TryParse("Ctrl+RShift", out var chord));

        Assert.Equal(ModifierSet.AnyCtrl | ModifierSet.RShift, chord.Modifiers);
        Assert.Equal(VirtualKey.None, chord.Key);
    }

    /// <summary>Некорректные строки отвергаются.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Ctrl+A")]
    [InlineData("A+B")]
    [InlineData("Ctrl+LCtrl")]
    [InlineData("RCtrl+RCtrl")]
    [InlineData("Ctrl+")]
    [InlineData("Banana")]
    [InlineData("F25")]
    [InlineData("0x00")]
    [InlineData("0xFF")]
    [InlineData("0xA3")]
    [InlineData("0x11")]
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        Assert.False(KeyChordParser.TryParse(input, out _));
    }
}
