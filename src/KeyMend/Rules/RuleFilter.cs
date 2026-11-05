namespace KeyMend.Rules;

/// <summary>Функция KeyMend, к которой относится правило; по ней применяются исключения программ (SPEC F7).</summary>
internal enum RuleCategory
{
    /// <summary>Переключение языка.</summary>
    LayoutSwitch,

    /// <summary>Исправление ввода.</summary>
    Correction,

    /// <summary>Остальные правила.</summary>
    Rules,
}

/// <summary>Чистая логика выбора правил, действующих сейчас: по программе переднего плана, исключениям и паузе.</summary>
internal static class RuleFilter
{
    /// <summary>Определяет функцию правила: есть шаг исправления — исправление, иначе есть переключение языка — язык, иначе остальные.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Категория правила.</returns>
    public static RuleCategory CategoryOf(Rule rule)
    {
        if (rule.Steps.Any(s => s is CorrectStep))
        {
            return RuleCategory.Correction;
        }

        return rule.Steps.Any(s => s is SwitchLayoutStep) ? RuleCategory.LayoutSwitch : RuleCategory.Rules;
    }

    /// <summary>Является ли правило «снятием паузы»: только такие правила работают на паузе, иначе её нельзя было бы снять клавишей.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns><c>true</c>, если среди шагов есть переключение паузы.</returns>
    public static bool TogglesPause(Rule rule) =>
        rule.Steps.Any(s => s is ServiceStep { Action: ServiceAction.TogglePause });

    /// <summary>Выбирает правила, которые действуют для программы переднего плана.</summary>
    /// <param name="rules">Все правила.</param>
    /// <param name="processName">Имя exe окна переднего плана или <c>null</c>, если оно неизвестно.</param>
    /// <param name="exclusions">Исключения программ.</param>
    /// <param name="paused">Стоит ли KeyMend на паузе.</param>
    /// <returns>Включённые правила, подходящие по программе, категории и паузе, в исходном порядке.</returns>
    public static IReadOnlyList<Rule> Select(
        IReadOnlyList<Rule> rules, string? processName, IReadOnlyList<ExclusionEntry> exclusions, bool paused)
    {
        var exclusion = FindExclusion(exclusions, processName);
        var result = new List<Rule>();
        foreach (var rule in rules)
        {
            if (!rule.Enabled || !AppliesToProcess(rule, processName))
            {
                continue;
            }

            if (paused ? !TogglesPause(rule) : !IsAllowed(exclusion, CategoryOf(rule)))
            {
                continue;
            }

            result.Add(rule);
        }

        return result;
    }

    /// <summary>Разрешена ли автозамена в программе (SPEC F7).</summary>
    /// <param name="exclusions">Исключения программ.</param>
    /// <param name="processName">Имя exe или <c>null</c>.</param>
    /// <returns><c>true</c>, если автозамена не отключена исключением.</returns>
    public static bool SnippetsAllowed(IReadOnlyList<ExclusionEntry> exclusions, string? processName) =>
        FindExclusion(exclusions, processName)?.Snippets ?? true;

    /// <summary>Подходит ли правило к программе: пустой список — везде; без имени программы работают только правила «везде».</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="processName">Имя exe или <c>null</c>.</param>
    /// <returns><c>true</c>, если правило действует в этой программе.</returns>
    private static bool AppliesToProcess(Rule rule, string? processName) =>
        rule.Processes.Count == 0
        || (processName is not null && rule.Processes.Contains(processName, StringComparer.OrdinalIgnoreCase));

    /// <summary>Находит исключение для программы.</summary>
    /// <param name="exclusions">Исключения.</param>
    /// <param name="processName">Имя exe или <c>null</c>.</param>
    /// <returns>Запись или <c>null</c>.</returns>
    private static ExclusionEntry? FindExclusion(IReadOnlyList<ExclusionEntry> exclusions, string? processName) =>
        processName is null
            ? null
            : exclusions.FirstOrDefault(e => string.Equals(e.Process, processName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Разрешена ли функция в программе с данным исключением.</summary>
    /// <param name="exclusion">Исключение или <c>null</c>, если программы нет в списке.</param>
    /// <param name="category">Функция.</param>
    /// <returns><c>true</c>, если функция не отключена.</returns>
    private static bool IsAllowed(ExclusionEntry? exclusion, RuleCategory category) => exclusion is null || category switch
    {
        RuleCategory.LayoutSwitch => exclusion.LayoutSwitch,
        RuleCategory.Correction => exclusion.Correction,
        _ => exclusion.Rules,
    };
}
