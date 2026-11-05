using System.Globalization;
using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Text;

/// <summary>Итог компиляции сокращений: набор для хука и предупреждения о пропущенных или недостижимых записях.</summary>
/// <param name="Matcher">Готовый набор.</param>
/// <param name="Warnings">Замечания с номерами записей; ни сокращений, ни текстов в них нет.</param>
internal sealed record SnippetCompileResult(SnippetMatcher Matcher, IReadOnlyList<string> Warnings);

/// <summary>
/// Превращает сокращения из настроек в <see cref="SnippetMatcher"/>: разбирает строку <c>keys</c> по таблицам
/// установленных раскладок и собирает правило для очереди цепочек (SPEC F5.1). Без WinAPI.
/// </summary>
internal static class SnippetCompiler
{
    /// <summary>Компилирует сокращения, когда раскладки окна неизвестны: разделитель — клавиша, не буква ни в одной раскладке.</summary>
    /// <param name="snippets">Сокращения из настроек.</param>
    /// <param name="tables">Таблицы установленных раскладок в порядке списка Windows.</param>
    /// <returns>Набор и предупреждения.</returns>
    public static SnippetCompileResult Compile(IReadOnlyList<Snippet> snippets, IReadOnlyList<LayoutTable> tables) =>
        Compile(snippets, tables, []);

    /// <summary>Компилирует сокращения; начало слова определяется по раскладке окна, в котором печатают.</summary>
    /// <param name="snippets">Сокращения из настроек.</param>
    /// <param name="layouts">Установленные раскладки в порядке списка Windows.</param>
    /// <returns>Набор и предупреждения.</returns>
    public static SnippetCompileResult Compile(IReadOnlyList<Snippet> snippets, IReadOnlyList<InstalledLayout> layouts) =>
        Compile(snippets, [.. layouts.Select(l => l.Table)], layouts);

    /// <summary>Общая часть компиляции.</summary>
    /// <param name="snippets">Сокращения из настроек.</param>
    /// <param name="tables">Таблицы раскладок.</param>
    /// <param name="layouts">Раскладки с описателями для таблиц разделителей по раскладке окна.</param>
    /// <returns>Набор и предупреждения.</returns>
    private static SnippetCompileResult Compile(
        IReadOnlyList<Snippet> snippets, IReadOnlyList<LayoutTable> tables, IReadOnlyList<InstalledLayout> layouts)
    {
        var warnings = new List<string>();
        var ignoreShift = SnippetKeys.BuildIgnoreShift(tables);
        var compiled = new List<CompiledSnippet>();

        for (var i = 0; i < snippets.Count; i++)
        {
            var snippet = snippets[i];
            var number = i + 1;
            if (!snippet.Enabled)
            {
                continue;
            }

            if (Build(snippet, number, tables, ignoreShift, warnings) is { } item)
            {
                CheckOverlap(compiled, item, number, warnings);
                if (compiled.Any(c => SnippetKeys.SamePattern(c.Pattern, item.Pattern)))
                {
                    continue;
                }

                compiled.Add(item);
            }
        }

        var byLayout = layouts.Select(l => new LayoutSeparators(l.Hkl, BuildSeparators([l.Table]))).ToArray();
        return new SnippetCompileResult(new SnippetMatcher([.. compiled], BuildSeparators(tables), byLayout), warnings);
    }

    /// <summary>Собирает шаблон и правило одного сокращения.</summary>
    /// <param name="snippet">Сокращение.</param>
    /// <param name="number">Номер в настройках, с единицы.</param>
    /// <param name="tables">Таблицы раскладок.</param>
    /// <param name="ignoreShift">Клавиши, у которых Shift не сравнивается.</param>
    /// <param name="warnings">Приёмник предупреждений.</param>
    /// <returns>Скомпилированное сокращение или <c>null</c>, если запись непригодна.</returns>
    private static CompiledSnippet? Build(
        Snippet snippet, int number, IReadOnlyList<LayoutTable> tables, bool[] ignoreShift, List<string> warnings)
    {
        var keys = SnippetKeys.ParseKeys(snippet.Keys, tables);
        if (keys is null || keys.Length == 0)
        {
            warnings.Add(Format($"snippet #{number} skipped: keys are empty or not typable in any installed layout"));
            return null;
        }

        if (snippet.Secret && snippet.SecretRef is null)
        {
            warnings.Add(Format($"snippet #{number} skipped: secret has no stored text"));
            return null;
        }

        if (!snippet.Secret && snippet.Text is null)
        {
            warnings.Add(Format($"snippet #{number} skipped: no text"));
            return null;
        }

        var pattern = SnippetKeys.ToPattern(keys, ignoreShift);

        // Последняя клавиша подавляется и не доходит до окна, поэтому стираются только предыдущие.
        var insert = new InsertTextStep
        {
            Text = snippet.Secret ? null : snippet.Text,
            SecretRef = snippet.Secret ? snippet.SecretRef : null,
            Insert = snippet.Secret ? InsertMode.Clipboard : snippet.Insert,
            Newline = snippet.Newline,
            Erase = pattern.Length - 1,
        };
        var rule = new Rule
        {
            Id = snippet.Id,
            Name = Format($"snippet #{number}"),
            Trigger = new PressTrigger { Key = VirtualKey.None },
            Steps = [insert, .. snippet.After],
        };
        return new CompiledSnippet(pattern, rule);
    }

    /// <summary>Строит таблицу разделителей: клавиша без Shift или с ним, которая ни в одной раскладке не даёт букву или цифру.</summary>
    /// <param name="tables">Таблицы раскладок.</param>
    /// <returns>Признак по индексу <c>vk * 2 + shift</c>.</returns>
    private static bool[] BuildSeparators(IReadOnlyList<LayoutTable> tables)
    {
        var result = new bool[SnippetMatcher.SeparatorTableSize];
        foreach (var vk in CharacterKeys.All)
        {
            for (var shift = 0; shift < 2; shift++)
            {
                var separator = true;
                foreach (var table in tables)
                {
                    var t = table.Get(vk, shift != 0, false);
                    separator &= !(t.Kind == TranslationKind.Char && char.IsLetterOrDigit(t.Char));
                }

                result[((int)vk * 2) + shift] = separator;
            }
        }

        return result;
    }

    /// <summary>Добавляет предупреждение, если новое сокращение дублирует или перекрывает уже принятое.</summary>
    /// <param name="existing">Принятые сокращения.</param>
    /// <param name="item">Новое сокращение.</param>
    /// <param name="number">Номер нового в настройках.</param>
    /// <param name="warnings">Приёмник предупреждений.</param>
    private static void CheckOverlap(List<CompiledSnippet> existing, CompiledSnippet item, int number, List<string> warnings)
    {
        foreach (var other in existing)
        {
            if (SnippetKeys.SamePattern(other.Pattern, item.Pattern))
            {
                warnings.Add(Format($"snippet #{number} skipped: same keys as {other.Rule.Name}"));
            }
            else if (SnippetKeys.IsPrefix(other.Pattern, item.Pattern))
            {
                warnings.Add(Format($"snippet #{number} never fires: {other.Rule.Name} is a prefix of it and fires first"));
            }
            else if (SnippetKeys.IsPrefix(item.Pattern, other.Pattern))
            {
                warnings.Add(Format($"{other.Rule.Name} never fires: snippet #{number} is a prefix of it and fires first"));
            }
        }
    }

    /// <summary>Форматирует интерполированную строку в инвариантной культуре.</summary>
    /// <param name="text">Строка.</param>
    /// <returns>Строка.</returns>
    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
