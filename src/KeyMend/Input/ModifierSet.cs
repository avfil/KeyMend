namespace KeyMend.Input;

/// <summary>
/// Набор модификаторов с учётом стороны (SPEC 2.4): восемь сторонних битов и четыре бита «любой». Бит «любой» значит
/// «левый, правый или оба»; в одном семействе (Ctrl, Shift, Alt, Win) его нельзя смешивать со сторонними битами.
/// </summary>
[Flags]
internal enum ModifierSet
{
    /// <summary>Нет модификаторов.</summary>
    None = 0,

    /// <summary>Левый Ctrl.</summary>
    LCtrl = 1 << 0,

    /// <summary>Правый Ctrl.</summary>
    RCtrl = 1 << 1,

    /// <summary>Левый Shift.</summary>
    LShift = 1 << 2,

    /// <summary>Правый Shift.</summary>
    RShift = 1 << 3,

    /// <summary>Левый Alt.</summary>
    LAlt = 1 << 4,

    /// <summary>Правый Alt.</summary>
    RAlt = 1 << 5,

    /// <summary>Левая Win.</summary>
    LWin = 1 << 6,

    /// <summary>Правая Win.</summary>
    RWin = 1 << 7,

    /// <summary>Любой Ctrl.</summary>
    AnyCtrl = 1 << 8,

    /// <summary>Любой Shift.</summary>
    AnyShift = 1 << 9,

    /// <summary>Любой Alt.</summary>
    AnyAlt = 1 << 10,

    /// <summary>Любая Win.</summary>
    AnyWin = 1 << 11,
}
