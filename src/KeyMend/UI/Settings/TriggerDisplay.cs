using System.Globalization;
using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Из чего состоит триггер на экране: подпись «держать», удерживаемые клавиши и сами клавиши.</summary>
/// <param name="Hold">Удерживаемые модификаторы tap; у сочетания пусто.</param>
/// <param name="Keys">Нажимаемые клавиши; у сочетания — модификаторы и основная клавиша.</param>
/// <param name="IsDouble">Двойное нажатие: рядом рисуется «×2».</param>
internal sealed record TriggerChips(IReadOnlyList<string> Hold, IReadOnlyList<string> Keys, bool IsDouble);

/// <summary>Тексты поля «Клавиши»: как показать триггер и как назвать замечания проверок (SPEC 5).</summary>
internal static class TriggerDisplay
{
    /// <summary>Раскладывает триггер на плашки клавиш.</summary>
    /// <param name="trigger">Триггер.</param>
    /// <returns>Плашки для показа.</returns>
    public static TriggerChips Chips(Trigger trigger)
    {
        var isDouble = trigger.DoubleMs is not null;
        return trigger switch
        {
            TapTrigger tap => new TriggerChips(Names(tap.Hold), Names(tap.Keys), isDouble),
            PressTrigger press => new TriggerChips([], [.. Names(press.Hold), KeyChordParser.Format(new KeyChord(ModifierSet.None, press.Key))], isDouble),
            _ => new TriggerChips([], [], isDouble),
        };
    }

    /// <summary>Записывает триггер одной строкой для сообщений: <c>RCtrl</c>, <c>RCtrl+RShift</c>, <c>F17 ×2</c>.</summary>
    /// <param name="trigger">Триггер.</param>
    /// <returns>Строка клавиш.</returns>
    public static string Describe(Trigger trigger)
    {
        var chips = Chips(trigger);
        var text = string.Join('+', chips.Hold.Concat(chips.Keys));
        return chips.IsDouble ? text + " ×2" : text;
    }

    /// <summary>Формулирует замечание к триггеру.</summary>
    /// <param name="issue">Замечание проверки.</param>
    /// <param name="candidate">Триггер правила, которое проверяют.</param>
    /// <returns>Текст для полосы под полем.</returns>
    public static string Message(TriggerIssue issue, Trigger candidate)
    {
        var keys = Describe(candidate);
        return issue.Kind switch
        {
            TriggerIssueKind.Busy => Fill(Strings.TriggerBusy, keys, issue.Other?.Name, Programs(issue.Other)),
            TriggerIssueKind.System => Fill(Strings.TriggerSystem, keys),
            TriggerIssueKind.WindowsSwitch => Fill(Strings.TriggerWindowsSwitch, WindowsSwitchName(candidate)),
            TriggerIssueKind.DoubleDelay when candidate.DoubleMs is null && issue.Other is { } other => Fill(Strings.TriggerDelaySingle, issue.DelayMs?.ToString(CultureInfo.CurrentCulture), Describe(other.Trigger)),
            TriggerIssueKind.DoubleDelay => Fill(Strings.TriggerDelayDouble, issue.DelayMs?.ToString(CultureInfo.CurrentCulture), issue.Other?.Name),
            _ => string.Empty,
        };
    }

    /// <summary>Подставляет аргументы в шаблон с метками <c>{0}</c>, <c>{1}</c>…; строки в ресурсах, поэтому <c>CompositeFormat</c> не кэшируется.</summary>
    private static string Fill(string template, params string?[] args)
    {
        var result = template;
        for (var i = 0; i < args.Length; i++)
        {
            result = result.Replace("{" + i.ToString(CultureInfo.InvariantCulture) + "}", args[i], StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>Называет сочетание Windows, которое заденет триггер: Alt+Shift, если в нём Alt, иначе Ctrl+Shift.</summary>
    private static string WindowsSwitchName(Trigger trigger)
    {
        var mods = trigger switch
        {
            TapTrigger tap => tap.Keys | tap.Hold,
            PressTrigger press => press.Hold,
            _ => ModifierSet.None,
        };

        return (mods & (ModifierSet.LAlt | ModifierSet.RAlt | ModifierSet.AnyAlt)) != 0 ? "Alt+Shift" : "Ctrl+Shift";
    }

    /// <summary>Где работает правило словами: «во всех программах» или «только в chrome.exe, notepad.exe».</summary>
    private static string Programs(Rule? rule) => rule is null || rule.Processes.Count == 0
        ? Strings.TriggerEverywhere
        : Fill(Strings.TriggerInPrograms, string.Join(", ", rule.Processes));

    /// <summary>Имена модификаторов набора по порядку записи (Ctrl, Shift, Alt, Win).</summary>
    private static string[] Names(ModifierSet set) => set == ModifierSet.None
        ? []
        : KeyChordParser.Format(new KeyChord(set, VirtualKey.None)).Split('+');
}
