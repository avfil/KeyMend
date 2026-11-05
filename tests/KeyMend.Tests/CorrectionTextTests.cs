using KeyMend.Input;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="CorrectionText"/> и <see cref="TerminalWindows"/>: правила обработки забранного из окна текста.</summary>
public sealed class CorrectionTextTests
{
    /// <summary>Разделитель строки Unicode (U+2028).</summary>
    private static readonly string _lineSeparator = ((char)0x2028).ToString();

    /// <summary>Разделитель абзаца Unicode (U+2029).</summary>
    private static readonly string _paragraphSeparator = ((char)0x2029).ToString();

    /// <summary>Неразрывный пробел (U+00A0).</summary>
    private static readonly string _nonBreakingSpace = ((char)0x00A0).ToString();

    /// <summary>Хвост после последнего перевода строки любого вида.</summary>
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("one\ntwo", "two")]
    [InlineData("one\r\ntwo three", "two three")]
    [InlineData("one\rtwo", "two")]
    [InlineData("one\v two", " two")]
    [InlineData("one\n", "")]
    [InlineData("", "")]
    [InlineData("a\nb\nc", "c")]
    public void LineTail_TextWithBreaks_ReturnsTextAfterLastBreak(string text, string expected) =>
        Assert.Equal(expected, CorrectionText.LineTail(text));

    /// <summary>Разделители строки и абзаца Unicode тоже завершают строку.</summary>
    [Fact]
    public void LineTail_UnicodeSeparators_AreBreaks()
    {
        Assert.Equal("two", CorrectionText.LineTail("one" + _lineSeparator + "two"));
        Assert.Equal("two", CorrectionText.LineTail("one" + _paragraphSeparator + "two"));
        Assert.True(CorrectionText.HasLineBreak("a" + _lineSeparator + "b"));
        Assert.True(CorrectionText.HasLineBreak("a" + _paragraphSeparator + "b"));
    }

    /// <summary>Перевод строки находится в любом виде.</summary>
    [Theory]
    [InlineData("abc", false)]
    [InlineData("a\nb", true)]
    [InlineData("a\r\nb", true)]
    [InlineData("a\rb", true)]
    [InlineData("a b\tc", false)]
    public void HasLineBreak_Text_DetectsBreaks(string text, bool expected) =>
        Assert.Equal(expected, CorrectionText.HasLineBreak(text));

    /// <summary>Пустой текст и одни пробелы исправлять не нужно.</summary>
    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData(" \t ", true)]
    [InlineData(" a ", false)]
    public void IsBlank_Text_DetectsEmptyAndSpaces(string text, bool expected) =>
        Assert.Equal(expected, CorrectionText.IsBlank(text));

    /// <summary>Неразрывный пробел тоже считается пробелом.</summary>
    [Fact]
    public void IsBlank_NonBreakingSpace_IsBlank() =>
        Assert.True(CorrectionText.IsBlank(_nonBreakingSpace));

    /// <summary>Строка целиком (Sublime, VS Code): один перевод строки в конце.</summary>
    [Theory]
    [InlineData("hello world\n", true)]
    [InlineData("hello world\r\n", true)]
    [InlineData("hello world\r", true)]
    [InlineData("\n", true)]
    [InlineData("hello world", false)]
    [InlineData("one\ntwo\n", false)]
    [InlineData("one\r\ntwo\r\n", false)]
    [InlineData("one\ntwo", false)]
    [InlineData("\n\n", false)]
    [InlineData("", false)]
    public void LooksLikeCopiedLine_Text_DetectsSingleTrailingBreak(string text, bool expected) =>
        Assert.Equal(expected, CorrectionText.LooksLikeCopiedLine(text));

    /// <summary>CRLF, суррогатная пара и символ с комбинируемым знаком дают один шаг каретки.</summary>
    [Fact]
    public void CaretSteps_Text_CountsGraphemes()
    {
        var combining = "e" + (char)0x0301 + "x";

        Assert.Equal(0, CorrectionText.CaretSteps(string.Empty));
        Assert.Equal(3, CorrectionText.CaretSteps("abc"));
        Assert.Equal(3, CorrectionText.CaretSteps("a\r\nb"));
        Assert.Equal(3, CorrectionText.CaretSteps("a\nb"));
        Assert.Equal(3, CorrectionText.CaretSteps("a" + char.ConvertFromUtf32(0x1F600) + "b"));
        Assert.Equal(2, CorrectionText.CaretSteps(combining));
    }

    /// <summary>Классы консольных окон распознаются, обычные окна — нет.</summary>
    [Theory]
    [InlineData("ConsoleWindowClass", true)]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS", true)]
    [InlineData("mintty", true)]
    [InlineData("VirtualConsoleClass", true)]
    [InlineData("Notepad", false)]
    [InlineData("Chrome_WidgetWin_1", false)]
    [InlineData("consolewindowclass", false)]
    [InlineData("", false)]
    public void IsTerminal_ClassName_MatchesKnownConsoles(string className, bool expected) =>
        Assert.Equal(expected, TerminalWindows.IsTerminal(className));
}
