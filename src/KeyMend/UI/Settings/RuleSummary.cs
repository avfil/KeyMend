using System.Globalization;
using System.IO;
using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Короткие тексты правила для таблицы: что оно делает и где работает.</summary>
internal static class RuleSummary
{
    /// <summary>Описывает цепочку по первому шагу; если шагов больше одного, добавляет «и ещё N».</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Текст для колонки «Что делает».</returns>
    public static string Action(Rule rule)
    {
        if (rule.Steps.Count == 0)
        {
            return Strings.SummaryNoSteps;
        }

        var first = Step(rule.Steps[0]);
        return rule.Steps.Count == 1
            ? first
            : Fill(Strings.SummaryMore, first, (rule.Steps.Count - 1).ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>Описывает один шаг одной строкой.</summary>
    /// <param name="step">Шаг.</param>
    /// <returns>Текст шага.</returns>
    public static string Step(Step step) => step switch
    {
        SwitchLayoutStep { Mode: SwitchMode.Pair } s when s.Pair.Count >= 2 =>
            Fill(Strings.SummaryLanguagePair, LanguageNames.Short(s.Pair[0]), LanguageNames.Short(s.Pair[1])),
        SwitchLayoutStep s => Fill(Strings.SummaryLanguageSpecific, LanguageNames.Short(s.Layout ?? string.Empty)),
        CorrectStep { Scope: CorrectScope.Line } => Strings.SummaryCorrectLine,
        CorrectStep => Strings.SummaryCorrectSelection,
        LaunchStep s => Fill(Strings.SummaryLaunch, Path.GetFileName(s.File)),
        PowerStep s => Power(s),
        PressStep s => Fill(Strings.SummaryPress, KeyChordParser.Format(s.Keys)),
        DelayStep s => Fill(Strings.SummaryDelay, s.Ms.ToString(CultureInfo.CurrentCulture)),
        InsertTextStep => Strings.SummaryInsert,
        ServiceStep { Action: ServiceAction.TogglePause } => Strings.SummaryServicePause,
        ServiceStep { Action: ServiceAction.OpenSettings } => Strings.SummaryServiceOpen,
        ServiceStep => Strings.SummaryServiceCancelTimer,
        _ => string.Empty,
    };

    /// <summary>Где работает правило: «везде», «только в chrome.exe», «только в chrome.exe +2».</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Текст для колонки «Где работает».</returns>
    public static string Where(Rule rule) => rule.Processes.Count switch
    {
        0 => Strings.WhereEverywhere,
        1 => Fill(Strings.WhereOnly, rule.Processes[0]),
        _ => Fill(Strings.WhereOnlyMore, rule.Processes[0], (rule.Processes.Count - 1).ToString(CultureInfo.CurrentCulture)),
    };

    /// <summary>Полный список программ для всплывающей подсказки.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Имена через запятую или <c>null</c>, если правило работает везде.</returns>
    public static string? WhereDetails(Rule rule) => rule.Processes.Count == 0 ? null : string.Join(", ", rule.Processes);

    /// <summary>Название действия питания и время: «Сон сейчас», «Сон через 30 мин».</summary>
    private static string Power(PowerStep step)
    {
        var name = step.Action switch
        {
            PowerAction.Sleep => Strings.PowerSleep,
            PowerAction.Hibernate => Strings.PowerHibernate,
            PowerAction.Shutdown => Strings.PowerShutdown,
            PowerAction.Restart => Strings.PowerRestart,
            PowerAction.Lock => Strings.PowerLock,
            _ => Strings.PowerSignOut,
        };
        return step.AfterMinutes == 0
            ? Fill(Strings.SummaryPowerNow, name)
            : Fill(Strings.SummaryPowerAfter, name, step.AfterMinutes.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>Подставляет аргументы в шаблон с метками <c>{0}</c>, <c>{1}</c>; строки лежат в ресурсах.</summary>
    internal static string Fill(string template, params string?[] args)
    {
        var result = template;
        for (var i = 0; i < args.Length; i++)
        {
            result = result.Replace("{" + i.ToString(CultureInfo.InvariantCulture) + "}", args[i], StringComparison.Ordinal);
        }

        return result;
    }
}
