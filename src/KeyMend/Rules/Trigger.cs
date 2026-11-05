using System.Text.Json.Serialization;
using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>
/// Что запускает правило (SPEC 3.1). В <c>settings.json</c> триггер — объект с полем <c>kind</c> и строками клавиш;
/// чтение и запись выполняет <see cref="TriggerJsonConverter"/>.
/// </summary>
[JsonConverter(typeof(TriggerJsonConverter))]
internal abstract record Trigger
{
    /// <summary>Интервал двойного нажатия в миллисекундах; <c>null</c> — одиночное срабатывание.</summary>
    public int? DoubleMs { get; init; }
}

/// <summary>
/// Короткое нажатие модификаторов: нажаты и отпущены, между ними нет других клавиш и кликов. Может быть с удерживаемым
/// модификатором: «держать RCtrl, нажать RShift».
/// </summary>
internal sealed record TapTrigger : Trigger
{
    /// <summary>Значение <see cref="MaxHoldMs"/> по умолчанию.</summary>
    public const int DefaultMaxHoldMs = 150;

    /// <summary>Нажимаемые модификаторы; только со стороной, не пусто.</summary>
    public required ModifierSet Keys { get; init; }

    /// <summary>Что должно удерживаться во время нажатия (кроме <see cref="Keys"/>); может быть «любой».</summary>
    public ModifierSet Hold { get; init; }

    /// <summary>Самое долгое удержание, которое ещё считается коротким, в миллисекундах.</summary>
    public int MaxHoldMs { get; init; } = DefaultMaxHoldMs;
}

/// <summary>Немодификаторная клавиша с модификаторами или без них (<c>F18</c>, <c>LCtrl+RCtrl+1</c>); срабатывает на нажатие.</summary>
internal sealed record PressTrigger : Trigger
{
    /// <summary>Модификаторы, которые должны быть зажаты (и никакие другие).</summary>
    public ModifierSet Hold { get; init; }

    /// <summary>Основная клавиша.</summary>
    public required VirtualKey Key { get; init; }
}
