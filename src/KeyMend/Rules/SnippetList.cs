namespace KeyMend.Rules;

/// <summary>Правки списка сокращений из окна настроек: добавить, заменить, удалить. Списки не меняются, возвращаются новые.</summary>
internal static class SnippetList
{
    /// <summary>Заменяет сокращение <paramref name="original"/> на <paramref name="updated"/> или добавляет в конец, если оригинала нет.</summary>
    /// <param name="snippets">Сокращения сейчас.</param>
    /// <param name="original">Сокращение до правки или <c>null</c> для нового.</param>
    /// <param name="updated">Сокращение после правки; без идентификатора получает новый.</param>
    /// <param name="newGuid">Источник случайных значений для идентификатора.</param>
    /// <returns>Новый список сокращений.</returns>
    public static IReadOnlyList<Snippet> Upsert(
        IReadOnlyList<Snippet> snippets, Snippet? original, Snippet updated, Func<Guid> newGuid)
    {
        var withId = updated.Id.Length > 0
            ? updated
            : updated with { Id = RuleIds.Next(snippets.Select(s => s.Id), newGuid, "a-") };
        var list = snippets.ToList();
        var index = original is null ? -1 : IndexOf(snippets, original);
        if (index < 0)
        {
            list.Add(withId);
        }
        else
        {
            list[index] = withId;
        }

        return list;
    }

    /// <summary>Убирает сокращение.</summary>
    /// <param name="snippets">Сокращения сейчас.</param>
    /// <param name="snippet">Сокращение для удаления.</param>
    /// <returns>Список без сокращения; если его не нашли, прежний состав.</returns>
    public static IReadOnlyList<Snippet> Remove(IReadOnlyList<Snippet> snippets, Snippet snippet)
    {
        var index = IndexOf(snippets, snippet);
        return index < 0 ? snippets : [.. snippets.Take(index), .. snippets.Skip(index + 1)];
    }

    /// <summary>Ищет сокращение по идентификатору, а без него — по ссылке.</summary>
    private static int IndexOf(IReadOnlyList<Snippet> snippets, Snippet snippet)
    {
        for (var i = 0; i < snippets.Count; i++)
        {
            if (ReferenceEquals(snippets[i], snippet)
                || (snippet.Id.Length > 0 && string.Equals(snippets[i].Id, snippet.Id, StringComparison.Ordinal)))
            {
                return i;
            }
        }

        return -1;
    }
}
