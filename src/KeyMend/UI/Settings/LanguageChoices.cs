using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>Один язык в списке выбора.</summary>
/// <param name="Tag">Тег вида <c>ru-RU</c>.</param>
/// <param name="Name">Название для показа.</param>
/// <param name="Installed">Раскладка этого языка установлена в Windows.</param>
internal sealed record LanguageChoice(string Tag, string Name, bool Installed)
{
    /// <summary>Название для программ чтения с экрана: имя элемента списка языков.</summary>
    /// <returns>Название языка.</returns>
    public override string ToString() => Name;
}

/// <summary>Состав списка выбора языка: установленные языки и те выбранные в правиле, которых в системе уже нет.</summary>
internal static class LanguageChoices
{
    /// <summary>Собирает список.</summary>
    /// <param name="installed">Теги установленных языков.</param>
    /// <param name="selected">Теги, выбранные в правиле сейчас; неустановленные остаются в списке с пометкой.</param>
    /// <returns>Языки: сначала установленные, потом выбранные неустановленные.</returns>
    public static IReadOnlyList<LanguageChoice> Build(IReadOnlyList<string> installed, IEnumerable<string> selected)
    {
        var tags = installed.ToList();
        foreach (var tag in selected)
        {
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tag);
            }
        }

        var shortNames = tags.Select(LanguageNames.Short).ToList();
        return [.. tags.Select((tag, i) =>
        {
            var isInstalled = installed.Contains(tag, StringComparer.OrdinalIgnoreCase);
            var name = shortNames.Count(n => n == shortNames[i]) > 1 ? LanguageNames.Full(tag) : shortNames[i];
            return new LanguageChoice(tag, isInstalled ? name : RuleSummary.Fill(Strings.LanguageNotInstalled, name), isInstalled);
        })];
    }
}
