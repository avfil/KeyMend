using KeyMend.Input;

namespace KeyMend.Text;

/// <summary>
/// Запасные таблицы английской (US) и русской (ЙЦУКЕН) раскладок на случай, когда система не смогла
/// построить таблицу через <c>ToUnicodeEx</c>. Сверяются с системой в тестах <c>KeyMapTests</c>.
/// </summary>
internal static class FallbackLayouts
{
    /// <summary>Код языка английского (США).</summary>
    public const int EnglishUs = 0x0409;

    /// <summary>Код языка русского.</summary>
    public const int Russian = 0x0419;

    /// <summary>Клавиши OEM в порядке: <c>;</c> <c>=</c> <c>,</c> <c>-</c> <c>.</c> <c>/</c> <c>`</c> <c>[</c> <c>\</c> <c>]</c> <c>'</c>.</summary>
    private static readonly VirtualKey[] _oemKeys =
    [
        (VirtualKey)0xBA, (VirtualKey)0xBB, (VirtualKey)0xBC, (VirtualKey)0xBD, (VirtualKey)0xBE, (VirtualKey)0xBF,
        (VirtualKey)0xC0, (VirtualKey)0xDB, (VirtualKey)0xDC, (VirtualKey)0xDD, (VirtualKey)0xDE,
    ];

    /// <summary>Таблица английской раскладки (США).</summary>
    public static LayoutTable Us { get; } = BuildUs();

    /// <summary>Таблица русской раскладки.</summary>
    public static LayoutTable Ru { get; } = BuildRu();

    /// <summary>Возвращает запасную таблицу для языка или <c>null</c>, если её нет.</summary>
    /// <param name="languageId">Код языка (младшее слово HKL).</param>
    /// <returns>Таблица или <c>null</c>.</returns>
    public static LayoutTable? ForLanguage(int languageId) => languageId switch
    {
        EnglishUs => Us,
        Russian => Ru,
        _ => null,
    };

    /// <summary>Строит таблицу US.</summary>
    private static LayoutTable BuildUs()
    {
        var table = new LayoutTable();
        AddCommon(table);
        AddDigits(table, ")!@#$%^&*(");
        AddLetters(table, "abcdefghijklmnopqrstuvwxyz");
        AddOem(table, ";=,-./`[\\]'", ":+<_>?~{|}\"");
        return table;
    }

    /// <summary>Строит таблицу русской раскладки.</summary>
    private static LayoutTable BuildRu()
    {
        var table = new LayoutTable();
        AddCommon(table);
        AddDigits(table, ")!\"№;%:?*(");
        AddLetters(table, "фисвуапршолдьтщзйкыегмцчня");

        // ; = , - . / ` [ \ ] '  →  ж = б - ю . ё х \ ъ э
        AddOem(table, "ж=б-ю.ё" + "х\\ъэ", "Ж+Б_Ю,Ё" + "Х/ЪЭ");
        return table;
    }

    /// <summary>Пробел одинаков во всех состояниях.</summary>
    private static void AddCommon(LayoutTable table) => table.SetPair(VirtualKey.Space, ' ', ' ');

    /// <summary>Цифровой ряд: без Shift цифры 0–9, с Shift — переданные символы в порядке цифр 0–9.</summary>
    private static void AddDigits(LayoutTable table, string shifted)
    {
        for (var digit = 0; digit <= 9; digit++)
        {
            table.SetPair((VirtualKey)(0x30 + digit), (char)('0' + digit), shifted[digit]);
        }
    }

    /// <summary>Буквы по клавишам A–Z; заглавная получается преобразованием строчной.</summary>
    private static void AddLetters(LayoutTable table, string lower)
    {
        for (var i = 0; i < 26; i++)
        {
            table.SetPair((VirtualKey)(0x41 + i), lower[i], char.ToUpperInvariant(lower[i]));
        }
    }

    /// <summary>Клавиши OEM в порядке <see cref="_oemKeys"/>.</summary>
    private static void AddOem(LayoutTable table, string normal, string shifted)
    {
        for (var i = 0; i < _oemKeys.Length; i++)
        {
            table.SetPair(_oemKeys[i], normal[i], shifted[i]);
        }
    }
}
