using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TriggerDisplay"/> и <see cref="RegistryLanguageHotkeys"/>: плашки клавиш, тексты замечаний, раскладки языка.</summary>
public sealed class TriggerDisplayTests
{
    /// <summary>Клавиша F18.</summary>
    internal const VirtualKey F18 = (VirtualKey)0x81;

    /// <summary>Tap раскладывается на плашки; удерживаемые отделены от нажимаемых.</summary>
    [Fact]
    public void Chips_TapWithHold_SplitsHoldFromKeys()
    {
        var chips = TriggerDisplay.Chips(new TapTrigger { Keys = ModifierSet.RShift, Hold = ModifierSet.RCtrl });

        Assert.Equal(["RCtrl"], chips.Hold);
        Assert.Equal(["RShift"], chips.Keys);
        Assert.False(chips.IsDouble);
    }

    /// <summary>У сочетания модификаторы и клавиша идут одним рядом, двойное помечено.</summary>
    [Fact]
    public void Chips_PressWithModifiers_ListsModifiersThenKey()
    {
        var chips = TriggerDisplay.Chips(new PressTrigger { Hold = ModifierSet.LCtrl | ModifierSet.LShift, Key = F18, DoubleMs = 300 });

        Assert.Empty(chips.Hold);
        Assert.Equal(["LCtrl", "LShift", "F18"], chips.Keys);
        Assert.True(chips.IsDouble);
    }

    /// <summary>Описание триггера одной строкой для сообщений.</summary>
    [Theory]
    [InlineData((int)ModifierSet.RCtrl, 0, "RCtrl")]
    [InlineData((int)ModifierSet.RCtrl, 200, "RCtrl ×2")]
    public void Describe_Tap_JoinsKeys(int keys, int doubleMs, string expected)
    {
        var trigger = new TapTrigger { Keys = (ModifierSet)keys, DoubleMs = doubleMs == 0 ? null : doubleMs };

        Assert.Equal(expected, TriggerDisplay.Describe(trigger));
    }

    /// <summary>В сообщении о занятых клавишах названы клавиши, правило и программы.</summary>
    [Fact]
    public void Message_Busy_NamesKeysRuleAndPrograms()
    {
        var other = new Rule { Name = "Монитор", Trigger = new PressTrigger { Key = F18 }, Processes = ["chrome.exe"] };
        var issue = new TriggerIssue(TriggerIssueKind.Busy, TriggerIssueSeverity.Error, other);

        var text = TriggerDisplay.Message(issue, new PressTrigger { Key = F18 });

        Assert.Contains("F18", text, StringComparison.Ordinal);
        Assert.Contains("«Монитор»", text, StringComparison.Ordinal);
        Assert.Contains("chrome.exe", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{", text, StringComparison.Ordinal);
    }

    /// <summary>Предупреждение называет сочетание Windows: с Alt — Alt+Shift, иначе Ctrl+Shift.</summary>
    [Theory]
    [InlineData((int)(ModifierSet.LAlt | ModifierSet.LShift), "Alt+Shift")]
    [InlineData((int)(ModifierSet.LCtrl | ModifierSet.LShift), "Ctrl+Shift")]
    public void Message_WindowsSwitch_NamesTheWindowsShortcut(int hold, string expected)
    {
        var trigger = new PressTrigger { Key = (VirtualKey)0x31, Hold = (ModifierSet)hold };

        var text = TriggerDisplay.Message(new TriggerIssue(TriggerIssueKind.WindowsSwitch, TriggerIssueSeverity.Warning), trigger);

        Assert.StartsWith(expected, text, StringComparison.Ordinal);
    }

    /// <summary>Задержка одиночного называет время и двойное правило.</summary>
    [Fact]
    public void Message_DoubleDelay_ForSingleMentionsDelayAndDoubleKeys()
    {
        var other = new Rule { Name = "Двойное", Trigger = new PressTrigger { Key = F18, DoubleMs = 400 } };
        var issue = new TriggerIssue(TriggerIssueKind.DoubleDelay, TriggerIssueSeverity.Info, other, 400);

        var text = TriggerDisplay.Message(issue, new PressTrigger { Key = F18 });

        Assert.Contains("400", text, StringComparison.Ordinal);
        Assert.Contains("F18 ×2", text, StringComparison.Ordinal);
    }

    /// <summary>Две раскладки одного языка находятся по младшим 16 битам описателя.</summary>
    [Fact]
    public void SeveralLayoutsInOneLanguage_SameLanguageId_ReturnsTrue()
    {
        nint[] layouts = [0x04090409, 0x04190419, 0x04120409];

        Assert.True(RegistryLanguageHotkeys.SeveralLayoutsInOneLanguage(layouts));
    }

    /// <summary>Разные языки по одной раскладке — повторов нет.</summary>
    [Fact]
    public void SeveralLayoutsInOneLanguage_OneLayoutPerLanguage_ReturnsFalse()
    {
        nint[] layouts = [0x04090409, 0x04190419];

        Assert.False(RegistryLanguageHotkeys.SeveralLayoutsInOneLanguage(layouts));
        Assert.False(RegistryLanguageHotkeys.SeveralLayoutsInOneLanguage([]));
    }
}
