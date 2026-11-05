using System.Text.Json.Nodes;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SchemaMigrator"/>: старые версии схемы приводятся к текущей до разбора (SPEC F8).</summary>
public sealed class SchemaMigratorTests
{
    /// <summary>Мигратор «текущая версия 3» с миграциями, которые оставляют след в поле <c>trace</c>.</summary>
    private static SchemaMigrator Chain() => new(3, new Dictionary<int, Action<JsonObject>>
    {
        [1] = root => root["trace"] = root["trace"]?.GetValue<string>() + "1",
        [2] = root => root["trace"] = root["trace"]?.GetValue<string>() + "2",
    });

    /// <summary>Файл без версии считается версией 1 и проходит всю цепочку.</summary>
    [Fact]
    public void Migrate_NoVersion_RunsWholeChain()
    {
        var result = JsonNode.Parse(Chain().Migrate("""{ "trace": "" }"""))!;

        Assert.Equal("12", result["trace"]!.GetValue<string>());
        Assert.Equal(3, result["schemaVersion"]!.GetValue<int>());
    }

    /// <summary>Версия 2 проходит только последнюю миграцию.</summary>
    [Fact]
    public void Migrate_VersionTwo_RunsLastStepOnly()
    {
        var result = JsonNode.Parse(Chain().Migrate("""{ "schemaVersion": 2, "trace": "" }"""))!;

        Assert.Equal("2", result["trace"]!.GetValue<string>());
    }

    /// <summary>Текущая версия, более новая версия и не JSON возвращаются как есть.</summary>
    [Theory]
    [InlineData("""{ "schemaVersion": 3 } // comment""")]
    [InlineData("""{ "schemaVersion": 9 }""")]
    [InlineData("{ broken")]
    public void Migrate_NothingToDo_ReturnsSameText(string json) => Assert.Equal(json, Chain().Migrate(json));

    /// <summary>Комментарии и висячие запятые в старом файле не мешают миграции.</summary>
    [Fact]
    public void Migrate_CommentsAndTrailingCommas_AreAccepted()
    {
        var result = JsonNode.Parse(Chain().Migrate("{ \"trace\": \"\", // note\n}"))!;

        Assert.Equal("12", result["trace"]!.GetValue<string>());
    }

    /// <summary>Версия читается без изменения текста; для не объекта — <c>null</c>.</summary>
    [Fact]
    public void ReadVersion_VariousInputs_ReturnsVersionOrNull()
    {
        Assert.Equal(7, SchemaMigrator.ReadVersion("""{ "schemaVersion": 7 }"""));
        Assert.Equal(1, SchemaMigrator.ReadVersion("{}"));
        Assert.Null(SchemaMigrator.ReadVersion("[1]"));
        Assert.Null(SchemaMigrator.ReadVersion("{ broken"));
    }

    /// <summary>Обычный разбор настроек проходит через мигратор и не ломает файл текущей версии.</summary>
    [Fact]
    public void Parse_CurrentVersion_StillWorks()
    {
        var result = SettingsJson.Parse("""{ "schemaVersion": 1, "wordBufferLimit": 300 }""");

        Assert.Equal(300, result.Settings!.WordBufferLimit);
    }
}
