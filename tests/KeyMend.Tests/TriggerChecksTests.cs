using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TriggerChecks"/> и <see cref="LanguageHotkeys"/>.</summary>
public sealed class TriggerChecksTests
{
    /// <summary>Клавиша F17.</summary>
    internal const VirtualKey F17 = (VirtualKey)0x80;

    /// <summary>Клавиша F18.</summary>
    internal const VirtualKey F18 = (VirtualKey)0x81;

    /// <summary>Сочетания смены языка у владельца: Alt+Shift для языка, Ctrl+Shift для раскладки.</summary>
    private static readonly LanguageHotkeys _both = new(AltShift: true, CtrlShift: true);

    /// <summary>Создаёт правило.</summary>
    private static Rule MakeRule(string id, Trigger trigger, bool enabled = true, params string[] processes) => new()
    {
        Id = id,
        Name = id,
        Enabled = enabled,
        Processes = processes,
        Trigger = trigger,
    };

    /// <summary>Создаёт сочетание на одной клавише.</summary>
    private static PressTrigger Key(VirtualKey key, int? doubleMs = null) => new() { Key = key, DoubleMs = doubleMs };

    /// <summary>Занятая клавиша другим включённым правилом — ошибка со ссылкой на это правило.</summary>
    [Fact]
    public void Check_SameKeyInOtherEnabledRule_ReportsBusy()
    {
        var other = MakeRule("a", Key(F18));
        var issues = TriggerChecks.Check(MakeRule("b", Key(F18)), [other], LanguageHotkeys.None);

        var issue = Assert.Single(issues);
        Assert.Equal(TriggerIssueKind.Busy, issue.Kind);
        Assert.Equal(TriggerIssueSeverity.Error, issue.Severity);
        Assert.Same(other, issue.Other);
    }

    /// <summary>Правило не конфликтует само с собой: сохранение уже существующего правила.</summary>
    [Fact]
    public void Check_SameRuleId_IsIgnored()
    {
        var existing = MakeRule("a", Key(F18));
        var edited = existing with { Name = "new" };

        Assert.Empty(TriggerChecks.Check(edited, [existing], LanguageHotkeys.None));
    }

    /// <summary>Выключенное правило не мешает, и выключенное редактируемое тоже не проверяется на занятость.</summary>
    [Fact]
    public void Check_DisabledRule_DoesNotConflict()
    {
        var off = MakeRule("a", Key(F18), enabled: false);

        Assert.Empty(TriggerChecks.Check(MakeRule("b", Key(F18)), [off], LanguageHotkeys.None));
        Assert.Empty(TriggerChecks.Check(
            MakeRule("c", Key(F18), enabled: false),
            [MakeRule("d", Key(F18))],
            LanguageHotkeys.None));
    }

    /// <summary>Разные программы не пересекаются, пустой список — «везде», регистр имён не важен.</summary>
    [Theory]
    [InlineData("notepad.exe", "chrome.exe", false)]
    [InlineData("chrome.exe", "CHROME.EXE", true)]
    [InlineData("", "chrome.exe", true)]
    [InlineData("chrome.exe", "", true)]
    public void Check_Processes_AreCompared(string first, string second, bool busy)
    {
        var a = MakeRule("a", Key(F18), true, first.Length == 0 ? [] : [first]);
        var b = MakeRule("b", Key(F18), true, second.Length == 0 ? [] : [second]);

        Assert.Equal(busy, TriggerChecks.Check(b, [a], LanguageHotkeys.None).Any(i => i.Kind == TriggerIssueKind.Busy));
    }

    /// <summary>Одиночное рядом с двойным — не ошибка, а сведения о задержке в обе стороны.</summary>
    [Fact]
    public void Check_SingleNextToDouble_ReportsDelayBothWays()
    {
        var single = MakeRule("single", Key(F17));
        var dbl = MakeRule("double", Key(F17, doubleMs: 400));

        var forSingle = Assert.Single(TriggerChecks.Check(single, [dbl], LanguageHotkeys.None));
        var forDouble = Assert.Single(TriggerChecks.Check(dbl, [single], LanguageHotkeys.None));

        Assert.Equal(TriggerIssueKind.DoubleDelay, forSingle.Kind);
        Assert.Equal(TriggerIssueSeverity.Info, forSingle.Severity);
        Assert.Equal(400, forSingle.DelayMs);
        Assert.Equal(400, forDouble.DelayMs);
    }

