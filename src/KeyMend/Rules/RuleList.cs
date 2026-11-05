namespace KeyMend.Rules;

/// <summary>Правки списка правил из окна настроек: добавить, заменить, удалить, вставить копию. Списки не меняются, возвращаются новые.</summary>
internal static class RuleList
{
    /// <summary>Заменяет правило <paramref name="original"/> на <paramref name="updated"/> или добавляет в конец, если оригинала нет.</summary>
    /// <param name="rules">Правила сейчас.</param>
    /// <param name="original">Правило до правки или <c>null</c> для нового.</param>
    /// <param name="updated">Правило после правки; без идентификатора получает новый.</param>
    /// <param name="newGuid">Источник случайных значений для идентификатора.</param>
    /// <returns>Новый список правил.</returns>
    public static IReadOnlyList<Rule> Upsert(IReadOnlyList<Rule> rules, Rule? original, Rule updated, Func<Guid> newGuid)
    {
        var withId = WithId(rules, updated, newGuid);
        var list = rules.ToList();
        var index = original is null ? -1 : IndexOf(rules, original);
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

    /// <summary>Убирает правило.</summary>
    /// <param name="rules">Правила сейчас.</param>
    /// <param name="rule">Правило для удаления.</param>
    /// <returns>Список без правила; если его не нашли, прежний состав.</returns>
    public static IReadOnlyList<Rule> Remove(IReadOnlyList<Rule> rules, Rule rule)
    {
        var index = IndexOf(rules, rule);
        return index < 0 ? rules : [.. rules.Take(index), .. rules.Skip(index + 1)];
    }

    /// <summary>Вставляет новое правило сразу после <paramref name="after"/>.</summary>
    /// <param name="rules">Правила сейчас.</param>
    /// <param name="after">Правило, под которым встанет копия; не найдено — копия уходит в конец.</param>
    /// <param name="added">Вставляемое правило; без идентификатора получает новый.</param>
    /// <param name="newGuid">Источник случайных значений для идентификатора.</param>
    /// <returns>Новый список правил.</returns>
    public static IReadOnlyList<Rule> InsertAfter(IReadOnlyList<Rule> rules, Rule after, Rule added, Func<Guid> newGuid)
    {
        var withId = WithId(rules, added, newGuid);
        var index = IndexOf(rules, after);
        var list = rules.ToList();
        list.Insert(index < 0 ? list.Count : index + 1, withId);
        return list;
    }

    /// <summary>Ищет правило по идентификатору, а без него — по ссылке.</summary>
    private static int IndexOf(IReadOnlyList<Rule> rules, Rule rule)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            if (ReferenceEquals(rules[i], rule)
                || (rule.Id.Length > 0 && string.Equals(rules[i].Id, rule.Id, StringComparison.Ordinal)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Возвращает правило с идентификатором, не занятым в списке.</summary>
    private static Rule WithId(IReadOnlyList<Rule> rules, Rule rule, Func<Guid> newGuid) =>
        rule.Id.Length > 0 ? rule : rule with { Id = RuleIds.Next(rules.Select(r => r.Id), newGuid) };
}
