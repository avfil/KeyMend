using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Правки списка правил и выдача идентификаторов.</summary>
public sealed class RuleListTests
{
    /// <summary>Правило для проверок.</summary>
    private static Rule Make(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Trigger = new TapTrigger { Keys = ModifierSet.RCtrl },
    };

    /// <summary>Источник «случайных» значений: числа по порядку.</summary>
    private static Func<Guid> Sequence()
    {
        var n = 0;
        return () => new Guid(++n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
    }

    /// <summary>Идентификатор выдаётся вида r-xxxxxxxx.</summary>
    [Fact]
    public void Next_Fresh_HasPrefixAndEightHex()
    {
        var id = RuleIds.Next([], Sequence());

        Assert.Equal("r-00000001", id);
    }

    /// <summary>Занятый идентификатор пропускается.</summary>
    [Fact]
    public void Next_Taken_PicksAnother()
    {
        var id = RuleIds.Next(["r-00000001"], Sequence());

        Assert.Equal("r-00000002", id);
    }

    /// <summary>Правка заменяет правило на его месте и сохраняет порядок.</summary>
    [Fact]
    public void Upsert_Existing_ReplacesInPlace()
    {
        var a = Make("a", "A");
        var b = Make("b", "B");

        var result = RuleList.Upsert([a, b], a, a with { Name = "A2" }, Sequence());

        Assert.Equal(["A2", "B"], result.Select(r => r.Name).ToArray());
    }

    /// <summary>Новое правило без идентификатора получает его и встаёт в конец.</summary>
    [Fact]
    public void Upsert_New_AppendsWithId()
    {
        var a = Make("a", "A");

        var result = RuleList.Upsert([a], null, Make(string.Empty, "Новое"), Sequence());

        Assert.Equal(2, result.Count);
        Assert.Equal("r-00000001", result[1].Id);
    }

    /// <summary>Правило без идентификатора ищется по ссылке.</summary>
    [Fact]
    public void Upsert_NoIdOriginal_FoundByReference()
    {
        var a = Make(string.Empty, "A");

        var result = RuleList.Upsert([a], a, a with { Name = "A2" }, Sequence());

        Assert.Equal("A2", Assert.Single(result).Name);
    }

    /// <summary>Удаление убирает только нужное правило.</summary>
    [Fact]
    public void Remove_Existing_DropsOnlyIt()
    {
        var a = Make("a", "A");
        var b = Make("b", "B");

        Assert.Equal(["B"], RuleList.Remove([a, b], a).Select(r => r.Name).ToArray());
    }

    /// <summary>Неизвестное правило список не меняет.</summary>
    [Fact]
    public void Remove_Unknown_KeepsList()
    {
        var a = Make("a", "A");

        Assert.Single(RuleList.Remove([a], Make("z", "Z")));
    }

    /// <summary>Копия встаёт сразу под оригиналом и получает новый идентификатор.</summary>
    [Fact]
    public void InsertAfter_Existing_PutsCopyBelowOriginal()
    {
        var a = Make("a", "A");
        var b = Make("b", "B");

        var result = RuleList.InsertAfter([a, b], a, a with { Id = string.Empty, Name = "A (копия)" }, Sequence());

        Assert.Equal(["A", "A (копия)", "B"], result.Select(r => r.Name).ToArray());
        Assert.Equal("r-00000001", result[1].Id);
    }
}
