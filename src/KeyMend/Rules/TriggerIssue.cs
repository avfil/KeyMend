namespace KeyMend.Rules;

/// <summary>Тяжесть замечания к триггеру.</summary>
internal enum TriggerIssueSeverity
{
    /// <summary>Сведения: сохранить можно, но стоит знать.</summary>
    Info,

    /// <summary>Предупреждение: сохранить можно.</summary>
    Warning,

    /// <summary>Ошибка: сохранить нельзя.</summary>
    Error,
}

/// <summary>Вид замечания к триггеру.</summary>
internal enum TriggerIssueKind
{
    /// <summary>Клавиши уже заняты другим включённым правилом в пересекающемся наборе программ.</summary>
    Busy,

    /// <summary>Сочетание зарезервировано Windows.</summary>
    System,

    /// <summary>Сочетание Windows использует для смены языка или раскладки, язык может переключиться заодно.</summary>
    WindowsSwitch,

    /// <summary>Одиночное нажатие делит клавиши с двойным: одиночное сработает с задержкой.</summary>
    DoubleDelay,
}

/// <summary>Замечание к триггеру правила.</summary>
/// <param name="Kind">Вид замечания.</param>
/// <param name="Severity">Тяжесть.</param>
/// <param name="Other">Другое правило, о котором речь (для <see cref="TriggerIssueKind.Busy"/> и <see cref="TriggerIssueKind.DoubleDelay"/>).</param>
/// <param name="DelayMs">Задержка одиночного нажатия, мс (для <see cref="TriggerIssueKind.DoubleDelay"/>).</param>
internal sealed record TriggerIssue(
    TriggerIssueKind Kind,
    TriggerIssueSeverity Severity,
    Rule? Other = null,
    int? DelayMs = null);
