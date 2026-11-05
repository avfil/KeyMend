using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SettingsJson"/>: схема v1 из SPEC 7.3, границы чисел, пропуск битых правил, ошибки.</summary>
public sealed class SettingsJsonTests
{
    /// <summary>Настройки по умолчанию переживают запись и чтение без потерь.</summary>
    [Fact]
    public void Serialize_Defaults_RoundTrips()
    {
        var defaults = DefaultSettings.Create();

        var result = SettingsJson.Parse(SettingsJson.Serialize(defaults));

        Assert.Null(result.Error);
        var parsed = Assert.IsType<AppSettings>(result.Settings);
        Assert.Equal(defaults.Rules.Count, parsed.Rules.Count);
        Assert.Equal(defaults.Rules[0].Trigger, parsed.Rules[0].Trigger);
        Assert.Equal(defaults.Rules[1].Trigger, parsed.Rules[1].Trigger);
        Assert.Equal("Язык (работа)", parsed.Rules[1].Name);
        Assert.False(parsed.Rules[1].Enabled);
        Assert.Equal(defaults.Exclusions, parsed.Exclusions);
    }

    /// <summary>Кириллица пишется как есть, без \u-экранирования.</summary>
    [Fact]
    public void Serialize_Cyrillic_IsNotEscaped()
    {
        var json = SettingsJson.Serialize(DefaultSettings.Create());

        Assert.Contains("Язык (работа)", json, StringComparison.Ordinal);
    }

    /// <summary>Пример из SPEC 7.3 читается: комментарии и висячие запятые допустимы.</summary>
    [Fact]
    public void Parse_SpecExampleWithCommentsAndTrailingCommas_Succeeds()
    {
        const string Json = """
            {
              // тема
              "schemaVersion": 1,
              "theme": "Dark",
              "rules": [
                { "id": "x", "name": "Тест", "enabled": true, "processes": ["chrome.exe"],
                  "trigger": { "kind": "Tap", "keys": "RShift", "hold": "RCtrl", "maxHoldMs": 120, },
                  "steps": [ { "kind": "SwitchLayout", "mode": "Specific", "layout": "en-US" }, ] },
                { "name": "Монитор", "trigger": { "kind": "Press", "keys": "F17", "doubleMs": 400 },
                  "steps": [ { "kind": "Service", "action": "TogglePause" } ] },
              ],
            }
            """;

        var result = SettingsJson.Parse(Json);

        Assert.Null(result.Error);
        var settings = Assert.IsType<AppSettings>(result.Settings);
        Assert.Equal(AppTheme.Dark, settings.Theme);
        var tap = Assert.IsType<TapTrigger>(settings.Rules[0].Trigger);
        Assert.Equal(ModifierSet.RShift, tap.Keys);
        Assert.Equal(ModifierSet.RCtrl, tap.Hold);
        Assert.Equal(120, tap.MaxHoldMs);
        var step = Assert.IsType<SwitchLayoutStep>(settings.Rules[0].Steps[0]);
        Assert.Equal(SwitchMode.Specific, step.Mode);
        Assert.Equal("en-US", step.Layout);
        var press = Assert.IsType<PressTrigger>(settings.Rules[1].Trigger);
        Assert.Equal((VirtualKey)0x80, press.Key);
        Assert.Equal(400, press.DoubleMs);
        Assert.Equal(ServiceAction.TogglePause, Assert.IsType<ServiceStep>(settings.Rules[1].Steps[0]).Action);
    }

    /// <summary>Числа вне диапазона приводятся к границам, без ошибки.</summary>
    [Fact]
    public void Parse_OutOfRangeNumbers_AreClamped()
    {
        const string Json = """
            { "wordBufferLimit": 5000, "indicator": { "durationMs": 10 },
              "rules": [ { "name": "r", "trigger": { "kind": "Tap", "keys": "RCtrl", "maxHoldMs": 5, "doubleMs": 5000 }, "steps": [] } ] }
            """;

        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings);

