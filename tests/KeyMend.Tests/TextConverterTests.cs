using KeyMend.Input;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TextConverter"/>: исправление готового текста между раскладками (F3.1в).</summary>
public sealed class TextConverterTests
{
    /// <summary>Конвертирует между US (первая) и RU (вторая).</summary>
    private static TextConversion? Run(string text) =>
        TextConverter.Convert(text, FallbackLayouts.Us, FallbackLayouts.Ru);

    /// <summary>Текст не в той раскладке печатается в другой.</summary>
    [Theory]
    [InlineData("Ьшскщыщае", "Microsoft", false)]
    [InlineData("hf,jnftn", "работает", true)]
    [InlineData("Ghbdtn", "Привет", true)]
    [InlineData("Руддщ цщкдв!", "Hello world!", false)]
    [InlineData("Руддщ, цщкдв", "Hello? world", false)]
    public void Convert_WrongLayoutText_GivesOtherLayout(string text, string expected, bool fromFirst)
    {
        var result = Run(text);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.Text);
        Assert.Equal(fromFirst, result.Value.FromFirst);
    }

    /// <summary>Переводы строк и табуляция сохраняются.</summary>
    [Fact]
    public void Convert_LineBreaksAndTabs_AreKept()
    {
        var result = Run("ghbdtn\r\n\tvbh");
        Assert.NotNull(result);
        Assert.Equal("привет\r\n\tмир", result.Value.Text);
    }

    /// <summary>Символы без клавиши остаются как есть.</summary>
    [Fact]
    public void Convert_CharactersWithoutKey_AreKept()
    {
        var result = Run("ghbdtn — 😀");
        Assert.NotNull(result);
        Assert.Equal("привет — 😀", result.Value.Text);
    }

    /// <summary>В смешанном тексте исходной считается раскладка с большим числом букв.</summary>
    [Fact]
    public void Convert_MixedText_ConvertsTheLargerLayoutPart()
    {
        var result = Run("Microsoft hf,jnftn");
        Assert.NotNull(result);
        Assert.True(result.Value.FromFirst);
    }

    /// <summary>Букв поровну — исправлять нечего.</summary>
    [Fact]
    public void Convert_EqualLetterCount_ReturnsNull() =>
        Assert.Null(Run("ab ты"));

    /// <summary>При равенстве букв направление задаёт подсказка (текущая раскладка окна).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Convert_EqualLetterCountWithHint_UsesHint(bool fromFirst)
    {
        var result = TextConverter.Convert("ab ты", FallbackLayouts.Us, FallbackLayouts.Ru, tieFromFirst: fromFirst);
        Assert.NotNull(result);
        Assert.Equal(fromFirst, result.Value.FromFirst);
    }

    /// <summary>Без букв подсказка не помогает.</summary>
    [Fact]
    public void Convert_NoLettersWithHint_ReturnsNull() =>
        Assert.Null(TextConverter.Convert("123 456", FallbackLayouts.Us, FallbackLayouts.Ru, tieFromFirst: true));

    /// <summary>Без букв направление неизвестно.</summary>
    [Fact]
    public void Convert_NoLetters_ReturnsNull() =>
        Assert.Null(Run("123 456"));

    /// <summary>Мёртвая клавиша в целевой раскладке отменяет исправление.</summary>
    [Fact]
    public void Convert_TargetKeyIsDead_ReturnsNull()
    {
        var target = new LayoutTable();
        target.Set((VirtualKey)0x41, false, false, Translation.Dead);
        var source = new LayoutTable();
        source.Set((VirtualKey)0x41, false, false, Translation.Of('a'));
        Assert.Null(TextConverter.Convert("a", source, target));
    }
}
