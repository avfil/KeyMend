using KeyMend.Input;

namespace KeyMend.Text;

/// <summary>Как два шаблона сокращений соотносятся при наборе.</summary>
internal enum SnippetRelation
{
    /// <summary>Не пересекаются.</summary>
    None,

    /// <summary>Шаблоны совпадают целиком.</summary>
    Same,

    /// <summary>Первый шаблон — начало второго: первый сработает раньше, второй не сработает никогда.</summary>
    PrefixOf,

    /// <summary>Второй шаблон — начало первого.</summary>
    StartsWith,
}

/// <summary>
/// Разбор строки сокращения в шаблон физических клавиш и сравнение шаблонов (SPEC F5.1). Общая часть компилятора
/// сокращений и проверок окна настроек, чтобы они всегда считали одинаково. Без WinAPI.
/// </summary>
internal static class SnippetKeys
{
    /// <summary>Разбирает строку по первой таблице, в которой есть все её символы.</summary>
    /// <param name="text">Строка сокращения.</param>
    /// <param name="tables">Таблицы раскладок.</param>
    /// <returns>Клавиши или <c>null</c>, если ни одна таблица не подходит.</returns>
    public static KeyStroke[]? ParseKeys(string text, IReadOnlyList<LayoutTable> tables)
    {
        foreach (var table in tables)
        {
            var result = new KeyStroke[text.Length];
            var ok = true;
            for (var i = 0; i < text.Length && ok; i++)
            {
                ok = table.TryFind(text[i], out result[i]);
            }

            if (ok)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>Находит клавиши, у которых Shift не сравнивается: дают букву в какой-либо раскладке или не меняются от Shift (пробел).</summary>
    /// <param name="tables">Таблицы раскладок.</param>
    /// <returns>Признак по коду клавиши.</returns>
    public static bool[] BuildIgnoreShift(IReadOnlyList<LayoutTable> tables)
    {
        var result = new bool[256];
        foreach (var vk in CharacterKeys.All)
        {
            var sameEverywhere = true;
            var letter = false;
            foreach (var table in tables)
            {
                var normal = table.Get(vk, false, false);
                var shifted = table.Get(vk, true, false);
                letter |= IsLetter(normal) || IsLetter(shifted);
                sameEverywhere &= normal == shifted;
            }

            result[(int)vk] = letter || sameEverywhere;
        }

        return result;
    }

    /// <summary>Превращает клавиши в шаблон для сравнения с буфером набора.</summary>
    /// <param name="keys">Клавиши сокращения.</param>
    /// <param name="ignoreShift">Клавиши, у которых Shift не сравнивается (<see cref="BuildIgnoreShift"/>).</param>
    /// <returns>Шаблон.</returns>
    public static KeyPattern[] ToPattern(KeyStroke[] keys, bool[] ignoreShift)
    {
        var pattern = new KeyPattern[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var check = !ignoreShift[(int)keys[i].Vk];
            pattern[i] = new KeyPattern(keys[i].Vk, check, check && keys[i].Shift);
        }

        return pattern;
    }

    /// <summary>Разбирает строку сокращения сразу в шаблон.</summary>
    /// <param name="text">Строка сокращения.</param>
    /// <param name="tables">Таблицы раскладок.</param>
    /// <param name="ignoreShift">Клавиши, у которых Shift не сравнивается.</param>
    /// <returns>Шаблон или <c>null</c>, если строка пуста или не набирается ни в одной раскладке.</returns>
    public static KeyPattern[]? Parse(string text, IReadOnlyList<LayoutTable> tables, bool[] ignoreShift) =>
        ParseKeys(text, tables) is { Length: > 0 } keys ? ToPattern(keys, ignoreShift) : null;

    /// <summary>Сравнивает два шаблона.</summary>
    /// <param name="a">Первый шаблон.</param>
    /// <param name="b">Второй шаблон.</param>
    /// <returns>Соотношение шаблонов.</returns>
    public static SnippetRelation Compare(KeyPattern[] a, KeyPattern[] b)
    {
        if (SamePattern(a, b))
        {
            return SnippetRelation.Same;
        }

        if (IsPrefix(a, b))
        {
            return SnippetRelation.PrefixOf;
        }

        return IsPrefix(b, a) ? SnippetRelation.StartsWith : SnippetRelation.None;
    }

    /// <summary>
    /// Показывает те же клавиши в другой раскладке: русское сокращение — в английской, английское — в русской.
    /// </summary>
    /// <param name="text">Строка сокращения.</param>
    /// <param name="tables">Таблицы установленных раскладок.</param>
    /// <returns>Строка в другой раскладке или <c>null</c>, если строка не разбирается или другого вида нет.</returns>
    public static string? InOtherLayout(string text, IReadOnlyList<LayoutTable> tables)
    {
        if (ParseKeys(text, tables) is not { Length: > 0 } keys)
        {
            return null;
        }

        var english = Render(keys, FallbackLayouts.Us);
        if (english is not null && english != text)
        {
            return english;
        }

        var russian = Render(keys, FallbackLayouts.Ru);
        return russian is not null && russian != text ? russian : null;
    }

    /// <summary>Печатает клавиши символами таблицы.</summary>
    /// <param name="keys">Клавиши.</param>
    /// <param name="table">Таблица раскладки.</param>
    /// <returns>Строка или <c>null</c>, если какая-то клавиша символа не даёт.</returns>
    private static string? Render(KeyStroke[] keys, LayoutTable table)
    {
        var chars = new char[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var translation = table.Get(keys[i].Vk, keys[i].Shift, false);
            if (translation.Kind != TranslationKind.Char)
            {
                return null;
            }

            chars[i] = translation.Char;
        }

        return new string(chars);
    }

    /// <summary>Даёт ли перевод букву.</summary>
    /// <param name="translation">Перевод клавиши.</param>
    /// <returns><c>true</c> для буквы.</returns>
    private static bool IsLetter(Translation translation) =>
        translation.Kind == TranslationKind.Char && char.IsLetter(translation.Char);

    /// <summary>Совпадают ли шаблоны целиком.</summary>
    /// <param name="a">Первый шаблон.</param>
    /// <param name="b">Второй шаблон.</param>
    /// <returns><c>true</c>, если равны.</returns>
    public static bool SamePattern(KeyPattern[] a, KeyPattern[] b) => a.AsSpan().SequenceEqual(b);

    /// <summary>Является ли <paramref name="prefix"/> началом более длинного шаблона <paramref name="full"/>.</summary>
    /// <param name="prefix">Короткий шаблон.</param>
    /// <param name="full">Длинный шаблон.</param>
    /// <returns><c>true</c>, если начало совпадает и <paramref name="full"/> длиннее.</returns>
    public static bool IsPrefix(KeyPattern[] prefix, KeyPattern[] full) =>
        prefix.Length < full.Length && full.AsSpan(0, prefix.Length).SequenceEqual(prefix);
}
