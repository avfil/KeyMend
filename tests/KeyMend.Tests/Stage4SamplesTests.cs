using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Нейтральные конфиги этапа 4 (<c>TestData/stage4</c>) должны читаться без замечаний.</summary>
public sealed class Stage4SamplesTests
{
    /// <summary>Имена нейтральных конфигов из папки тестовых данных.</summary>
    /// <returns>Имена файлов без пути.</returns>
    public static TheoryData<string> Files()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "stage4");
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    /// <summary>Конфиг читается без предупреждений и содержит правила.</summary>
    /// <param name="file">Имя файла.</param>
    [Theory]
    [MemberData(nameof(Files))]
    public void Parse_Stage4Config_HasRulesAndNoWarnings(string file)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "stage4", file));

        var parsed = SettingsJson.Parse(json);

        Assert.Empty(parsed.Warnings);
        Assert.NotNull(parsed.Settings);
        Assert.True(parsed.Settings.Rules.Count >= 2, file);
    }
}
