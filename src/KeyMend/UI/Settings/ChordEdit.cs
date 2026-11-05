using KeyMend.Input;

namespace KeyMend.UI.Settings;

/// <summary>Правка сочетания из меню «Выбрать ▾» в шаге «Нажать клавиши».</summary>
internal static class ChordEdit
{
    /// <summary>Биты семейства: левый, правый и «любой».</summary>
    /// <param name="family">Бит «любой» нужного семейства (<see cref="ModifierSet.AnyCtrl"/> и т. п.).</param>
    /// <returns>Все три бита семейства.</returns>
    private static ModifierSet FamilyBits(ModifierSet family) => family switch
    {
        ModifierSet.AnyCtrl => ModifierSet.LCtrl | ModifierSet.RCtrl | ModifierSet.AnyCtrl,
        ModifierSet.AnyShift => ModifierSet.LShift | ModifierSet.RShift | ModifierSet.AnyShift,
        ModifierSet.AnyAlt => ModifierSet.LAlt | ModifierSet.RAlt | ModifierSet.AnyAlt,
        _ => ModifierSet.LWin | ModifierSet.RWin | ModifierSet.AnyWin,
    };

    /// <summary>Есть ли в сочетании модификатор семейства, левый, правый или любой.</summary>
    /// <param name="chord">Сочетание.</param>
    /// <param name="family">Бит «любой» нужного семейства.</param>
    /// <returns><c>true</c>, если модификатор есть.</returns>
    public static bool HasModifier(KeyChord chord, ModifierSet family) => (chord.Modifiers & FamilyBits(family)) != 0;

    /// <summary>Включает или выключает модификатор: есть — снимает все его варианты, нет — ставит «любой».</summary>
    /// <param name="chord">Сочетание.</param>
    /// <param name="family">Бит «любой» нужного семейства.</param>
    /// <returns>Новое сочетание.</returns>
    public static KeyChord ToggleModifier(KeyChord chord, ModifierSet family)
    {
        var bits = FamilyBits(family);
        var modifiers = HasModifier(chord, family) ? chord.Modifiers & ~bits : chord.Modifiers | family;
        return chord with { Modifiers = modifiers };
    }

    /// <summary>Задаёт основную клавишу.</summary>
    /// <param name="chord">Сочетание.</param>
    /// <param name="key">Клавиша.</param>
    /// <returns>Новое сочетание с теми же модификаторами.</returns>
    public static KeyChord SetKey(KeyChord chord, VirtualKey key) => chord with { Key = key };
}
