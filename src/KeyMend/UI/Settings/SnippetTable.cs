using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.UI.Settings;

/// <summary>Условие отбора строк таблицы сокращений.</summary>
/// <param name="Search">Строка поиска по сокращению, его виду в другой раскладке и тексту; пустая — без поиска.</param>
/// <param name="OnlySecrets">Показывать только секреты.</param>
internal sealed record SnippetTableFilter(string Search, bool OnlySecrets);

/// <summary>Строка таблицы сокращений.</summary>
/// <param name="Snippet">Сокращение.</param>
/// <param name="InOtherLayout">Те же клавиши в другой раскладке или <c>null</c>.</param>
internal sealed record SnippetTableRow(Snippet Snippet, string? InOtherLayout);

/// <summary>Отобранные строки и счётчики.</summary>
/// <param name="Rows">Строки в порядке настроек.</param>
/// <param name="Total">Всего сокращений в настройках.</param>
/// <param name="Secrets">Из них секретов.</param>
internal sealed record SnippetTableView(IReadOnlyList<SnippetTableRow> Rows, int Total, int Secrets);

/// <summary>Отбор строк таблицы «Автозамена»; без интерфейса, чтобы проверяться тестами.</summary>
internal static class SnippetTable
{
    /// <summary>Отбирает сокращения.</summary>
    /// <param name="snippets">Все сокращения.</param>
    /// <param name="filter">Условие отбора.</param>
    /// <param name="tables">Таблицы установленных раскладок для вида в другой раскладке.</param>
    /// <returns>Строки и счётчики.</returns>
    public static SnippetTableView Build(
        IReadOnlyList<Snippet> snippets, SnippetTableFilter filter, IReadOnlyList<LayoutTable> tables)
    {
        var search = filter.Search.Trim();
        var rows = new List<SnippetTableRow>();
        foreach (var snippet in snippets)
        {
            if (filter.OnlySecrets && !snippet.Secret)
            {
                continue;
            }

            var other = SnippetKeys.InOtherLayout(snippet.Keys, tables);
            if (search.Length == 0 || Matches(snippet, other, search))
            {
                rows.Add(new SnippetTableRow(snippet, other));
            }
        }

        return new SnippetTableView(rows, snippets.Count, snippets.Count(s => s.Secret));
    }

    /// <summary>Показывает сокращение так, чтобы пробел на конце был виден: «␣» вместо пробела.</summary>
    /// <param name="keys">Сокращение как оно записано.</param>
    /// <returns>Строка для показа.</returns>
    public static string DisplayKeys(string keys) => keys.Replace(' ', '␣');

    /// <summary>Подходит ли сокращение под строку поиска; текст секрета не ищется, его в настройках нет.</summary>
    private static bool Matches(Snippet snippet, string? other, string search) =>
        Contains(snippet.Keys, search)
        || (other is not null && Contains(other, search))
        || (!snippet.Secret && snippet.Text is { } text && Contains(text, search));

    /// <summary>Содержит ли текст подстроку без учёта регистра.</summary>
    private static bool Contains(string text, string search) => text.Contains(search, StringComparison.CurrentCultureIgnoreCase);
}
