using KeyMend.Infrastructure;

namespace KeyMend.Tests;

/// <summary>Тесты чистой части <see cref="Win32Clipboard"/>: какие форматы буфера обмена копировать. Сам буфер проверяется вручную.</summary>
public sealed class Win32ClipboardTests
{
    /// <summary>Форматы с объектами GDI и «отображения» передавать как память нельзя.</summary>
    [Theory]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(9u)]
    [InlineData(14u)]
    [InlineData(0x80u)]
    [InlineData(0x81u)]
    [InlineData(0x8Eu)]
    [InlineData(0x200u)]
    [InlineData(0x3FFu)]
    public void ShouldSkip_GdiAndDisplayFormats_ReturnsTrue(uint format) =>
        Assert.True(Win32Clipboard.ShouldSkip(format, hasUnicodeText: false));

    /// <summary>Текст Unicode, DIB, список файлов и зарегистрированные форматы копируются.</summary>
    [Theory]
    [InlineData(13u)]
    [InlineData(8u)]
    [InlineData(15u)]
    [InlineData(17u)]
    [InlineData(0xC001u)]
    public void ShouldSkip_MemoryFormats_ReturnsFalse(uint format) =>
        Assert.False(Win32Clipboard.ShouldSkip(format, hasUnicodeText: true));

    /// <summary>CF_TEXT и CF_OEMTEXT — синтез из Unicode-текста: при его наличии не копируются, без него — копируются.</summary>
    [Theory]
    [InlineData(1u)]
    [InlineData(7u)]
    public void ShouldSkip_SynthesizedText_DependsOnUnicodeText(uint format)
    {
        Assert.True(Win32Clipboard.ShouldSkip(format, hasUnicodeText: true));
        Assert.False(Win32Clipboard.ShouldSkip(format, hasUnicodeText: false));
    }
}
