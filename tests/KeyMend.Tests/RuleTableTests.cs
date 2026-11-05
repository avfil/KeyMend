using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Группы, отбор и тексты таблицы «Правила» (SPEC 5).</summary>
public sealed class RuleTableTests
{
    /// <summary>Правило с одним шагом и триггером из строки клавиш.</summary>
    private static Rule Make(string name, string keys, Step? step, bool enabled = true, int? doubleMs = null, params string[] processes) => new()
    {
        Id = name,
        Name = name,
        Enabled = enabled,
        Processes = processes,
        Trigger = KeyChordParser.TryParse(keys, out var chord)
            ? (chord.Key == VirtualKey.None
                ? new TapTrigger { Keys = chord.Modifiers, DoubleMs = doubleMs }
                : new PressTrigger { Hold = chord.Modifiers, Key = chord.Key, DoubleMs = doubleMs })
            : throw new InvalidOperationException(keys),
        Steps = step is null ? [] : [step],
    };

    /// <summary>Набор правил на все группы.</summary>
    private static List<Rule> Sample() =>
    [
        Make("Язык", "RCtrl", new SwitchLayoutStep()),
        Make("Язык2", "RCtrl", new SwitchLayoutStep(), enabled: false, doubleMs: 200),
        Make("Исправить", "RShift", new CorrectStep { Scope = CorrectScope.Selection }),
        Make("Монитор", "F17", new LaunchStep { File = @"scripts\tools\notepad.exe" }),
        Make("Монитор x2", "F17", new LaunchStep { File = "m.exe" }, doubleMs: 400),
        Make("Сон", "F15", new PowerStep { Action = PowerAction.Sleep }),
        Make("Нажатие", "F13", new PressStep { Keys = new KeyChord(ModifierSet.AnyCtrl, VirtualKey.Return) }),
        Make("Пустое", "F14", null),
    ];

    /// <summary>Группа определяется первым шагом, всё прочее и пустая цепочка — «Другое».</summary>
    [Fact]
    public void Of_FirstStep_PicksGroup()
    {
        var groups = Sample().Select(RuleGroups.Of).ToArray();

        Assert.Equal(
            [RuleGroup.Language, RuleGroup.Language, RuleGroup.Correction, RuleGroup.Launch, RuleGroup.Launch, RuleGroup.Power, RuleGroup.Other, RuleGroup.Other],
            groups);
    }

    /// <summary>Не первый шаг, а только первый определяет группу.</summary>
    [Fact]
    public void Of_CorrectAfterDelay_IsOther()
    {
        var rule = Make("x", "F1", null) with { Steps = [new DelayStep { Ms = 10 }, new CorrectStep { Scope = CorrectScope.Line }] };

        Assert.Equal(RuleGroup.Other, RuleGroups.Of(rule));
    }

    /// <summary>Без фильтра видны все непустые группы в порядке показа, числа считаются по группам.</summary>
    [Fact]
    public void Build_NoFilter_ShowsAllGroupsWithCounts()
    {
        var view = RuleTable.Build(Sample(), new RuleTableFilter());

        Assert.Equal(
            [RuleGroup.Language, RuleGroup.Correction, RuleGroup.Launch, RuleGroup.Power, RuleGroup.Other],
            view.Groups.Select(g => g.Group).ToArray());
        Assert.Equal(8, view.Total);
        Assert.Equal(2, view.Counts[RuleGroup.Language]);
        Assert.Equal(2, view.Counts[RuleGroup.Other]);
    }

    /// <summary>Чип группы оставляет одну группу, а числа на других чипах не меняются.</summary>
    [Fact]
    public void Build_GroupChip_ShowsOnlyGroupButKeepsCounts()
    {
        var view = RuleTable.Build(Sample(), new RuleTableFilter(Group: RuleGroup.Launch));

        Assert.Single(view.Groups);
        Assert.Equal(2, view.Groups[0].Rules.Count);
        Assert.Equal(8, view.Total);
    }

