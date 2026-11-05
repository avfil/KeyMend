using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты чтения шага «Питание» и служебного шага отмены таймера из <c>settings.json</c> (SPEC 7.3).</summary>
public sealed class PowerStepSettingsTests
{
    /// <summary>Шаг читается целиком; задержка вне границ приводится к допустимой.</summary>
    [Fact]
    public void Parse_PowerStep_ReadsFieldsAndClampsMinutes()
    {
        const string Json = """
            { "rules": [ { "id": "p", "name": "p", "trigger": { "kind": "Press", "keys": "F15" },
              "steps": [ { "kind": "Power", "action": "Shutdown", "afterMinutes": 99999, "countdown": false } ] } ] }
            """;

        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings);

        var step = Assert.IsType<PowerStep>(Assert.Single(Assert.Single(settings.Rules).Steps));
        Assert.Equal(PowerAction.Shutdown, step.Action);
        Assert.Equal(PowerStep.MaxAfterMinutes, step.AfterMinutes);
        Assert.False(step.Countdown);
    }

    /// <summary>Значения по умолчанию: сразу, с отсчётом.</summary>
    [Fact]
    public void Parse_MinimalPowerStep_UsesDefaults()
    {
        const string Json = """
            { "rules": [ { "id": "p", "name": "p", "trigger": { "kind": "Press", "keys": "F15" },
              "steps": [ { "kind": "Power", "action": "Sleep" }, { "kind": "Service", "action": "CancelPowerTimer" } ] } ] }
            """;

        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings);

        var steps = Assert.Single(settings.Rules).Steps;
        var power = Assert.IsType<PowerStep>(steps[0]);
        Assert.Equal(0, power.AfterMinutes);
        Assert.True(power.Countdown);
        Assert.Equal(ServiceAction.CancelPowerTimer, Assert.IsType<ServiceStep>(steps[1]).Action);
    }

    /// <summary>Шаг без действия пропускает правило как битое.</summary>
    [Fact]
    public void Parse_PowerWithoutAction_SkipsRule()
    {
        const string Json = """
            { "rules": [ { "id": "p", "name": "p", "trigger": { "kind": "Press", "keys": "F15" }, "steps": [ { "kind": "Power" } ] } ] }
            """;

        var result = SettingsJson.Parse(Json);

        Assert.Empty(Assert.IsType<AppSettings>(result.Settings).Rules);
        Assert.Single(result.Warnings);
    }
}
