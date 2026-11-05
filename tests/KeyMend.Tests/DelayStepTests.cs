using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты шага «Пауза» (SPEC 3.2): границы и чтение из JSON.</summary>
public sealed class DelayStepTests
{
    /// <summary>Длительность вне границ приводится к ближайшей.</summary>
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(250, 250)]
    [InlineData(1_000_000, DelayStep.MaxMs)]
    public void Ms_OutOfRange_IsClamped(int value, int expected) =>
        Assert.Equal(expected, new DelayStep { Ms = value }.Ms);

    /// <summary>Шаг читается из JSON по <c>kind</c>.</summary>
    [Fact]
    public void Parse_DelayStep_ReadsMs()
    {
        const string Json = """
            { "rules": [ { "id": "r", "name": "r", "trigger": { "kind": "Press", "keys": "F13" },
              "steps": [ { "kind": "Delay", "ms": 250 } ] } ] }
            """;

        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings);

        var step = Assert.IsType<DelayStep>(Assert.Single(Assert.Single(settings.Rules).Steps));
        Assert.Equal(250, step.Ms);
    }
}
