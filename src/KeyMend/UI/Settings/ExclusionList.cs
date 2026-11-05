using KeyMend.Infrastructure;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Функция KeyMend, которую можно отключить для программы (SPEC F7).</summary>
internal enum ExclusionFeature
{
    /// <summary>Переключение языка.</summary>
    LayoutSwitch,

    /// <summary>Исправление ввода.</summary>
    Correction,

    /// <summary>Автозамена.</summary>
    Snippets,

    /// <summary>Остальные правила.</summary>
    Rules,
}

/// <summary>Итог добавления программ в исключения.</summary>
/// <param name="List">Список после добавления.</param>
/// <param name="Added">Имена, которые добавлены.</param>
/// <param name="Duplicates">Имена, которые уже были в списке.</param>
internal sealed record ExclusionAddResult(IReadOnlyList<ExclusionEntry> List, IReadOnlyList<string> Added, IReadOnlyList<string> Duplicates);

/// <summary>Правка списка исключений для раздела «Исключения» (SPEC F7): чистая логика без окна, покрыта тестами.</summary>
internal static class ExclusionList
{
    /// <summary>Добавляет программы в список. Новая работает целиком (все функции включены), повторы и недопустимые имена пропускаются.</summary>
    /// <param name="list">Список сейчас.</param>
    /// <param name="names">Имена или пути, как их ввели.</param>
    /// <returns>Новый список, добавленные имена и повторы.</returns>
    public static ExclusionAddResult Add(IReadOnlyList<ExclusionEntry> list, IEnumerable<string> names)
    {
        var result = new List<ExclusionEntry>(list);
        var added = new List<string>();
        var duplicates = new List<string>();
        foreach (var text in names)
        {
            if (ProcessNames.Normalize(text) is not { } name)
            {
                continue;
            }

            if (result.Any(e => string.Equals(e.Process, name, StringComparison.OrdinalIgnoreCase)))
            {
                duplicates.Add(name);
                continue;
            }

            result.Add(new ExclusionEntry { Process = name });
            added.Add(name);
        }

        return new ExclusionAddResult(result, added, duplicates);
    }

    /// <summary>Убирает программу из списка.</summary>
    /// <param name="list">Список сейчас.</param>
    /// <param name="process">Имя exe.</param>
    /// <returns>Новый список.</returns>
    public static IReadOnlyList<ExclusionEntry> Remove(IReadOnlyList<ExclusionEntry> list, string process) =>
        [.. list.Where(e => !string.Equals(e.Process, process, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Включает или выключает функцию для программы.</summary>
    /// <param name="list">Список сейчас.</param>
    /// <param name="process">Имя exe.</param>
    /// <param name="feature">Функция.</param>
    /// <param name="works"><c>true</c> — в программе работает, <c>false</c> — отключена.</param>
    /// <returns>Новый список.</returns>
    public static IReadOnlyList<ExclusionEntry> Set(IReadOnlyList<ExclusionEntry> list, string process, ExclusionFeature feature, bool works) =>
        [.. list.Select(e => string.Equals(e.Process, process, StringComparison.OrdinalIgnoreCase)
            ? feature switch
            {
                ExclusionFeature.LayoutSwitch => e with { LayoutSwitch = works },
                ExclusionFeature.Correction => e with { Correction = works },
                ExclusionFeature.Snippets => e with { Snippets = works },
                _ => e with { Rules = works },
            }
            : e)];

    /// <summary>Читает, работает ли функция в программе.</summary>
    /// <param name="entry">Запись исключения.</param>
    /// <param name="feature">Функция.</param>
    /// <returns><c>true</c>, если работает.</returns>
    public static bool Works(ExclusionEntry entry, ExclusionFeature feature) => feature switch
    {
        ExclusionFeature.LayoutSwitch => entry.LayoutSwitch,
        ExclusionFeature.Correction => entry.Correction,
        ExclusionFeature.Snippets => entry.Snippets,
        _ => entry.Rules,
    };

    /// <summary>Отбирает запущенные программы, которые можно предложить: без уже добавленных и самого KeyMend, по алфавиту.</summary>
    /// <param name="running">Запущенные программы.</param>
    /// <param name="existing">Уже в исключениях.</param>
    /// <param name="ownExe">Имя exe самого KeyMend или <c>null</c>.</param>
    /// <param name="filter">Часть имени или описания; пусто — без отбора.</param>
    /// <returns>Программы для списка выбора.</returns>
    public static IReadOnlyList<RunningApp> Candidates(
        IEnumerable<RunningApp> running, IReadOnlyList<ExclusionEntry> existing, string? ownExe, string? filter)
    {
        var text = filter?.Trim() ?? string.Empty;
        return [.. running
            .Where(a => !string.Equals(a.Exe, ownExe, StringComparison.OrdinalIgnoreCase))
            .Where(a => !existing.Any(e => string.Equals(e.Process, a.Exe, StringComparison.OrdinalIgnoreCase)))
            .Where(a => text.Length == 0
                || a.Exe.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (a.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(a => a.Exe, StringComparer.OrdinalIgnoreCase)];
    }
}
