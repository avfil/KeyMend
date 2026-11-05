using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Text;

/// <summary>Одна клавиша шаблона сокращения: сама клавиша и, если важно, состояние Shift.</summary>
/// <param name="Vk">Виртуальный код.</param>
/// <param name="CheckShift">Сравнивать ли Shift (для букв и пробела не сравнивается).</param>
/// <param name="Shift">Нужное состояние Shift при <paramref name="CheckShift"/>.</param>
internal readonly record struct KeyPattern(VirtualKey Vk, bool CheckShift, bool Shift);

/// <summary>Скомпилированное сокращение: шаблон клавиш и готовое правило с шагами.</summary>
/// <param name="Pattern">Шаблон: последняя клавиша — конечный символ сокращения.</param>
/// <param name="Rule">Правило, которое уходит в очередь цепочек при совпадении.</param>
internal sealed record CompiledSnippet(KeyPattern[] Pattern, Rule Rule);

/// <summary>Установленная раскладка: описатель и таблица клавиш.</summary>
/// <param name="Hkl">Описатель раскладки (HKL).</param>
/// <param name="Table">Таблица «клавиша → символ».</param>
internal sealed record InstalledLayout(nint Hkl, LayoutTable Table);

/// <summary>Разделители слов для одной раскладки: по индексу <c>vk * 2 + shift</c>.</summary>
/// <param name="Hkl">Описатель раскладки.</param>
/// <param name="Separators">Таблица разделителей.</param>
internal sealed record LayoutSeparators(nint Hkl, bool[] Separators);

/// <summary>
/// Сопоставление набранных физических клавиш с сокращениями (SPEC F5.1). Неизменяемый набор, собранный заранее
/// <see cref="SnippetCompiler"/>; <see cref="TryMatch"/> не выделяет память и вызывается из колбэка хука.
/// Начало слова определяется по раскладке окна, в котором печатают (она читается только при полном совпадении
/// шаблона, то есть редко); если раскладка неизвестна, клавиша — разделитель, когда она не буква ни в одной раскладке.
/// Без WinAPI.
/// </summary>
internal sealed class SnippetMatcher
{
    /// <summary>Пустой набор: ничего не срабатывает.</summary>
    public static readonly SnippetMatcher Empty = new([], new bool[SeparatorTableSize], []);

    /// <summary>Размер таблицы разделителей: 256 клавиш × 2 состояния Shift.</summary>
    internal const int SeparatorTableSize = 512;

    /// <summary>Значение «раскладка ещё не прочитана» (HKL не бывает равен -1).</summary>
    private const nint _notRead = -1;

    /// <summary>Сокращения в порядке настроек.</summary>
    private readonly CompiledSnippet[] _snippets;

    /// <summary>Разделители, если раскладка окна неизвестна: клавиша не буква ни в одной раскладке; индекс <c>vk * 2 + shift</c>.</summary>
    private readonly bool[] _separators;

    /// <summary>Разделители по каждой установленной раскладке.</summary>
    private readonly LayoutSeparators[] _byLayout;

    /// <summary>Создаёт набор.</summary>
    /// <param name="snippets">Скомпилированные сокращения.</param>
    /// <param name="separators">Таблица разделителей для неизвестной раскладки.</param>
    /// <param name="byLayout">Таблицы разделителей установленных раскладок.</param>
    internal SnippetMatcher(CompiledSnippet[] snippets, bool[] separators, LayoutSeparators[] byLayout)
    {
        _snippets = snippets;
        _separators = separators;
        _byLayout = byLayout;
    }

    /// <summary>Сколько сокращений в наборе.</summary>
    public int Count => _snippets.Length;

    /// <summary>
    /// Ищет сокращение, которым заканчивается буфер и которое начинается с начала слова. Если подходят несколько
    /// (совпали на одной клавише), берётся самое длинное.
    /// </summary>
    /// <param name="buffer">Буфер набранных клавиш, последняя клавиша уже в нём.</param>
    /// <param name="currentLayout">Читает HKL раскладки окна с фокусом (0 — неизвестна); вызывается не больше раза и только при совпадении шаблона. <c>null</c> — раскладка неизвестна.</param>
    /// <param name="rule">Правило найденного сокращения.</param>
    /// <returns><c>true</c>, если сокращение набрано.</returns>
    public bool TryMatch(WordBuffer buffer, Func<nint>? currentLayout, out Rule rule)
    {
        CompiledSnippet? best = null;
        var hkl = _notRead;
        foreach (var snippet in _snippets)
        {
            if ((best is null || snippet.Pattern.Length > best.Pattern.Length)
                && Matches(snippet.Pattern, buffer, currentLayout, ref hkl))
            {
                best = snippet;
            }
        }

        rule = best?.Rule!;
        return best is not null;
    }

    /// <summary>Заканчивается ли буфер шаблоном и стоит ли перед ним граница слова.</summary>
    /// <param name="pattern">Шаблон.</param>
    /// <param name="buffer">Буфер.</param>
    /// <param name="currentLayout">Чтение раскладки окна.</param>
    /// <param name="hkl">Раскладка окна: читается при первой надобности и запоминается.</param>
    /// <returns><c>true</c>, если подходит.</returns>
    private bool Matches(KeyPattern[] pattern, WordBuffer buffer, Func<nint>? currentLayout, ref nint hkl)
    {
        var length = pattern.Length;
        if (buffer.Count < length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            var expected = pattern[length - 1 - i];
            var actual = buffer.FromEnd(i);
            if (actual.Vk != expected.Vk || (expected.CheckShift && actual.Shift != expected.Shift))
            {
                return false;
            }
        }

        if (buffer.Count == length)
        {
            return buffer.StartsAtBoundary;
        }

        if (hkl == _notRead)
        {
            hkl = currentLayout?.Invoke() ?? 0;
        }

        var before = buffer.FromEnd(length);
        return SeparatorsFor(hkl)[((int)before.Vk * 2) + (before.Shift ? 1 : 0)];
    }

    /// <summary>Выбирает таблицу разделителей для раскладки окна.</summary>
    /// <param name="hkl">Описатель раскладки или 0, если неизвестна.</param>
    /// <returns>Таблица раскладки или общая, если раскладка неизвестна среди установленных.</returns>
    private bool[] SeparatorsFor(nint hkl)
    {
        if (hkl != 0)
        {
            foreach (var layout in _byLayout)
            {
                if (layout.Hkl == hkl)
                {
                    return layout.Separators;
                }
            }
        }

        return _separators;
    }
}
