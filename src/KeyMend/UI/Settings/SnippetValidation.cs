using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.UI.Settings;

/// <summary>Что мешает сохранить сокращение.</summary>
[Flags]
internal enum SnippetProblems
{
    /// <summary>Ничего.</summary>
    None = 0,

    /// <summary>Сокращение не введено.</summary>
    KeysEmpty = 1,

    /// <summary>Какой-то символ не набирается ни в одной установленной раскладке.</summary>
    KeysUntypable = 2,

    /// <summary>Нет текста замены.</summary>
    TextEmpty = 4,

    /// <summary>Те же клавиши уже заняты другим включённым сокращением (см. <see cref="SnippetValidationResult.Taken"/>).</summary>
    KeysTaken = 8,

    /// <summary>У какого-то шага «После вставки» есть ошибка.</summary>
    StepError = 16,
}

/// <summary>Предупреждение: два сокращения перекрываются, и одно из них не сработает.</summary>
/// <param name="Other">Другое сокращение.</param>
/// <param name="Relation">Как шаблон редактируемого сокращения соотносится с шаблоном <paramref name="Other"/>.</param>
internal sealed record SnippetOverlap(Snippet Other, SnippetRelation Relation);

/// <summary>Итог проверки сокращения.</summary>
/// <param name="Problems">Что мешает сохранить.</param>
/// <param name="Taken">Включённое сокращение с теми же клавишами или <c>null</c>.</param>
/// <param name="Overlaps">Сокращения, из которых одно — начало другого; сохранить можно.</param>
/// <param name="StepProblems">Проблема каждого шага «После вставки» по порядку.</param>
internal sealed record SnippetValidationResult(
    SnippetProblems Problems, Snippet? Taken, IReadOnlyList<SnippetOverlap> Overlaps, IReadOnlyList<StepProblem> StepProblems)
{
    /// <summary>Сокращение можно сохранять: предупреждения не мешают.</summary>
    public bool CanSave => Problems == SnippetProblems.None;
}

/// <summary>Проверка сокращения при сохранении в редакторе (SPEC 5, F5.1).</summary>
internal static class SnippetValidation
{
    /// <summary>Ищет включённое сокращение с теми же клавишами: проверка перед включением сокращения в таблице.</summary>
    /// <param name="keys">Сокращение, которое хотят включить.</param>
    /// <param name="others">Остальные сокращения настроек; само проверяемое среди них быть не должно.</param>
    /// <param name="tables">Таблицы установленных раскладок.</param>
    /// <returns>Сокращение, занявшее клавиши, или <c>null</c>.</returns>
    public static Snippet? FindTaken(string keys, IReadOnlyList<Snippet> others, IReadOnlyList<LayoutTable> tables)
    {
        var ignoreShift = SnippetKeys.BuildIgnoreShift(tables);
        if (SnippetKeys.Parse(keys, tables, ignoreShift) is not { } pattern)
        {
            return null;
        }

        return others.FirstOrDefault(o => o.Enabled
            && SnippetKeys.Parse(o.Keys, tables, ignoreShift) is { } other
            && SnippetKeys.Compare(pattern, other) == SnippetRelation.Same);
    }

    /// <summary>Проверяет черновик.</summary>
    /// <param name="draft">Сокращение в редакторе.</param>
    /// <param name="others">Остальные сокращения настроек; редактируемого среди них быть не должно.</param>
    /// <param name="tables">Таблицы установленных раскладок в порядке списка Windows.</param>
    /// <param name="scriptsDir">Папка скриптов для проверки шагов «После вставки».</param>
    /// <returns>Что мешает сохранить и замечания к пересечениям и шагам.</returns>
    public static SnippetValidationResult Validate(
        SnippetDraft draft, IReadOnlyList<Snippet> others, IReadOnlyList<LayoutTable> tables, string scriptsDir)
    {
        var problems = SnippetProblems.None;
        if (string.IsNullOrEmpty(draft.Keys))
        {
            problems |= SnippetProblems.KeysEmpty;
        }

        if (string.IsNullOrEmpty(draft.Text))
        {
            problems |= SnippetProblems.TextEmpty;
        }

        IReadOnlyList<StepProblem> stepProblems = [.. draft.After.Select(s => StepValidation.Check(s, scriptsDir))];
        if (stepProblems.Any(p => p != StepProblem.None))
        {
            problems |= SnippetProblems.StepError;
        }

        Snippet? taken = null;
        List<SnippetOverlap> overlaps = [];
        if (draft.Keys.Length > 0)
        {
            var ignoreShift = SnippetKeys.BuildIgnoreShift(tables);
            if (SnippetKeys.Parse(draft.Keys, tables, ignoreShift) is not { } pattern)
            {
                problems |= SnippetProblems.KeysUntypable;
            }
            else if (draft.Enabled)
            {
                // Выключенные сокращения компилятор пропускает, поэтому они ни с чем не конфликтуют.
                foreach (var other in others.Where(o => o.Enabled))
                {
                    if (SnippetKeys.Parse(other.Keys, tables, ignoreShift) is not { } otherPattern)
                    {
                        continue;
                    }

                    var relation = SnippetKeys.Compare(pattern, otherPattern);
                    if (relation == SnippetRelation.Same)
                    {
                        taken ??= other;
                    }
                    else if (relation != SnippetRelation.None)
                    {
                        overlaps.Add(new SnippetOverlap(other, relation));
                    }
                }

                if (taken is not null)
                {
                    problems |= SnippetProblems.KeysTaken;
                }
            }
        }

        return new SnippetValidationResult(problems, taken, overlaps, stepProblems);
    }
}
