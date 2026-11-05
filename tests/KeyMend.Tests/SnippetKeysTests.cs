using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SnippetKeys"/>: шаблоны клавиш, сравнение и вид сокращения в другой раскладке.</summary>
public sealed class SnippetKeysTests
{
    /// <summary>Таблицы установленных раскладок: EN, затем RU.</summary>
    private static readonly LayoutTable[] _tables = [FallbackLayouts.Us, FallbackLayouts.Ru];

    /// <summary>Признаки игнорирования Shift для этих таблиц.</summary>
    private static readonly bool[] _ignoreShift = SnippetKeys.BuildIgnoreShift(_tables);

    /// <summary>Русская и английская запись одних клавиш дают один шаблон.</summary>
    [Fact]
    public void Parse_SameKeysInTwoLayouts_GiveEqualPatterns()
    {
        var ru = SnippetKeys.Parse("дд ", _tables, _ignoreShift);
        var en = SnippetKeys.Parse("ll ", _tables, _ignoreShift);

        Assert.NotNull(ru);
        Assert.NotNull(en);
        Assert.Equal(SnippetRelation.Same, SnippetKeys.Compare(ru, en));
    }

    /// <summary>Пустая строка и символ, которого нет ни в одной раскладке, не разбираются.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("дд§")]
    public void Parse_EmptyOrUntypable_ReturnsNull(string text)
    {
        Assert.Null(SnippetKeys.Parse(text, _tables, _ignoreShift));
    }

    /// <summary>Короткое сокращение — начало длинного: сравнение видит это в обе стороны.</summary>
    [Fact]
    public void Compare_Prefix_ReportsBothDirections()
    {
        var shortOne = SnippetKeys.Parse("дд", _tables, _ignoreShift)!;
        var longOne = SnippetKeys.Parse("дд ", _tables, _ignoreShift)!;

        Assert.Equal(SnippetRelation.PrefixOf, SnippetKeys.Compare(shortOne, longOne));
        Assert.Equal(SnippetRelation.StartsWith, SnippetKeys.Compare(longOne, shortOne));
    }

    /// <summary>Разные клавиши не пересекаются.</summary>
    [Fact]
    public void Compare_DifferentKeys_ReturnsNone()
    {
        var a = SnippetKeys.Parse("дд ", _tables, _ignoreShift)!;
        var b = SnippetKeys.Parse("gm ", _tables, _ignoreShift)!;

        Assert.Equal(SnippetRelation.None, SnippetKeys.Compare(a, b));
    }

    /// <summary>Регистр буквы не различает сокращения.</summary>
    [Fact]
    public void Compare_LetterCase_IsIgnored()
    {
        var lower = SnippetKeys.Parse("дд ", _tables, _ignoreShift)!;
        var upper = SnippetKeys.Parse("ДД ", _tables, _ignoreShift)!;

        Assert.Equal(SnippetRelation.Same, SnippetKeys.Compare(lower, upper));
    }

    /// <summary>Русскому сокращению показывается английская запись и наоборот.</summary>
    [Theory]
    [InlineData("дд ", "ll ")]
    [InlineData("ll ", "дд ")]
    [InlineData("sec1 ", "ыус1 ")]
    public void InOtherLayout_Abbreviation_ShowsOppositeLayout(string text, string expected)
    {
        Assert.Equal(expected, SnippetKeys.InOtherLayout(text, _tables));
    }

    /// <summary>Строка, которая не разбирается, вида в другой раскладке не имеет.</summary>
    [Fact]
    public void InOtherLayout_Untypable_ReturnsNull()
    {
        Assert.Null(SnippetKeys.InOtherLayout("§", _tables));
    }
}
