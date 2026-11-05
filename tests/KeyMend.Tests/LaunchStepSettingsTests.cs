using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты чтения и записи шага «Запустить программу» в <c>settings.json</c> (SPEC 7.3).</summary>
public sealed class LaunchStepSettingsTests
{
    /// <summary>Шаг читается целиком; тип запуска лежит в <c>type</c>, а <c>kind</c> остаётся видом шага.</summary>
    [Fact]
    public void Parse_LaunchStep_ReadsAllFields()
    {
        const string Json = """
            { "rules": [ { "id": "m", "name": "m", "trigger": { "kind": "Press", "keys": "F17" },
              "steps": [ { "kind": "Launch", "type": "Pwsh", "file": "fix.ps1", "arguments": "-x", "workingDirectory": "tools",
                           "window": "Hidden", "wait": true, "repeat": "StopPrevious" } ] } ] }
            """;

        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings);

        var step = Assert.IsType<LaunchStep>(Assert.Single(Assert.Single(settings.Rules).Steps));
        Assert.Equal(LaunchType.Pwsh, step.Type);
        Assert.Equal("fix.ps1", step.File);
        Assert.Equal("-x", step.Arguments);
        Assert.Equal("tools", step.WorkingDirectory);
        Assert.Equal(LaunchWindow.Hidden, step.Window);
        Assert.True(step.Wait);
        Assert.Equal(LaunchRepeat.StopPrevious, step.Repeat);
    }

    /// <summary>Значения по умолчанию: программа, обычное окно, не ждать, игнорировать повтор.</summary>
    [Fact]
    public void Parse_MinimalLaunchStep_UsesDefaults()
    {
        const string Json = """
            { "rules": [ { "id": "m", "name": "m", "trigger": { "kind": "Press", "keys": "F17" },
              "steps": [ { "kind": "Launch", "file": "a.exe" } ] } ] }
            """;

        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings);

        var step = Assert.IsType<LaunchStep>(Assert.Single(Assert.Single(settings.Rules).Steps));
        Assert.Equal(LaunchType.Exe, step.Type);
        Assert.Equal(LaunchWindow.Normal, step.Window);
        Assert.False(step.Wait);
        Assert.Equal(LaunchRepeat.Ignore, step.Repeat);
    }

    /// <summary>Шаг без файла пропускает правило как битое, остальные работают.</summary>
    [Fact]
    public void Parse_LaunchWithoutFile_SkipsRule()
    {
        const string Json = """
            { "rules": [
              { "id": "bad", "name": "bad", "trigger": { "kind": "Press", "keys": "F17" }, "steps": [ { "kind": "Launch" } ] },
              { "id": "ok", "name": "ok", "trigger": { "kind": "Press", "keys": "F18" }, "steps": [ { "kind": "Delay", "ms": 5 } ] } ] }
            """;

        var result = SettingsJson.Parse(Json);

        Assert.Equal("ok", Assert.Single(Assert.IsType<AppSettings>(result.Settings).Rules).Id);
        Assert.Single(result.Warnings);
    }

    /// <summary>Запись и повторное чтение сохраняют шаг.</summary>
    [Fact]
    public void Serialize_LaunchStep_RoundTrips()
    {
        var rule = new Rule
        {
            Id = "m",
            Name = "m",
            Trigger = new PressTrigger { Key = Input.VirtualKey.Space },
            Steps = [new LaunchStep { File = "a.exe", Window = LaunchWindow.Minimized, Repeat = LaunchRepeat.Another }],
        };

        var json = SettingsJson.Serialize(new AppSettings { Rules = [rule] });
        var back = Assert.IsType<AppSettings>(SettingsJson.Parse(json).Settings);

        Assert.Equal(rule.Steps, Assert.Single(back.Rules).Steps);
    }
}
