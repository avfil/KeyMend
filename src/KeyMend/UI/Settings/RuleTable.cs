using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Какие правила показывать по состоянию.</summary>
internal enum RuleStateFilter
{
    /// <summary>Все.</summary>
    All,

    /// <summary>Только включённые.</summary>
    Enabled,

    /// <summary>Только выключенные.</summary>
    Disabled,
}

/// <summary>Условия отбора правил в таблице.</summary>
/// <param name="Group">Выбранный чип группы; <c>null</c> — «Все».</param>
/// <param name="State">Состояние правила.</param>
/// <param name="Search">Строка поиска по названию и клавишам; пустая — без поиска.</param>
internal sealed record RuleTableFilter(RuleGroup? Group = null, RuleStateFilter State = RuleStateFilter.All, string Search = "");

/// <summary>Группа таблицы с подошедшими правилами.</summary>
/// <param name="Group">Группа.</param>
/// <param name="Rules">Правила группы в исходном порядке.</param>
internal sealed record RuleTableGroup(RuleGroup Group, IReadOnlyList<Rule> Rules);

/// <summary>Содержимое таблицы и числа на чипах.</summary>
/// <param name="Groups">Непустые группы в порядке показа.</param>
/// <param name="Counts">Сколько правил в каждой группе при текущем состоянии и поиске, без учёта выбранного чипа.</param>
/// <param name="Total">Сумма <paramref name="Counts"/> — число на чипе «Все».</param>
internal sealed record RuleTableView(IReadOnlyList<RuleTableGroup> Groups, IReadOnlyDictionary<RuleGroup, int> Counts, int Total);

/// <summary>Отбор и группировка правил для таблицы «Правила».</summary>
internal static class RuleTable
{
    /// <summary>Отбирает правила по фильтру и раскладывает по группам.</summary>
    /// <param name="rules">Все правила.</param>
    /// <param name="filter">Условия отбора.</param>
    /// <returns>Группы для показа и числа для чипов.</returns>
    public static RuleTableView Build(IReadOnlyList<Rule> rules, RuleTableFilter filter)
    {
        var search = filter.Search.Trim();
        var matched = rules.Where(rule => MatchesState(rule, filter.State) && MatchesSearch(rule, search)).ToList();
        var counts = RuleGroups.All.ToDictionary(group => group, group => matched.Count(rule => RuleGroups.Of(rule) == group));
        var groups = RuleGroups.All
            .Where(group => filter.Group is null || filter.Group == group)
            .Select(group => new RuleTableGroup(group, [.. matched.Where(rule => RuleGroups.Of(rule) == group)]))
            .Where(group => group.Rules.Count > 0)
            .ToList();
        return new RuleTableView(groups, counts, counts.Values.Sum());
    }

    /// <summary>Подходит ли правило под фильтр состояния.</summary>
    private static bool MatchesState(Rule rule, RuleStateFilter state) => state switch
    {
        RuleStateFilter.Enabled => rule.Enabled,
        RuleStateFilter.Disabled => !rule.Enabled,
        _ => true,
    };

    /// <summary>Есть ли строка поиска в названии или в записи клавиш, без учёта регистра.</summary>
    private static bool MatchesSearch(Rule rule, string search) => search.Length == 0
        || rule.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
        || TriggerDisplay.Describe(rule.Trigger).Contains(search, StringComparison.OrdinalIgnoreCase);
}
