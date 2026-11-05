using KeyMend.Rules;
using KeyMend.Text;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SnippetTable"/>: поиск, фильтр «Только секреты» и счётчики.</summary>
public sealed class SnippetTableTests
{
    /// <summary>Таблицы установленных раскладок: EN, затем RU.</summary>
    private static readonly LayoutTable[] _tables = [FallbackLayouts.Us, FallbackLayouts.Ru];

    /// <summary>Три сокращения: два обычных и секрет.</summary>
    private static readonly Snippet[] _snippets =
    [
        new() { Id = "dd", Keys = "дд ", Text = "Добрый день!" },
        new() { Id = "gm", Keys = "gm ", Text = "Good morning!" },
        new() { Id = "sec1", Keys = "sec1 ", Secret = true, SecretRef = "s-1" },
    ];

    /// <summary>Строит таблицу по строке поиска.</summary>
    private static SnippetTableView Build(string search = "", bool onlySecrets = false) =>
        SnippetTable.Build(_snippets, new SnippetTableFilter(search, onlySecrets), _tables);

    /// <summary>Без условий видны все строки, а счётчики считают сокращения и секреты.</summary>
    [Fact]
    public void Build_NoFilter_ShowsAllAndCounts()
    {
        var view = Build();

        Assert.Equal(["dd", "gm", "sec1"], view.Rows.Select(r => r.Snippet.Id));
        Assert.Equal(3, view.Total);
        Assert.Equal(1, view.Secrets);
    }

    /// <summary>Поиск находит по сокращению, по его виду в английской раскладке и по тексту, без учёта регистра.</summary>
    [Theory]
    [InlineData("ДД", "dd")]
    [InlineData("ll", "dd")]
    [InlineData("morn", "gm")]
    [InlineData("ыус1", "sec1")]
    public void Build_Search_MatchesKeysOtherLayoutAndText(string search, string id)
    {
        Assert.Equal(id, Assert.Single(Build(search).Rows).Snippet.Id);
    }

    /// <summary>Поиск ничего не находит, а счётчики остаются полными.</summary>
    [Fact]
    public void Build_NoMatch_EmptyRowsKeepsCounts()
    {
        var view = Build("zzz");

        Assert.Empty(view.Rows);
        Assert.Equal(3, view.Total);
    }

    /// <summary>«Только секреты» оставляет секреты.</summary>
    [Fact]
    public void Build_OnlySecrets_KeepsSecrets()
    {
        Assert.Equal("sec1", Assert.Single(Build(onlySecrets: true).Rows).Snippet.Id);
    }

    /// <summary>Пробел на конце сокращения виден как «␣».</summary>
    [Fact]
    public void DisplayKeys_TrailingSpace_ShowsVisibleMark()
    {
        Assert.Equal("дд␣", SnippetTable.DisplayKeys("дд "));
    }

    /// <summary>У строки есть вид в другой раскладке.</summary>
    [Fact]
    public void Build_Row_HasOtherLayout()
    {
        Assert.Equal("ll ", Build("дд").Rows[0].InOtherLayout);
    }
}
