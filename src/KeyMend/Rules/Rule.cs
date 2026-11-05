namespace KeyMend.Rules;

/// <summary>Правило: триггер запускает цепочку шагов (SPEC 3). Встроенные функции — тоже правила.</summary>
internal sealed record Rule
{
    /// <summary>Устойчивый идентификатор правила.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Имя правила для списка и логов.</summary>
    public required string Name { get; init; }

    /// <summary>Включено ли правило.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Имена exe, в которых правило работает, без учёта регистра; пусто — везде.</summary>
    public IReadOnlyList<string> Processes { get; init; } = [];

    /// <summary>Что запускает правило.</summary>
    public required Trigger Trigger { get; init; }

    /// <summary>Цепочка шагов.</summary>
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

/// <summary>Исключение для программы (SPEC F7): какие функции в ней отключены; по умолчанию все включены.</summary>
internal sealed record ExclusionEntry
{
    /// <summary>Имя exe без учёта регистра, например <c>mstsc.exe</c>.</summary>
    public required string Process { get; init; }

    /// <summary>Работает ли переключение языка.</summary>
    public bool LayoutSwitch { get; init; } = true;

    /// <summary>Работает ли исправление ввода.</summary>
    public bool Correction { get; init; } = true;

    /// <summary>Работает ли автозамена.</summary>
    public bool Snippets { get; init; } = true;

    /// <summary>Работают ли остальные правила.</summary>
    public bool Rules { get; init; } = true;
}
