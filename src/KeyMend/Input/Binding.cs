using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>Вид привязки в движке триггеров.</summary>
internal enum BindingKind
{
    /// <summary>Короткое нажатие модификаторов, срабатывает на отпускание.</summary>
    Tap,

    /// <summary>Немодификаторная клавиша, срабатывает на нажатие.</summary>
    Press,
}

/// <summary>
/// «Основа» триггера без временных настроек. Привязки с одинаковой основой образуют пару «одиночное + двойное»
/// (SPEC 3.1, правило 1).
/// </summary>
/// <param name="Kind">Вид привязки.</param>
/// <param name="Keys">Нажимаемые модификаторы (для tap).</param>
/// <param name="Key">Основная клавиша (для press).</param>
/// <param name="Hold">Удерживаемые модификаторы.</param>
internal readonly record struct BindingBase(BindingKind Kind, ModifierSet Keys, VirtualKey Key, ModifierSet Hold);

/// <summary>Правило, подготовленное для движка: основа триггера, клавиши и пороги времени уже разобраны.</summary>
internal sealed class Binding
{
    /// <summary>Создаёт привязку из правила.</summary>
    /// <param name="rule">Правило с триггером tap или press.</param>
    /// <exception cref="ArgumentException">Неизвестный вид триггера.</exception>
    public Binding(Rule rule)
    {
        Rule = rule;
        switch (rule.Trigger)
        {
            case TapTrigger tap:
                Base = new BindingBase(BindingKind.Tap, tap.Keys, VirtualKey.None, tap.Hold);
                KeySet = tap.Keys;
                Keys = ModifierSets.SidedKeys.Where(k => (ModifierSets.FromKey(k) & tap.Keys) != 0).ToArray();
                MaxHoldMs = tap.MaxHoldMs;
                break;
            case PressTrigger press:
                Base = new BindingBase(BindingKind.Press, ModifierSet.None, press.Key, press.Hold);
                Keys = [];
                MaxHoldMs = 0;
                break;
            default:
                throw new ArgumentException("unknown trigger kind", nameof(rule));
        }

        DoubleMs = rule.Trigger.DoubleMs;
    }

    /// <summary>Исходное правило.</summary>
    public Rule Rule { get; }

    /// <summary>Основа триггера.</summary>
    public BindingBase Base { get; }

    /// <summary>Нажимаемые модификаторы tap одним набором; для press пусто.</summary>
    public ModifierSet KeySet { get; }

    /// <summary>Нажимаемые модификаторы tap по клавишам; для press пусто.</summary>
    public VirtualKey[] Keys { get; }

    /// <summary>Самое долгое удержание tap, мс.</summary>
    public int MaxHoldMs { get; }

    /// <summary>Интервал двойного нажатия, мс; <c>null</c> — одиночное.</summary>
    public int? DoubleMs { get; }

    /// <summary>Основная клавиша press.</summary>
    public VirtualKey Key => Base.Key;

    /// <summary>Удерживаемые модификаторы.</summary>
    public ModifierSet Hold => Base.Hold;
}
