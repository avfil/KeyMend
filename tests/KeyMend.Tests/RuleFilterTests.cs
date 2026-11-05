using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="RuleFilter"/>: выбор действующих правил по программе, исключениям и паузе.</summary>
public sealed class RuleFilterTests
{
    /// <summary>Создаёт правило переключения языка.</summary>
    private static Rule Layout(string name, bool enabled = true, params string[] processes) => new()
    {
        Name = name,
        Enabled = enabled,
        Processes = processes,
        Trigger = new TapTrigger { Keys = ModifierSet.RCtrl },
        Steps = [new SwitchLayoutStep()],
    };

    /// <summary>Создаёт правило паузы.</summary>
    private static Rule PauseRule() => new()
    {
        Name = "Pause",
        Trigger = new PressTrigger { Key = (VirtualKey)0x7F },
        Steps = [new ServiceStep { Action = ServiceAction.TogglePause }],
    };

    /// <summary>Выключенное правило не выбирается.</summary>
    [Fact]
    public void Select_DisabledRule_IsSkipped()
    {
        var result = RuleFilter.Select([Layout("a", enabled: false)], "notepad.exe", [], paused: false);

        Assert.Empty(result);
    }

    /// <summary>Правило без списка программ работает везде, в том числе когда процесс неизвестен.</summary>
    [Fact]
    public void Select_NoProcessList_AppliesEverywhere()
    {
        var result = RuleFilter.Select([Layout("a")], null, [], paused: false);

        Assert.Single(result);
    }

    /// <summary>Правило со списком работает только в перечисленных программах без учёта регистра.</summary>
    [Fact]
    public void Select_ProcessList_MatchesIgnoringCase()
    {
        var rules = new[] { Layout("a", true, "Chrome.exe") };

        Assert.Single(RuleFilter.Select(rules, "chrome.EXE", [], paused: false));
        Assert.Empty(RuleFilter.Select(rules, "notepad.exe", [], paused: false));
        Assert.Empty(RuleFilter.Select(rules, null, [], paused: false));
    }

    /// <summary>Исключение отключает переключение языка, но не остальные правила.</summary>
    [Fact]
    public void Select_ExclusionDisablesLayoutSwitchOnly()
    {
        var exclusions = new[] { new ExclusionEntry { Process = "mstsc.exe", LayoutSwitch = false } };
        var other = PauseRule() with { Name = "other", Steps = [] };

        var result = RuleFilter.Select([Layout("lang"), other], "MSTSC.exe", exclusions, paused: false);

        Assert.Equal(["other"], result.Select(r => r.Name));
    }

    /// <summary>Другая программа исключение не затрагивает.</summary>
    [Fact]
    public void Select_ExclusionForAnotherProcess_DoesNotApply()
    {
        var exclusions = new[] { new ExclusionEntry { Process = "mstsc.exe", LayoutSwitch = false } };

        var result = RuleFilter.Select([Layout("lang")], "notepad.exe", exclusions, paused: false);

        Assert.Single(result);
    }

    /// <summary>На паузе работает только правило, которое снимает паузу.</summary>
    [Fact]
    public void Select_Paused_KeepsOnlyPauseToggle()
    {
        var result = RuleFilter.Select([Layout("lang"), PauseRule()], "notepad.exe", [], paused: true);

        Assert.Equal(["Pause"], result.Select(r => r.Name));
    }

    /// <summary>Категория правила с переключением языка — язык, иначе — остальные.</summary>
    [Fact]
    public void CategoryOf_ByStepKind_ReturnsExpected()
    {
        Assert.Equal(RuleCategory.LayoutSwitch, RuleFilter.CategoryOf(Layout("a")));
        Assert.Equal(RuleCategory.Rules, RuleFilter.CategoryOf(PauseRule()));
    }
}
