namespace KeyMend.Input;

/// <summary>Виртуальные коды клавиш, нужные приложению. Модификаторы различаются по сторонам, как их отдаёт низкоуровневый хук.</summary>
internal enum VirtualKey : ushort
{
    /// <summary>Отсутствие клавиши.</summary>
    None = 0,

    /// <summary>Backspace.</summary>
    Back = 0x08,

    /// <summary>Tab.</summary>
    Tab = 0x09,

    /// <summary>Enter.</summary>
    Return = 0x0D,

    /// <summary>Pause.</summary>
    Pause = 0x13,

    /// <summary>CapsLock.</summary>
    CapsLock = 0x14,

    /// <summary>Esc.</summary>
    Escape = 0x1B,

    /// <summary>Пробел.</summary>
    Space = 0x20,

    /// <summary>Page Up.</summary>
    PageUp = 0x21,

    /// <summary>Page Down.</summary>
    PageDown = 0x22,

    /// <summary>End.</summary>
    End = 0x23,

    /// <summary>Home.</summary>
    Home = 0x24,

    /// <summary>Стрелка влево.</summary>
    Left = 0x25,

    /// <summary>Стрелка вверх.</summary>
    Up = 0x26,

    /// <summary>Стрелка вправо.</summary>
    Right = 0x27,

    /// <summary>Стрелка вниз.</summary>
    Down = 0x28,

    /// <summary>Insert.</summary>
    Insert = 0x2D,

    /// <summary>Delete.</summary>
    Delete = 0x2E,

    /// <summary>Клавиша C: вместе с Ctrl копирует выделенное.</summary>
    C = 0x43,

    /// <summary>Клавиша V: вместе с Ctrl вставляет из буфера обмена.</summary>
    V = 0x56,

    /// <summary>Левая Win.</summary>
    LWin = 0x5B,

    /// <summary>Правая Win.</summary>
    RWin = 0x5C,

    /// <summary>NumLock.</summary>
    NumLock = 0x90,

    /// <summary>ScrollLock.</summary>
    ScrollLock = 0x91,

    /// <summary>Левый Shift.</summary>
    LShift = 0xA0,

    /// <summary>Правый Shift.</summary>
    RShift = 0xA1,

    /// <summary>Левый Ctrl.</summary>
    LControl = 0xA2,

    /// <summary>Правый Ctrl.</summary>
    RControl = 0xA3,

    /// <summary>Левый Alt.</summary>
    LMenu = 0xA4,

    /// <summary>Правый Alt.</summary>
    RMenu = 0xA5,

    /// <summary>Свободный код, которым маскируют одиночное нажатие Alt или Win (SPEC 3.1).</summary>
    Mask = 0xE8,
}
