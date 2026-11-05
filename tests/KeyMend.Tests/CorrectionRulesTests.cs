using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты модели правил исправления: шаги в JSON, границы полей, категория для исключений, правила по умолчанию.</summary>
public sealed class CorrectionRulesTests
{
    /// <summary>Настройки с правилами исправления, как их мог бы написать владелец.</summary>
    private const string _rulesJson = """
        {
          "schemaVersion": 1,
          "rules": [
            { "id": "fix-selection", "name": "Выделенное", "trigger": { "kind": "Tap", "keys": "RShift", "maxHoldMs": 300 },
              "steps": [ { "kind": "Correct", "scope": "Selection", "pair": ["ru-RU", "en-US"], "maxChars": 500, "switchLayout": false, "caretAfter": "Right" } ] },
            { "id": "fix-line", "name": "До начала строки", "trigger": { "kind": "Tap", "keys": "RShift", "hold": "RCtrl" },
              "steps": [ { "kind": "Correct", "scope": "Line" } ] }
          ]
        }
        """;

    /// <summary>Шаги исправления читаются из JSON, поля по умолчанию заполняются.</summary>
    [Fact]
    public void Parse_CorrectionSteps_AreRead()
    {
        var result = SettingsJson.Parse(_rulesJson);

        Assert.NotNull(result.Settings);
        Assert.Empty(result.Warnings);
        var selection = Assert.IsType<CorrectStep>(Assert.Single(result.Settings.Rules[0].Steps));
        Assert.Equal(CorrectScope.Selection, selection.Scope);
        Assert.Equal(["ru-RU", "en-US"], selection.Pair);
        Assert.Equal(500, selection.MaxChars);
        Assert.False(selection.SwitchLayout);
        Assert.Equal(CaretAfter.Right, selection.CaretAfter);

        var line = Assert.IsType<CorrectStep>(Assert.Single(result.Settings.Rules[1].Steps));
        Assert.Equal(CorrectScope.Line, line.Scope);
        Assert.Equal(CorrectStep.DefaultMaxChars, line.MaxChars);
        Assert.True(line.SwitchLayout);
        Assert.Equal(CaretAfter.AsIs, line.CaretAfter);
    }

    /// <summary>Шаги исправления переживают запись и повторное чтение.</summary>
    [Fact]
    public void Serialize_CorrectionSteps_RoundTrip()
    {
        var settings = SettingsJson.Parse(_rulesJson).Settings;
        Assert.NotNull(settings);

        var again = SettingsJson.Parse(SettingsJson.Serialize(settings)).Settings;

        Assert.NotNull(again);
        var selection = Assert.IsType<CorrectStep>(Assert.Single(again.Rules[0].Steps));
        Assert.Equal(500, selection.MaxChars);
        Assert.False(selection.SwitchLayout);
        Assert.Equal(CaretAfter.Right, selection.CaretAfter);
        Assert.Equal(CorrectScope.Line, Assert.IsType<CorrectStep>(Assert.Single(again.Rules[1].Steps)).Scope);
    }

    /// <summary>Лимит длины вне границ приводится к ближайшей.</summary>
    [Theory]
    [InlineData(0, CorrectStep.MinChars)]
    [InlineData(-5, CorrectStep.MinChars)]
    [InlineData(5_000_000, CorrectStep.MaxCharsLimit)]
    [InlineData(12_345, 12_345)]
    public void MaxChars_OutOfRange_IsClamped(int value, int expected)
    {
        var step = new CorrectStep { Scope = CorrectScope.Line, MaxChars = value };

        Assert.Equal(expected, step.MaxChars);
    }

    /// <summary>Устаревшие правила (слово и отмена) пропускаются с предупреждением, остальные работают.</summary>
    [Fact]
    public void Parse_LegacyWordAndUndoRules_AreSkipped()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "rules": [
                { "id": "fix-word", "name": "Слово", "trigger": { "kind": "Tap", "keys": "RShift" },
                  "steps": [ { "kind": "Correct", "scope": "Word" } ] },
                { "id": "fix-undo", "name": "Отмена", "trigger": { "kind": "Tap", "keys": "RShift", "hold": "LCtrl" },
                  "steps": [ { "kind": "UndoCorrection" } ] },
                { "id": "fix-line", "name": "Строка", "trigger": { "kind": "Tap", "keys": "RShift", "hold": "RCtrl" },
                  "steps": [ { "kind": "Correct", "scope": "Line" } ] }
              ]
            }
            """;

        var result = SettingsJson.Parse(json);

        Assert.NotNull(result.Settings);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Equal("Строка", Assert.Single(result.Settings.Rules).Name);
    }

    /// <summary>Правила с шагами исправления относятся к функции «исправление» (исключения программ, SPEC F7).</summary>
    [Fact]
    public void CategoryOf_CorrectionSteps_IsCorrection()
    {
        var trigger = new TapTrigger { Keys = ModifierSet.RShift };
        var correct = new Rule { Name = "c", Trigger = trigger, Steps = [new CorrectStep { Scope = CorrectScope.Line }] };

        Assert.Equal(RuleCategory.Correction, RuleFilter.CategoryOf(correct));
    }

    /// <summary>Исключение с отключённым исправлением убирает правила исправления, но не переключение языка.</summary>
    [Fact]
    public void Select_CorrectionDisabledForProcess_DropsOnlyCorrectionRules()
    {
        var trigger = new TapTrigger { Keys = ModifierSet.RShift };
        Rule[] rules =
        [
            new() { Name = "c", Trigger = trigger, Steps = [new CorrectStep { Scope = CorrectScope.Selection }] },
            new() { Name = "l", Trigger = trigger, Steps = [new SwitchLayoutStep()] },
        ];
        ExclusionEntry[] exclusions = [new() { Process = "mstsc.exe", Correction = false }];

        var selected = RuleFilter.Select(rules, "mstsc.exe", exclusions, paused: false);

        Assert.Equal("l", Assert.Single(selected).Name);
    }

    /// <summary>Правила исправления по умолчанию: выделенное — RShift, до начала строки — RCtrl + RShift, порог 300 мс.</summary>
    [Fact]
    public void DefaultSettings_CorrectionRules_UseSharedGestureTimeout()
    {
        var rules = DefaultSettings.Create().Rules;

        var selection = Assert.IsType<TapTrigger>(rules.Single(r => r.Id == "fix-selection").Trigger);
        var line = Assert.IsType<TapTrigger>(rules.Single(r => r.Id == "fix-line").Trigger);
        Assert.Equal(ModifierSet.RShift, selection.Keys);
        Assert.Equal(ModifierSet.None, selection.Hold);
        Assert.Equal(300, selection.MaxHoldMs);
        Assert.Equal(ModifierSet.RShift, line.Keys);
        Assert.Equal(ModifierSet.RCtrl, line.Hold);
        Assert.Equal(300, line.MaxHoldMs);
        Assert.DoesNotContain(rules, r => r.Id is "fix-word" or "fix-undo");
    }
}
