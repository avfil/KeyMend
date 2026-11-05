using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Разбор текста подсказок «?»: абзацы, переносы, полужирный.</summary>
public sealed class HelpTextTests
{
    /// <summary>Пустой текст не даёт абзацев.</summary>
    [Fact]
    public void Parse_EmptyText_ReturnsNoParagraphs() => Assert.Empty(HelpText.Parse("  \n \n"));

    /// <summary>Одна строка — один абзац из одного куска.</summary>
    [Fact]
    public void Parse_OneLine_ReturnsOneParagraphWithOneRun()
    {
        var paragraph = Assert.Single(HelpText.Parse("Просто текст"));

        Assert.Equal([new HelpRun("Просто текст")], paragraph.Runs);
    }

    /// <summary>Пустая строка делит текст на абзацы.</summary>
    [Fact]
    public void Parse_BlankLine_SplitsParagraphs() => Assert.Equal(2, HelpText.Parse("Один\n\nДва").Count);

    /// <summary>Пустая строка в виде CRLF тоже делит на абзацы.</summary>
    [Fact]
    public void Parse_CrLfBlankLine_SplitsParagraphs() => Assert.Equal(2, HelpText.Parse("Один\r\n\r\nДва").Count);

    /// <summary>Одиночный перевод строки — перенос внутри абзаца.</summary>
    [Fact]
    public void Parse_SingleNewline_MakesLineBreakInsideParagraph()
    {
        var paragraph = Assert.Single(HelpText.Parse("Раз\nДва"));

        Assert.Equal([new HelpRun("Раз"), new HelpRun(string.Empty, LineBreak: true), new HelpRun("Два")], paragraph.Runs);
    }

    /// <summary>Выделение посреди строки даёт три куска.</summary>
    [Fact]
    public void Parse_BoldInMiddle_SplitsIntoThreeRuns()
    {
        var paragraph = Assert.Single(HelpText.Parse("до **важно** после"));

        Assert.Equal([new HelpRun("до "), new HelpRun("важно", Bold: true), new HelpRun(" после")], paragraph.Runs);
    }

    /// <summary>Непарный маркер остаётся обычным текстом.</summary>
    [Fact]
    public void Parse_UnclosedBold_StaysPlainText()
    {
        var paragraph = Assert.Single(HelpText.Parse("до **без конца"));

        Assert.Equal([new HelpRun("до **без конца")], paragraph.Runs);
    }

    /// <summary>Текст для читалок экрана без разметки.</summary>
    [Fact]
    public void Plain_RemovesMarkup() => Assert.Equal("a b c d", HelpText.Plain("a **b**\nc\n\nd"));
}