    /// <summary>Два двойных на одной клавише конфликтуют, даже с разным интервалом.</summary>
    [Fact]
    public void Check_TwoDoubles_ReportsBusy()
    {
        var a = MakeRule("a", Key(F17, 300));
        var b = MakeRule("b", Key(F17, 200));

        Assert.Equal(TriggerIssueKind.Busy, Assert.Single(TriggerChecks.Check(b, [a], LanguageHotkeys.None)).Kind);
    }

    /// <summary>Системное сочетание — ошибка без ссылки на правило.</summary>
    [Fact]
    public void Check_SystemShortcut_ReportsError()
    {
        var trigger = new PressTrigger { Key = (VirtualKey)0x4C, Hold = ModifierSet.LWin };

        var issue = Assert.Single(TriggerChecks.Check(MakeRule("a", trigger), [], LanguageHotkeys.None));
        Assert.Equal(TriggerIssueKind.System, issue.Kind);
        Assert.Equal(TriggerIssueSeverity.Error, issue.Severity);
    }

    /// <summary>Ctrl+Shift и левый Alt+Shift предупреждают только когда Windows их использует.</summary>
    [Theory]
    [InlineData((int)(ModifierSet.LCtrl | ModifierSet.LShift), true, false, false)]
    [InlineData((int)(ModifierSet.LCtrl | ModifierSet.LShift), true, true, true)]
    [InlineData((int)(ModifierSet.LCtrl | ModifierSet.LShift), false, true, true)]
    [InlineData((int)(ModifierSet.RCtrl | ModifierSet.RShift), false, true, true)]
    [InlineData((int)(ModifierSet.LAlt | ModifierSet.LShift), true, false, true)]
    [InlineData((int)(ModifierSet.LAlt | ModifierSet.LShift), false, true, false)]
    [InlineData((int)(ModifierSet.RAlt | ModifierSet.LShift), true, false, false)]
    [InlineData((int)(ModifierSet.LCtrl | ModifierSet.LShift | ModifierSet.LAlt), true, true, false)]
    [InlineData((int)ModifierSet.LCtrl, true, true, false)]
    public void Check_PressWithModifiers_WarnsWhenWindowsUsesThem(int hold, bool altShift, bool ctrlShift, bool expectWarning)
    {
        var trigger = new PressTrigger { Key = (VirtualKey)0x31, Hold = (ModifierSet)hold };
        var windows = new LanguageHotkeys(altShift, ctrlShift);

        var issues = TriggerChecks.Check(MakeRule("a", trigger), [], windows);

        Assert.Equal(expectWarning, issues.Any(i => i.Kind == TriggerIssueKind.WindowsSwitch && i.Severity == TriggerIssueSeverity.Warning));
    }

    /// <summary>«Держать RCtrl, нажать RShift» тоже предупреждает: Windows видит оба модификатора.</summary>
    [Fact]
    public void Check_TapHoldCtrlShift_WarnsWhenCtrlShiftIsUsed()
    {
        var trigger = new TapTrigger { Keys = ModifierSet.RShift, Hold = ModifierSet.RCtrl };

        Assert.Contains(
            TriggerChecks.Check(MakeRule("a", trigger), [], _both),
            issue => issue.Kind == TriggerIssueKind.WindowsSwitch);
    }

    /// <summary>Ошибки идут перед предупреждениями.</summary>
    [Fact]
    public void Check_ErrorAndWarning_ErrorsComeFirst()
    {
        var trigger = new PressTrigger { Key = VirtualKey.Escape, Hold = ModifierSet.LCtrl | ModifierSet.LShift };

        var issues = TriggerChecks.Check(MakeRule("a", trigger), [], _both);

        Assert.Equal(TriggerIssueKind.System, issues[0].Kind);
        Assert.Equal(TriggerIssueKind.WindowsSwitch, issues[1].Kind);
    }

    /// <summary>Коды реестра: 1 — Alt+Shift, 2 — Ctrl+Shift, 3 — нет; раскладка важна только при нескольких раскладках языка.</summary>
    [Theory]
    [InlineData("1", "2", false, true, false)]
    [InlineData("1", "2", true, true, true)]
    [InlineData("3", "2", true, false, true)]
    [InlineData("3", "3", true, false, false)]
    [InlineData("2", "3", false, false, true)]
    [InlineData("3", "1", true, true, false)]
    [InlineData(null, null, false, true, false)]
    [InlineData(null, null, true, true, true)]
    public void From_RegistryCodes_ResolvesHotkeys(
        string? language, string? layout, bool several, bool altShift, bool ctrlShift)
    {
        Assert.Equal(new LanguageHotkeys(altShift, ctrlShift), LanguageHotkeys.From(language, layout, several));
    }
}
