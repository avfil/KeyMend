using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Чистая логика редактора правила: программы, языки, проверка при сохранении, сравнение правил.</summary>
public sealed class RuleEditorLogicTests
{
    /// <summary>Правило на тап указанных модификаторов.</summary>
    private static Rule Tap(string id, ModifierSet keys, bool enabled = true) => new()
    {
        Id = id,
        Name = id,
        Enabled = enabled,
        Trigger = new TapTrigger { Keys = keys },
    };

    /// <summary>Папка скриптов для проверок.</summary>
    private const string _scripts = @"C:\Test\scripts";

    /// <summary>Допустимый черновик для проверок.</summary>
    private static RuleDraft Draft() => new()
    {
        Name = "Новое",
        Trigger = new TapTrigger { Keys = ModifierSet.RAlt },
        Steps = [new SwitchLayoutStep()],
    };

    /// <summary>Имя без расширения получает .exe, путь отбрасывается.</summary>
    [Theory]
    [InlineData("chrome", "chrome.exe")]
    [InlineData("  Chrome.EXE ", "Chrome.EXE")]
    [InlineData(@"C:\Program Files\Chrome\chrome.exe", "chrome.exe")]
    [InlineData("\"C:/x/notepad.exe\"", "notepad.exe")]
    [InlineData("tool.bat", "tool.bat")]
    public void Normalize_ValidName_ReturnsFileName(string input, string expected) =>
        Assert.Equal(expected, ProcessNames.Normalize(input));

    /// <summary>Пустое и недопустимое не принимается.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("a*b.exe")]
    [InlineData("...")]
    [InlineData(@"C:\folder\")]
    public void Normalize_Invalid_ReturnsNull(string? input) => Assert.Null(ProcessNames.Normalize(input));

    /// <summary>Повтор без учёта регистра не добавляется.</summary>
    [Fact]
    public void Add_Duplicate_KeepsList()
    {
        var list = ProcessNames.Add(["chrome.exe"], "CHROME");

        Assert.Equal(["chrome.exe"], list);
        Assert.Equal(["chrome.exe", "notepad.exe"], ProcessNames.Add(list, "notepad"));
    }

    /// <summary>Выбранный, но не установленный язык остаётся в списке с пометкой.</summary>
    [Fact]
    public void Build_SelectedNotInstalled_AppendsMarked()
    {
        var choices = LanguageChoices.Build(["ru-RU", "en-US"], ["de-DE"]);

        Assert.Equal(["ru-RU", "en-US", "de-DE"], choices.Select(c => c.Tag).ToArray());
        Assert.True(choices[0].Installed);
        Assert.False(choices[2].Installed);
        Assert.Equal(RuleSummary.Fill(Strings.LanguageNotInstalled, LanguageNames.Short("de-DE")), choices[2].Name);
    }

    /// <summary>Два варианта одного языка различаются страной.</summary>
    [Fact]
    public void Build_TwoEnglishes_UsesFullNames()
    {
        var choices = LanguageChoices.Build(["en-US", "en-GB"], []);

        Assert.NotEqual(choices[0].Name, choices[1].Name);
    }

    /// <summary>Хороший черновик сохранять можно.</summary>
    [Fact]
    public void Validate_GoodDraft_CanSave() =>
        Assert.True(RuleValidation.Validate(Draft(), [], LanguageHotkeys.None, _scripts).CanSave);

    /// <summary>Каждая ошибка видна своим флагом.</summary>
    [Fact]
    public void Validate_Problems_AreReported()
    {
        var draft = Draft() with
        {
            Name = "  ",
            Trigger = null,
            OnlySelected = true,
            Steps = [new SwitchLayoutStep { Pair = ["ru-RU", "RU-ru"] }],
        };

        var result = RuleValidation.Validate(draft, [], LanguageHotkeys.None, _scripts);

        Assert.Equal(RuleProblems.NameEmpty | RuleProblems.TriggerMissing | RuleProblems.NoPrograms | RuleProblems.StepError, result.Problems);
        Assert.False(result.CanSave);
    }

    /// <summary>Правило без шагов сохранить нельзя.</summary>
    [Fact]
    public void Validate_NoSteps_BlocksSave()
    {
        var result = RuleValidation.Validate(Draft() with { Steps = [] }, [], LanguageHotkeys.None, _scripts);

        Assert.Equal(RuleProblems.NoSteps, result.Problems);
        Assert.Empty(result.StepProblems);
    }

    /// <summary>Ошибка шага видна у своего номера.</summary>
    [Fact]
    public void Validate_BadStep_ReportsProblemByIndex()
    {
        var draft = Draft() with { Steps = [new SwitchLayoutStep(), new PressStep { Keys = default }] };

        var result = RuleValidation.Validate(draft, [], LanguageHotkeys.None, _scripts);

        Assert.Equal(RuleProblems.StepError, result.Problems);
        Assert.Equal([StepProblem.None, StepProblem.NoKeys], result.StepProblems);
    }

    /// <summary>Занятые клавиши мешают сохранить.</summary>
    [Fact]
    public void Validate_BusyTrigger_BlocksSave()
    {
        var result = RuleValidation.Validate(Draft(), [Tap("other", ModifierSet.RAlt)], LanguageHotkeys.None, _scripts);

        Assert.Equal(RuleProblems.TriggerError, result.Problems);
        Assert.Single(result.TriggerIssues, i => i.Kind == TriggerIssueKind.Busy);
    }

    /// <summary>Предупреждение про смену языка Windows не мешает.</summary>
    [Fact]
    public void Validate_WindowsSwitchWarning_StillCanSave()
    {
        var draft = Draft() with { Trigger = new TapTrigger { Keys = ModifierSet.LCtrl | ModifierSet.LShift } };

        var result = RuleValidation.Validate(draft, [], new LanguageHotkeys(AltShift: false, CtrlShift: true), _scripts);

        Assert.True(result.CanSave);
        Assert.Contains(result.TriggerIssues, i => i.Kind == TriggerIssueKind.WindowsSwitch);
    }

    /// <summary>Выключенный черновик с занятыми клавишами сохранять можно.</summary>
    [Fact]
    public void Validate_DisabledDraftOnBusyKeys_CanSave()
    {
        var result = RuleValidation.Validate(Draft() with { Enabled = false }, [Tap("other", ModifierSet.RAlt)], LanguageHotkeys.None, _scripts);

        Assert.True(result.CanSave);
    }

    /// <summary>В режиме «везде» список программ пуст, что бы ни лежало в черновике.</summary>
    [Fact]
    public void ToRule_Everywhere_DropsProcesses()
    {
        var draft = Draft() with { OnlySelected = false, Processes = ["a.exe"] };

        Assert.Empty(draft.ToRule(draft.Trigger!).Processes);
    }

    /// <summary>Равные правила с разными списками записываются одинаково, разные — по-разному.</summary>
    [Fact]
    public void SerializeRule_EqualRules_SameText()
    {
        var a = Tap("x", ModifierSet.RCtrl) with { Steps = [new SwitchLayoutStep()], Processes = ["a.exe"] };
        var b = Tap("x", ModifierSet.RCtrl) with { Steps = [new SwitchLayoutStep()], Processes = new List<string> { "a.exe" } };

        Assert.Equal(SettingsJson.SerializeRule(a), SettingsJson.SerializeRule(b));
        Assert.NotEqual(SettingsJson.SerializeRule(a), SettingsJson.SerializeRule(a with { Name = "y" }));
    }
}
