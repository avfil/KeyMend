using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>
/// Определяет, могут ли два триггера сработать на одно и то же физическое нажатие. Нужен проверке конфликтов (SPEC 3.1, п. 4).
/// Условия совпадают с условиями <see cref="HotkeyEngine"/>, интервал двойного нажатия не учитывается.
/// </summary>
internal static class TriggerOverlap
{
    /// <summary>Число наборов зажатых модификаторов со стороной: восемь битов.</summary>
    internal const int SidedSetCount = 1 << 8;

    /// <summary>Пересекаются ли триггеры: существует набор зажатых клавиш, на котором срабатывают оба.</summary>
    /// <param name="a">Первый триггер.</param>
    /// <param name="b">Второй триггер.</param>
    /// <returns><c>true</c>, если одно нажатие может запустить оба; Tap и Press не пересекаются.</returns>
    public static bool Overlaps(Trigger a, Trigger b)
    {
        if (a is PressTrigger pa && b is PressTrigger pb && pa.Key != pb.Key)
        {
            return false;
        }

        for (var bits = 0; bits < SidedSetCount; bits++)
        {
            var held = (ModifierSet)bits;
            if (Fires(a, held) && Fires(b, held) && SameKind(a, b))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Относятся ли триггеры к одному виду жеста.</summary>
    private static bool SameKind(Trigger a, Trigger b) => a.GetType() == b.GetType();

    /// <summary>Срабатывает ли триггер, когда зажат ровно набор <paramref name="held"/> (для Tap — в момент отпускания).</summary>
    private static bool Fires(Trigger trigger, ModifierSet held) => trigger switch
    {
        TapTrigger tap => (held & tap.Keys) == tap.Keys && ModifierSets.Matches(tap.Hold, held & ~tap.Keys),
        PressTrigger press => ModifierSets.Matches(press.Hold, held),
        _ => false,
    };
}
