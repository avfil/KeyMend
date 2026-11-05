using System.Globalization;

namespace KeyMend.Input;

/// <summary>
/// Разбор и запись клавиш в читаемом виде: <c>RCtrl</c>, <c>LCtrl+RCtrl</c>, <c>Ctrl+RShift</c>, <c>F18</c>, <c>LCtrl+RCtrl+1</c>.
/// Регистр не важен. Произвольную клавишу можно задать шестнадцатеричным кодом: <c>0x7C</c>.
/// </summary>
internal static class KeyChordParser
{
    /// <summary>Виртуальный код F1; F-клавиши идут подряд до F24.</summary>
    internal const int F1 = 0x70;

    /// <summary>Имена модификаторов: со стороной и «любой».</summary>
    private static readonly (string Name, ModifierSet Bit)[] _modifierNames =
    [
        ("LCtrl", ModifierSet.LCtrl), ("RCtrl", ModifierSet.RCtrl), ("Ctrl", ModifierSet.AnyCtrl), ("Control", ModifierSet.AnyCtrl),
        ("LShift", ModifierSet.LShift), ("RShift", ModifierSet.RShift), ("Shift", ModifierSet.AnyShift),
        ("LAlt", ModifierSet.LAlt), ("RAlt", ModifierSet.RAlt), ("Alt", ModifierSet.AnyAlt),
        ("LWin", ModifierSet.LWin), ("RWin", ModifierSet.RWin), ("Win", ModifierSet.AnyWin),
    ];

    /// <summary>Имена остальных клавиш; первое имя для клавиши используется при записи.</summary>
    private static readonly (string Name, VirtualKey Key)[] _namedKeys =
    [
        ("Space", VirtualKey.Space),
        ("CapsLock", VirtualKey.CapsLock),
        ("ScrollLock", VirtualKey.ScrollLock),
        ("NumLock", VirtualKey.NumLock),
        ("Pause", VirtualKey.Pause),
        ("Tab", VirtualKey.Tab),
        ("Enter", VirtualKey.Return),
        ("Return", VirtualKey.Return),
        ("Esc", VirtualKey.Escape),
        ("Escape", VirtualKey.Escape),
        ("Backspace", VirtualKey.Back),
        ("Up", VirtualKey.Up),
        ("Down", VirtualKey.Down),
        ("Left", VirtualKey.Left),
        ("Right", VirtualKey.Right),
        ("Home", VirtualKey.Home),
        ("End", VirtualKey.End),
        ("PgUp", VirtualKey.PageUp),
        ("PgDn", VirtualKey.PageDown),
        ("Insert", VirtualKey.Insert),
        ("Delete", VirtualKey.Delete),
    ];

    /// <summary>Порядок записи модификаторов: Ctrl, Shift, Alt, Win; в семействе левый, правый, затем «любой».</summary>
    private static readonly ModifierSet[] _formatOrder =
    [
        ModifierSet.LCtrl, ModifierSet.RCtrl, ModifierSet.AnyCtrl,
        ModifierSet.LShift, ModifierSet.RShift, ModifierSet.AnyShift,
        ModifierSet.LAlt, ModifierSet.RAlt, ModifierSet.AnyAlt,
        ModifierSet.LWin, ModifierSet.RWin, ModifierSet.AnyWin,
    ];

    /// <summary>Пытается разобрать строку; при неудаче возвращает <c>false</c>.</summary>
    /// <param name="text">Строка клавиш, например <c>LCtrl+RCtrl+1</c>.</param>
    /// <param name="chord">Результат разбора.</param>
    /// <returns><c>true</c>, если строка корректна: нет повторов, нет противоречий «любой»/сторона, не больше одной основной клавиши.</returns>
    public static bool TryParse(string? text, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = ModifierSet.None;
        var key = VirtualKey.None;
        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();
            if (TryParseModifier(token, out var bit))
            {
                // Один и тот же модификатор дважды — ошибка записи.
                if ((modifiers & bit) != 0)
                {
                    return false;
                }

                modifiers |= bit;
            }
            else if (key == VirtualKey.None && TryParseKey(token, out var parsed))
            {
                key = parsed;
            }
            else
            {
                return false;
            }
        }

        if (!ModifierSets.IsValid(modifiers))
        {
            return false;
        }

        chord = new KeyChord(modifiers, key);
        return true;
    }

    /// <summary>Записывает клавиши в каноническом виде: сначала модификаторы, затем основная клавиша.</summary>
    /// <param name="chord">Клавиши.</param>
    /// <returns>Строка вида <c>LCtrl+RCtrl+1</c>.</returns>
    public static string Format(KeyChord chord)
    {
        var parts = new List<string>();
        foreach (var bit in _formatOrder)
        {
            if ((chord.Modifiers & bit) != 0)
            {
                parts.Add(_modifierNames.First(m => m.Bit == bit).Name);
            }
        }

        if (chord.Key != VirtualKey.None)
        {
            parts.Add(KeyName(chord.Key));
        }

        return string.Join('+', parts);
    }

    /// <summary>Разбирает имя модификатора.</summary>
    /// <param name="token">Имя без пробелов.</param>
    /// <param name="bit">Бит модификатора.</param>
    /// <returns><c>true</c>, если имя — модификатор.</returns>
    private static bool TryParseModifier(string token, out ModifierSet bit)
    {
        foreach (var (name, value) in _modifierNames)
        {
            if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
            {
                bit = value;
                return true;
            }
        }

        bit = ModifierSet.None;
        return false;
    }

    /// <summary>Разбирает имя одной немодификаторной клавиши: из таблицы, буква, цифра, F1–F24 или шестнадцатеричный код.</summary>
    /// <param name="token">Имя без пробелов.</param>
    /// <param name="key">Виртуальный код.</param>
    /// <returns><c>true</c>, если имя распознано.</returns>
    private static bool TryParseKey(string token, out VirtualKey key)
    {
        key = VirtualKey.None;
        foreach (var (name, vk) in _namedKeys)
        {
            if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
            {
                key = vk;
                return true;
            }
        }

        if (token.Length == 1 && char.ToUpperInvariant(token[0]) is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
        {
            // Коды букв и цифр совпадают с их ASCII-кодами.
            key = (VirtualKey)char.ToUpperInvariant(token[0]);
            return true;
        }

        if (token.Length is >= 2 and <= 3 && token[0] is 'F' or 'f'
            && int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 24)
        {
            key = (VirtualKey)(F1 + n - 1);
            return true;
        }

        // Модификаторы (в том числе общие VK_SHIFT, VK_CONTROL, VK_MENU) задаются только именами.
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(token.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)
            && code is >= 1 and <= 254
            && code is not (>= 0x10 and <= 0x12)
            && !ModifierSets.IsModifier((VirtualKey)code))
        {
            key = (VirtualKey)code;
            return true;
        }

        return false;
    }

    /// <summary>Имя клавиши для записи; для неизвестных кодов — шестнадцатеричное представление.</summary>
    /// <param name="key">Виртуальный код.</param>
    /// <returns>Имя клавиши.</returns>
    private static string KeyName(VirtualKey key)
    {
        foreach (var (name, vk) in _namedKeys)
        {
            if (vk == key)
            {
                return name;
            }
        }

        var code = (int)key;
        if (code is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
        {
            return ((char)code).ToString();
        }

        if (code is >= F1 and < F1 + 24)
        {
            return "F" + (code - F1 + 1).ToString(CultureInfo.InvariantCulture);
        }

        return "0x" + code.ToString("X2", CultureInfo.InvariantCulture);
    }
}