        Assert.Equal(AppSettings.MaxWordBufferLimit, settings.WordBufferLimit);
        Assert.Equal(IndicatorSettings.MinDurationMs, settings.Indicator.DurationMs);
        var tap = Assert.IsType<TapTrigger>(settings.Rules[0].Trigger);
        Assert.Equal(TriggerLimits.MinHoldMs, tap.MaxHoldMs);
        Assert.Equal(TriggerLimits.MaxDoubleMs, tap.DoubleMs);
    }

    /// <summary>Битое правило пропускается с предупреждением, остальные работают.</summary>
    [Fact]
    public void Parse_BrokenRules_AreSkippedWithWarnings()
    {
        const string Json = """
            { "rules": [
              { "name": "плохие клавиши", "trigger": { "kind": "Tap", "keys": "Banana" }, "steps": [] },
              { "name": "неизвестный шаг", "trigger": { "kind": "Tap", "keys": "RCtrl" }, "steps": [ { "kind": "Dance" } ] },
              { "name": "Tap с буквой", "trigger": { "kind": "Tap", "keys": "RCtrl+A" }, "steps": [] },
              { "name": "хорошее", "trigger": { "kind": "Tap", "keys": "RCtrl" }, "steps": [] }
            ] }
            """;

        var result = SettingsJson.Parse(Json);

        var settings = Assert.IsType<AppSettings>(result.Settings);
        Assert.Equal(["хорошее"], settings.Rules.Select(r => r.Name));
        Assert.Equal(3, result.Warnings.Count);
        Assert.Contains("'плохие клавиши'", result.Warnings[0], StringComparison.Ordinal);
    }

    /// <summary>Синтаксическая ошибка даёт описание с номером строки и не возвращает настройки.</summary>
    [Fact]
    public void Parse_BrokenJson_ReturnsErrorWithLine()
    {
        var result = SettingsJson.Parse("{\n  \"theme\": \n}");

        Assert.Null(result.Settings);
        Assert.StartsWith("line ", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Файл из более новой версии схемы не принимается.</summary>
    [Fact]
    public void Parse_NewerSchemaVersion_ReturnsError()
    {
        var result = SettingsJson.Parse("""{ "schemaVersion": 99 }""");

        Assert.Null(result.Settings);
        Assert.Contains("schemaVersion", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Пустой объект даёт значения по умолчанию.</summary>
    [Fact]
    public void Parse_EmptyObject_UsesDefaults()
    {
        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse("{}").Settings);

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(AppSettings.DefaultWordBufferLimit, settings.WordBufferLimit);
        Assert.Empty(settings.Rules);
    }

    /// <summary>Язык интерфейса: нет поля — «Как в Windows», явное значение читается и записывается обратно.</summary>
    [Fact]
    public void Parse_UiLanguage_DefaultsToSystemAndRoundTrips()
    {
        var missing = Assert.IsType<AppSettings>(SettingsJson.Parse("{}").Settings);
        var english = Assert.IsType<AppSettings>(SettingsJson.Parse("""{ "uiLanguage": "English" }""").Settings);
        var again = Assert.IsType<AppSettings>(SettingsJson.Parse(SettingsJson.Serialize(english)).Settings);

        Assert.Equal(AppLanguage.System, missing.UiLanguage);
        Assert.Equal(AppLanguage.English, english.UiLanguage);
        Assert.Equal(AppLanguage.English, again.UiLanguage);
    }

    /// <summary>Журнал по умолчанию выключен; явное значение важнее и возвращается после сериализации.</summary>
    [Fact]
    public void Parse_LogEnabled_DefaultsToFalseAndExplicitValueRoundTrips()
    {
        var missing = Assert.IsType<AppSettings>(SettingsJson.Parse("{}").Settings);
        var on = Assert.IsType<AppSettings>(SettingsJson.Parse("""{ "logEnabled": true }""").Settings);
        var again = Assert.IsType<AppSettings>(SettingsJson.Parse(SettingsJson.Serialize(on)).Settings);

        Assert.False(missing.LogEnabled);
        Assert.True(on.LogEnabled);
        Assert.True(again.LogEnabled);
    }

    /// <summary>Записи исключений без имени программы отбрасываются.</summary>
    [Fact]
    public void Parse_ExclusionWithoutProcess_IsDropped()
    {
        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse("""{ "exclusions": [ { "process": "" }, { "process": "a.exe" } ] }""").Settings);

        Assert.Equal(["a.exe"], settings.Exclusions.Select(e => e.Process));
    }
}
