using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>Сочетания Windows, которые нельзя назначить правилу (SPEC 5, «Поле клавиш и проверки»).</summary>
internal static class SystemShortcuts
{
    /// <summary>Виртуальный код клавиши L.</summary>
    internal const VirtualKey KeyL = (VirtualKey)0x4C;

    /// <summary>Виртуальный код клавиши F4.</summary>
    internal const VirtualKey KeyF4 = (VirtualKey)0x73;

    /// <summary>Занятые системой сочетания; модификаторы заданы как «любой», сторона не важна.</summary>
    private static readonly PressTrigger[] _reserved =
    [
        Press(ModifierSet.AnyWin, KeyL),
        Press(ModifierSet.AnyWin, VirtualKey.Tab),
        Press(ModifierSet.AnyCtrl | ModifierSet.AnyAlt, VirtualKey.Delete),
        Press(ModifierSet.AnyCtrl | ModifierSet.AnyShift, VirtualKey.Escape),
        Press(ModifierSet.AnyCtrl, VirtualKey.Escape),
        Press(ModifierSet.AnyAlt, VirtualKey.Tab),
        Press(ModifierSet.AnyAlt | ModifierSet.AnyShift, VirtualKey.Tab),
        Press(ModifierSet.AnyCtrl | ModifierSet.AnyAlt, VirtualKey.Tab),
        Press(ModifierSet.AnyAlt, VirtualKey.Escape),
        Press(ModifierSet.AnyAlt, KeyF4),
    ];

    /// <summary>Совпадает ли триггер с системным сочетанием.</summary>
    /// <param name="trigger">Проверяемый триггер.</param>
    /// <returns><c>true</c>, если нажатие зарезервировано Windows.</returns>
    public static bool IsReserved(Trigger trigger)
    {
        foreach (var shortcut in _reserved)
        {
            if (TriggerOverlap.Overlaps(trigger, shortcut))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Создаёт описание сочетания.</summary>
    private static PressTrigger Press(ModifierSet hold, VirtualKey key) => new() { Hold = hold, Key = key };
}
