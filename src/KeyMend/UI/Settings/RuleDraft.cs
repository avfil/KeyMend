using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>
/// Правило в редакторе до сохранения. В отличие от <see cref="Rule"/>, допускает незаписанные клавиши и режим
/// «только в выбранных» с пустым списком: пока владелец не закончил, это не ошибка модели.
/// </summary>
internal sealed record RuleDraft
{
    /// <summary>Идентификатор правила; у нового пуст.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Название.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Включено ли правило.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Клавиши или <c>null</c>, если ещё не записаны.</summary>
    public Trigger? Trigger { get; init; }

    /// <summary>Выбран режим «только в выбранных программах».</summary>
    public bool OnlySelected { get; init; }

    /// <summary>Программы режима «только в выбранных».</summary>
    public IReadOnlyList<string> Processes { get; init; } = [];

    /// <summary>Шаги цепочки.</summary>
    public IReadOnlyList<Step> Steps { get; init; } = [];

    /// <summary>Строит черновик из правила.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Черновик с теми же значениями.</returns>
    public static RuleDraft From(Rule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Enabled = rule.Enabled,
        Trigger = rule.Trigger,
        OnlySelected = rule.Processes.Count > 0,
        Processes = rule.Processes,
        Steps = rule.Steps,
    };

    /// <summary>Превращает черновик в правило: при «везде» список программ пуст, как бы ни был заполнен.</summary>
    /// <param name="trigger">Клавиши; к моменту вызова записаны.</param>
    /// <returns>Правило.</returns>
    public Rule ToRule(Trigger trigger) => new()
    {
        Id = Id,
        Name = Name.Trim(),
        Enabled = Enabled,
        Trigger = trigger,
        Processes = OnlySelected ? Processes : [],
        Steps = Steps,
    };
}
