namespace KeyMend.Input;

/// <summary>Операции над <see cref="ModifierSet"/>: перевод из клавиш, проверка корректности и сопоставление с зажатыми клавишами.</summary>
internal static class ModifierSets
{
    /// <summary>Все биты «любой».</summary>
    internal const ModifierSet AnyMask = ModifierSet.AnyCtrl | ModifierSet.AnyShift | ModifierSet.AnyAlt | ModifierSet.AnyWin;

    /// <summary>Семейства модификаторов: левый и правый биты, бит «любой».</summary>
    private static readonly (ModifierSet Sided, ModifierSet Any)[] _families =
    [
        (ModifierSet.LCtrl | ModifierSet.RCtrl, ModifierSet.AnyCtrl),
        (ModifierSet.LShift | ModifierSet.RShift, ModifierSet.AnyShift),
        (ModifierSet.LAlt | ModifierSet.RAlt, ModifierSet.AnyAlt),
        (ModifierSet.LWin | ModifierSet.RWin, ModifierSet.AnyWin),
    ];

    /// <summary>Все модификаторы со стороной в порядке, в котором их удобно перебирать.</summary>
    internal static readonly VirtualKey[] SidedKeys =
    [
        VirtualKey.LControl, VirtualKey.RControl, VirtualKey.LShift, VirtualKey.RShift,
        VirtualKey.LMenu, VirtualKey.RMenu, VirtualKey.LWin, VirtualKey.RWin,
    ];

    /// <summary>Является ли клавиша модификатором со стороной (Shift, Ctrl, Alt, Win).</summary>
    /// <param name="vk">Виртуальный код.</param>
    /// <returns><c>true</c> для восьми модификаторов со стороной.</returns>
    public static bool IsModifier(VirtualKey vk) => FromKey(vk) != ModifierSet.None;

    /// <summary>Возвращает бит модификатора со стороной для клавиши.</summary>
    /// <param name="vk">Виртуальный код.</param>
    /// <returns>Бит модификатора или <see cref="ModifierSet.None"/>, если клавиша не модификатор.</returns>
    public static ModifierSet FromKey(VirtualKey vk) => vk switch
    {
        VirtualKey.LControl => ModifierSet.LCtrl,
        VirtualKey.RControl => ModifierSet.RCtrl,
        VirtualKey.LShift => ModifierSet.LShift,
        VirtualKey.RShift => ModifierSet.RShift,
        VirtualKey.LMenu => ModifierSet.LAlt,
        VirtualKey.RMenu => ModifierSet.RAlt,
        VirtualKey.LWin => ModifierSet.LWin,
        VirtualKey.RWin => ModifierSet.RWin,
        _ => ModifierSet.None,
    };

    /// <summary>Возвращает клавишу для одного стороннего бита.</summary>
    /// <param name="bit">Ровно один сторонний бит.</param>
    /// <returns>Виртуальный код или <see cref="VirtualKey.None"/>, если бит не сторонний.</returns>
    public static VirtualKey ToKey(ModifierSet bit)
    {
        foreach (var key in SidedKeys)
        {
            if (FromKey(key) == bit)
            {
                return key;
            }
        }

        return VirtualKey.None;
    }

    /// <summary>Допустима ли зажатая модификаторная клавиша при требовании: она названа в нём или семейство задано как «любой».</summary>
    /// <param name="required">Требуемый набор.</param>
    /// <param name="modifier">Модификатор со стороной.</param>
    /// <returns><c>true</c>, если требование разрешает эту клавишу.</returns>
    public static bool Permits(ModifierSet required, VirtualKey modifier)
    {
        var bit = FromKey(modifier);
        if (bit == ModifierSet.None)
        {
            return false;
        }

        if ((required & bit) != 0)
        {
            return true;
        }

        foreach (var (sided, any) in _families)
        {
            if ((sided & bit) != 0)
            {
                return (required & any) != 0;
            }
        }

        return false;
    }

    /// <summary>Есть ли в наборе Alt или Win (сторонние или «любой»): такие сочетания требуют маски, чтобы не открылось меню или «Пуск».</summary>
    /// <param name="set">Набор модификаторов.</param>
    /// <returns><c>true</c>, если набор содержит Alt или Win.</returns>
    public static bool HasAltOrWin(ModifierSet set) =>
        (set & (ModifierSet.LAlt | ModifierSet.RAlt | ModifierSet.AnyAlt | ModifierSet.LWin | ModifierSet.RWin | ModifierSet.AnyWin)) != 0;

    /// <summary>Является ли клавиша Alt или Win со стороной.</summary>
    /// <param name="vk">Виртуальный код.</param>
    /// <returns><c>true</c> для LAlt, RAlt, LWin, RWin.</returns>
    public static bool IsAltOrWin(VirtualKey vk) => HasAltOrWin(FromKey(vk));

    /// <summary>Есть ли в наборе биты «любой».</summary>
    /// <param name="set">Набор модификаторов.</param>
    /// <returns><c>true</c>, если хотя бы одно семейство задано как «любой».</returns>
    public static bool HasAny(ModifierSet set) => (set & AnyMask) != 0;

    /// <summary>Корректен ли набор: в одном семействе «любой» не смешивается со сторонними битами.</summary>
    /// <param name="set">Набор модификаторов.</param>
    /// <returns><c>true</c>, если противоречий нет.</returns>
    public static bool IsValid(ModifierSet set)
    {
        foreach (var (sided, any) in _families)
        {
            if ((set & any) != 0 && (set & sided) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Подходят ли зажатые клавиши под требование. В каждом семействе: требование «нет» — ничего не зажато; «левый» или
    /// «правый» — зажат именно он и только он; «оба» — зажаты оба; «любой» — зажат хотя бы один.
    /// </summary>
    /// <param name="required">Требуемый набор.</param>
    /// <param name="held">Что зажато сейчас; только сторонние биты.</param>
    /// <returns><c>true</c>, если зажатое отвечает требованию.</returns>
    public static bool Matches(ModifierSet required, ModifierSet held)
    {
        foreach (var (sided, any) in _families)
        {
            var heldSided = held & sided;
            var ok = (required & any) != 0 ? heldSided != 0 : heldSided == (required & sided);
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
