using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.Input;

/// <summary>
/// Наборы привязок для всех случаев, собранные заранее (при загрузке или изменении настроек): колбэк хука только берёт
/// готовый набор по имени программы и не выделяет память.
/// </summary>
internal sealed class BindingTables
{
    /// <summary>Пустой комплект.</summary>
    public static readonly BindingTables Empty = new(BindingTable.Empty, BindingTable.Empty, new Dictionary<string, BindingTable>());

    /// <summary>Наборы для программ, упомянутых в правилах или исключениях.</summary>
    private readonly Dictionary<string, BindingTable> _byProcess;

    /// <summary>Создаёт комплект.</summary>
    /// <param name="defaultTable">Набор для остальных программ и для неизвестной программы.</param>
    /// <param name="paused">Набор на паузе KeyMend.</param>
    /// <param name="byProcess">Наборы для отдельных программ.</param>
    private BindingTables(BindingTable defaultTable, BindingTable paused, Dictionary<string, BindingTable> byProcess)
    {
        Default = defaultTable;
        Paused = paused;
        _byProcess = byProcess;
    }

    /// <summary>Набор для программ, о которых в настройках ничего не сказано.</summary>
    public BindingTable Default { get; }

    /// <summary>Набор на паузе: только правила, снимающие паузу.</summary>
    public BindingTable Paused { get; }

    /// <summary>Собирает комплект из настроек.</summary>
    /// <param name="rules">Все правила.</param>
    /// <param name="exclusions">Исключения программ.</param>
    /// <param name="snippets">Сокращения автозамены или <c>null</c>; на паузе и в программах с отключённой автозаменой не действуют.</param>
    /// <param name="ownProcess">
    /// Имя exe самого KeyMend или <c>null</c>: в его окне автозамена не действует, иначе сокращение, набранное в поле
    /// редактора, тут же заменилось бы текстом. Правила в окне работают как везде.
    /// </param>
    /// <returns>Комплект наборов.</returns>
    public static BindingTables Build(
        IReadOnlyList<Rule> rules, IReadOnlyList<ExclusionEntry> exclusions, SnippetMatcher? snippets = null, string? ownProcess = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ownProcess is not null)
        {
            names.Add(ownProcess);
        }

        foreach (var exclusion in exclusions)
        {
            names.Add(exclusion.Process);
        }

        foreach (var rule in rules)
        {
            names.UnionWith(rule.Processes);
        }

        var matcher = snippets ?? SnippetMatcher.Empty;
        var byProcess = new Dictionary<string, BindingTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            byProcess[name] = BindingTable.Build(
                RuleFilter.Select(rules, name, exclusions, paused: false),
                RuleFilter.SnippetsAllowed(exclusions, name) && !string.Equals(name, ownProcess, StringComparison.OrdinalIgnoreCase)
                    ? matcher
                    : null);
        }

        return new BindingTables(
            BindingTable.Build(RuleFilter.Select(rules, null, exclusions, paused: false), matcher),
            BindingTable.Build(RuleFilter.Select(rules, null, exclusions, paused: true)),
            byProcess);
    }

    /// <summary>Возвращает набор для программы переднего плана.</summary>
    /// <param name="processName">Имя exe или <c>null</c>, если определить не удалось.</param>
    /// <returns>Набор привязок.</returns>
    public BindingTable For(string? processName) =>
        processName is not null && _byProcess.TryGetValue(processName, out var table) ? table : Default;
}
