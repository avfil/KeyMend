using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>Проверки триггера при сохранении правила (SPEC 3.1, п. 4): занятые клавиши, системные сочетания, смена языка Windows.</summary>
internal static class TriggerChecks
{
    /// <summary>Находит замечания к триггеру правила.</summary>
    /// <param name="candidate">Правило, которое сохраняют; при редактировании — с тем же <see cref="Rule.Id"/>, что и в списке.</param>
    /// <param name="rules">Все правила настроек; само правило в списке пропускается.</param>
    /// <param name="windows">Сочетания, на которых Windows меняет язык или раскладку.</param>
    /// <returns>Замечания: сначала ошибки, затем предупреждения и сведения.</returns>
    public static IReadOnlyList<TriggerIssue> Check(Rule candidate, IReadOnlyList<Rule> rules, LanguageHotkeys windows)
    {
        var issues = new List<TriggerIssue>();
        if (SystemShortcuts.IsReserved(candidate.Trigger))
        {
            issues.Add(new TriggerIssue(TriggerIssueKind.System, TriggerIssueSeverity.Error));
        }

        // Выключенное правило ни с кем не конфликтует: так можно хранить запасной вариант на тех же клавишах.
        if (candidate.Enabled)
        {
            AddConflicts(issues, candidate, rules);
        }

        if (SwitchesWindowsLanguage(candidate.Trigger, windows))
        {
            issues.Add(new TriggerIssue(TriggerIssueKind.WindowsSwitch, TriggerIssueSeverity.Warning));
        }

        return [.. issues.OrderByDescending(issue => issue.Severity)];
    }

    /// <summary>Добавляет «занято» и «задержка одиночного» по другим включённым правилам.</summary>
    private static void AddConflicts(List<TriggerIssue> issues, Rule candidate, IReadOnlyList<Rule> rules)
    {
        var isDouble = candidate.Trigger.DoubleMs is not null;
        foreach (var other in rules)
        {
            if (!other.Enabled
                || IsSameRule(candidate, other)
                || !ProcessesOverlap(candidate.Processes, other.Processes)
                || !TriggerOverlap.Overlaps(candidate.Trigger, other.Trigger))
            {
                continue;
            }

            var otherIsDouble = other.Trigger.DoubleMs is not null;
            if (isDouble == otherIsDouble)
            {
                issues.Add(new TriggerIssue(TriggerIssueKind.Busy, TriggerIssueSeverity.Error, other));
            }
            else
            {
                // Одиночное ждёт конец интервала двойного (SPEC 3.1, п. 1).
                var doubleMs = (isDouble ? candidate : other).Trigger.DoubleMs;
                issues.Add(new TriggerIssue(TriggerIssueKind.DoubleDelay, TriggerIssueSeverity.Info, other, doubleMs));
            }
        }
    }

    /// <summary>Одно ли это правило: совпадает ссылка или непустой идентификатор.</summary>
    private static bool IsSameRule(Rule a, Rule b) =>
        ReferenceEquals(a, b) || (a.Id.Length > 0 && string.Equals(a.Id, b.Id, StringComparison.Ordinal));

    /// <summary>Работают ли правила хотя бы в одной общей программе; пустой список — «везде».</summary>
    private static bool ProcessesOverlap(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return true;
        }

        foreach (var name in a)
        {
            foreach (var other in b)
            {
                if (string.Equals(name, other, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Нажимает ли триггер ровно Ctrl+Shift или Alt+Shift (без других модификаторов), которыми Windows меняет язык.
    /// Модификаторы нажатия Windows видит всегда, даже когда KeyMend подавляет основную клавишу.
    /// </summary>
    private static bool SwitchesWindowsLanguage(Trigger trigger, LanguageHotkeys windows)
    {
        var mods = trigger switch
        {
            TapTrigger tap => tap.Keys | tap.Hold,
            PressTrigger press => press.Hold,
            _ => ModifierSet.None,
        };

        var ctrl = Has(mods, ModifierSet.LCtrl | ModifierSet.RCtrl | ModifierSet.AnyCtrl);
        var shift = Has(mods, ModifierSet.LShift | ModifierSet.RShift | ModifierSet.AnyShift);
        var alt = Has(mods, ModifierSet.LAlt | ModifierSet.RAlt | ModifierSet.AnyAlt);
        var win = Has(mods, ModifierSet.LWin | ModifierSet.RWin | ModifierSet.AnyWin);
        if (!shift || win)
        {
            return false;
        }

        // Windows знает только левый Alt в сочетании Alt+Shift.
        var leftAlt = Has(mods, ModifierSet.LAlt | ModifierSet.AnyAlt);
        return (ctrl && !alt && windows.CtrlShift) || (alt && !ctrl && leftAlt && windows.AltShift);
    }

    /// <summary>Есть ли в наборе хотя бы один из битов маски.</summary>
    private static bool Has(ModifierSet set, ModifierSet mask) => (set & mask) != 0;
}
