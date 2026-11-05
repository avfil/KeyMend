using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SnippetList"/>: добавление, замена и удаление сокращений из окна.</summary>
public sealed class SnippetListTests
{
    /// <summary>Сокращение для тестов.</summary>
    private static Snippet Make(string id, string keys) => new() { Id = id, Keys = keys, Text = "test-text" };

    /// <summary>Новое сокращение без идентификатора получает свободный <c>a-…</c> и встаёт в конец.</summary>
    [Fact]
    public void Upsert_New_AppendsWithGeneratedId()
    {
        IReadOnlyList<Snippet> list = [Make("dd", "дд ")];

        var result = SnippetList.Upsert(list, null, Make(string.Empty, "gm "), Guid.NewGuid);

        Assert.Equal(2, result.Count);
        Assert.StartsWith("a-", result[1].Id, StringComparison.Ordinal);
        Assert.Equal("gm ", result[1].Keys);
    }

    /// <summary>Правка заменяет сокращение на месте, найдя его по идентификатору.</summary>
    [Fact]
    public void Upsert_Existing_ReplacesInPlace()
    {
        IReadOnlyList<Snippet> list = [Make("dd", "дд "), Make("gm", "gm ")];

        var result = SnippetList.Upsert(list, list[0], Make("dd", "sp "), Guid.NewGuid);

        Assert.Equal(["sp ", "gm "], result.Select(s => s.Keys));
    }

    /// <summary>Удаление убирает только своё сокращение; чужое не трогает.</summary>
    [Fact]
    public void Remove_Existing_DropsIt()
    {
        IReadOnlyList<Snippet> list = [Make("dd", "дд "), Make("gm", "gm ")];

        Assert.Equal(["gm"], SnippetList.Remove(list, list[0]).Select(s => s.Id));
        Assert.Same(list, SnippetList.Remove(list, Make("none", "нет ")));
    }
}
