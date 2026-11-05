using KeyMend.Actions;
using KeyMend.Rules;

namespace KeyMend.Storage.Transfer;

/// <summary>Какие файлы из архива записать в папку скриптов.</summary>
/// <param name="ToWrite">Файлы для записи.</param>
/// <param name="Skipped">Пути файлов, которые не записаны, потому что у владельца лежит другой файл с тем же именем.</param>
internal sealed record ScriptPlan(IReadOnlyList<ScriptFile> ToWrite, IReadOnlyList<string> Skipped);

/// <summary>Решения о файлах скриптов и проверка программ правил после загрузки (SPEC F8).</summary>
internal static class ScriptMerge
{
    /// <summary>Решает, какие файлы записывать.</summary>
    /// <param name="incoming">Файлы из архива.</param>
    /// <param name="readExisting">Читает файл из папки скриптов; <c>null</c>, если его нет.</param>
    /// <param name="mode">«Заменить всё» перезаписывает файлы; «Добавить» оставляет отличающиеся файлы владельца нетронутыми.</param>
    /// <returns>Что записать и что пропущено.</returns>
    public static ScriptPlan Plan(IReadOnlyList<ScriptFile> incoming, Func<string, byte[]?> readExisting, ImportMode mode)
    {
        if (mode == ImportMode.Replace)
        {
            return new ScriptPlan(incoming, []);
        }

        var write = new List<ScriptFile>();
        var skipped = new List<string>();
        foreach (var file in incoming)
        {
            var existing = readExisting(file.RelativePath);
            if (existing is null)
            {
                write.Add(file);
            }
            else if (!existing.AsSpan().SequenceEqual(file.Content))
            {
                skipped.Add(file.RelativePath);
            }
        }

        return new ScriptPlan(write, skipped);
    }

    /// <summary>Находит правила, у которых шаг «Запустить программу» указывает на файл, которого нет на этом компьютере.</summary>
    /// <param name="rules">Правила.</param>
    /// <param name="env">Окружение для разбора путей шагов.</param>
    /// <returns>Названия правил.</returns>
    public static IReadOnlyList<string> FindMissingPrograms(IReadOnlyList<Rule> rules, LaunchEnvironment env) =>
    [
        .. rules
            .Where(rule => rule.Steps.OfType<LaunchStep>().Any(step => !LaunchCommandBuilder.TryBuild(step, env, out _, out _)))
            .Select(rule => rule.Name),
    ];
}
