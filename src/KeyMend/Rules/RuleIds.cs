namespace KeyMend.Rules;

/// <summary>Выдаёт устойчивые идентификаторы новым правилам: по ним правило находят при правке и сверяют при проверке конфликтов.</summary>
internal static class RuleIds
{
    /// <summary>Подбирает идентификатор вида <c>r-1a2b3c4d</c>, которого нет среди существующих.</summary>
    /// <param name="existing">Уже занятые идентификаторы.</param>
    /// <param name="newGuid">Источник случайных значений; в тестах подменяется.</param>
    /// <param name="prefix">Начало идентификатора: <c>r-</c> у правил, <c>a-</c> у сокращений автозамены.</param>
    /// <returns>Свободный идентификатор.</returns>
    public static string Next(IEnumerable<string> existing, Func<Guid> newGuid, string prefix = "r-")
    {
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        while (true)
        {
            var id = prefix + newGuid().ToString("N")[..8];
            if (taken.Add(id))
            {
                return id;
            }
        }
    }
}
