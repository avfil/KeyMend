using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Что мешает сохранить правило.</summary>
[Flags]
internal enum RuleProblems
{
    /// <summary>Ничего.</summary>
    None = 0,

    /// <summary>Название пустое.</summary>
    NameEmpty = 1,

    /// <summary>Клавиши не записаны.</summary>
    TriggerMissing = 2,

    /// <summary>У клавиш есть ошибка: занято или системное сочетание.</summary>
    TriggerError = 4,

    /// <summary>В правиле нет ни одного шага.</summary>
    NoSteps = 8,

    /// <summary>Выбрано «только в выбранных», а программ нет.</summary>
    NoPrograms = 16,

    /// <summary>У какого-то шага есть ошибка (см. <see cref="RuleValidationResult.StepProblems"/>).</summary>
    StepError = 32,
}

/// <summary>Итог проверки правила.</summary>
/// <param name="Problems">Что мешает сохранить.</param>
/// <param name="TriggerIssues">Замечания к клавишам: ошибки, предупреждения, сведения.</param>
/// <param name="StepProblems">Проблема каждого шага по порядку; <see cref="StepProblem.None"/> — шаг в порядке.</param>
internal sealed record RuleValidationResult(RuleProblems Problems, IReadOnlyList<TriggerIssue> TriggerIssues, IReadOnlyList<StepProblem> StepProblems)
{
    /// <summary>Правило можно сохранять: предупреждения и сведения не мешают.</summary>
    public bool CanSave => Problems == RuleProblems.None;
}

/// <summary>Проверка правила при сохранении в редакторе (SPEC 5).</summary>
internal static class RuleValidation
{
    /// <summary>Проверяет черновик.</summary>
    /// <param name="draft">Правило в редакторе.</param>
    /// <param name="others">Остальные правила настроек; редактируемое среди них быть не должно.</param>
    /// <param name="windows">Сочетания, на которых Windows меняет язык или раскладку.</param>
    /// <param name="scriptsDir">Папка скриптов для проверки файлов шагов «Запустить программу».</param>
    /// <returns>Что мешает сохранить и замечания к клавишам и шагам.</returns>
    public static RuleValidationResult Validate(RuleDraft draft, IReadOnlyList<Rule> others, LanguageHotkeys windows, string scriptsDir)
    {
        var problems = RuleProblems.None;
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            problems |= RuleProblems.NameEmpty;
        }

        if (draft.OnlySelected && draft.Processes.Count == 0)
        {
            problems |= RuleProblems.NoPrograms;
        }

        if (draft.Steps.Count == 0)
        {
            problems |= RuleProblems.NoSteps;
        }

        IReadOnlyList<StepProblem> stepProblems = [.. draft.Steps.Select(s => StepValidation.Check(s, scriptsDir))];
        if (stepProblems.Any(p => p != StepProblem.None))
        {
            problems |= RuleProblems.StepError;
        }

        IReadOnlyList<TriggerIssue> issues = [];
        if (draft.Trigger is { } trigger)
        {
            issues = TriggerChecks.Check(draft.ToRule(trigger), others, windows);
            if (issues.Any(i => i.Severity == TriggerIssueSeverity.Error))
            {
                problems |= RuleProblems.TriggerError;
            }
        }
        else
        {
            problems |= RuleProblems.TriggerMissing;
        }

        return new RuleValidationResult(problems, issues, stepProblems);
    }
}