    /// <summary>Состояние сужает и показ, и числа.</summary>
    [Fact]
    public void Build_DisabledOnly_ShowsDisabledAndCountsThem()
    {
        var view = RuleTable.Build(Sample(), new RuleTableFilter(State: RuleStateFilter.Disabled));

        Assert.Equal(1, view.Total);
        Assert.Equal("Язык2", view.Groups.Single().Rules.Single().Name);
        Assert.Equal(0, view.Counts[RuleGroup.Power]);
    }

    /// <summary>Поиск без учёта регистра по названию.</summary>
    [Fact]
    public void Build_SearchByName_IgnoresCase()
    {
        var view = RuleTable.Build(Sample(), new RuleTableFilter(Search: "монитор"));

        Assert.Equal(2, view.Total);
    }

    /// <summary>Поиск по записи клавиш, в том числе по «×2».</summary>
    [Theory]
    [InlineData("F17", 2)]
    [InlineData("f17 ×2", 1)]
    [InlineData("rctrl", 2)]
    [InlineData("нет такого", 0)]
    public void Build_SearchByKeys_FindsRules(string search, int expected)
    {
        var view = RuleTable.Build(Sample(), new RuleTableFilter(Search: search));

        Assert.Equal(expected, view.Total);
    }

    /// <summary>Группа без правил не показывается.</summary>
    [Fact]
    public void Build_EmptyGroup_IsHidden()
    {
        var view = RuleTable.Build([Make("Язык", "RCtrl", new SwitchLayoutStep())], new RuleTableFilter());

        Assert.Equal(RuleGroup.Language, view.Groups.Single().Group);
    }

    /// <summary>Тексты «что делает» по первому шагу и «и ещё N».</summary>
    [Fact]
    public void Action_Steps_DescribesFirstAndCountsRest()
    {
        var launch = Make("m", "F1", new LaunchStep { File = @"scripts\tools\notepad.exe" });
        var sleep = Make("s", "F2", new PowerStep { Action = PowerAction.Sleep, AfterMinutes = 30 });
        var many = Make("x", "F3", new CorrectStep { Scope = CorrectScope.Line }) with
        {
            Steps = [new CorrectStep { Scope = CorrectScope.Line }, new DelayStep { Ms = 200 }, new DelayStep { Ms = 200 }],
        };

        Assert.Equal(RuleSummary.Fill(Strings.SummaryLaunch, "notepad.exe"), RuleSummary.Action(launch));
        Assert.Equal(RuleSummary.Fill(Strings.SummaryPowerAfter, Strings.PowerSleep, "30"), RuleSummary.Action(sleep));
        Assert.Equal(RuleSummary.Fill(Strings.SummaryMore, Strings.SummaryCorrectLine, "2"), RuleSummary.Action(many));
        Assert.Equal(Strings.SummaryNoSteps, RuleSummary.Action(Make("e", "F4", null)));
    }

    /// <summary>Пара языков в тексте называется словами.</summary>
    [Fact]
    public void Action_LanguagePair_ShowsBothLanguages()
    {
        var text = RuleSummary.Action(Make("l", "RCtrl", new SwitchLayoutStep()));

        Assert.Contains("⇄", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ru-RU", text, StringComparison.Ordinal);
    }

    /// <summary>«Где работает»: везде, одна программа, несколько с остатком; подробности — полным списком.</summary>
    [Fact]
    public void Where_Processes_ShortensAndKeepsDetails()
    {
        var all = Make("a", "F1", null);
        var one = Make("b", "F2", null, processes: "chrome.exe");
        var three = Make("c", "F3", null, processes: ["chrome.exe", "a.exe", "b.exe"]);

        Assert.Equal(Strings.WhereEverywhere, RuleSummary.Where(all));
        Assert.Null(RuleSummary.WhereDetails(all));
        Assert.Equal(RuleSummary.Fill(Strings.WhereOnly, "chrome.exe"), RuleSummary.Where(one));
        Assert.Equal(RuleSummary.Fill(Strings.WhereOnlyMore, "chrome.exe", "2"), RuleSummary.Where(three));
        Assert.Equal("chrome.exe, a.exe, b.exe", RuleSummary.WhereDetails(three));
    }
}
