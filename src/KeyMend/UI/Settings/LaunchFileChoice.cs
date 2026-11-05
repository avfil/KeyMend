using System.IO;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Что делать с файлом, выбранным кнопкой «Обзор…» в шаге «Запустить программу».</summary>
internal static class LaunchFileChoice
{
    /// <summary>Приводит выбранный файл к виду для настроек: внутри папки скриптов — относительным путём (SPEC F4.1).</summary>
    /// <param name="fullPath">Полный путь выбранного файла.</param>
    /// <param name="scriptsDir">Папка скриптов.</param>
    /// <returns>Относительный путь или исходный полный.</returns>
    public static string ToStored(string fullPath, string scriptsDir)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scriptsDir)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fullPath);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full[root.Length..] : fullPath;
    }

    /// <summary>Папка, в которой открывается диалог «Обзор…»: первая существующая из списка кандидатов, иначе папка скриптов.</summary>
    /// <param name="scriptsDir">Папка скриптов: запасной вариант.</param>
    /// <param name="exists">Проверка, что папка есть на диске.</param>
    /// <param name="candidates">Тексты из полей: рабочая папка, затем файл. Для файла берётся его папка; пустое и относительное пропускается.</param>
    /// <returns>Папка для диалога.</returns>
    public static string StartDirectory(string scriptsDir, Func<string, bool> exists, params (string Text, bool IsFile)[] candidates)
    {
        foreach (var (text, isFile) in candidates)
        {
            if (!Path.IsPathFullyQualified(text))
            {
                continue;
            }

            var dir = isFile ? Path.GetDirectoryName(text) : text;
            if (!string.IsNullOrEmpty(dir) && exists(dir))
            {
                return dir;
            }
        }

        return scriptsDir;
    }

    /// <summary>Подбирает, чем запускать файл с таким расширением.</summary>
    /// <param name="extension">Расширение с точкой.</param>
    /// <param name="current">Выбранный сейчас тип.</param>
    /// <returns>Тип запуска; для незнакомого расширения прежний.</returns>
    public static LaunchType TypeFor(string extension, LaunchType current) => extension.ToLowerInvariant() switch
    {
        ".exe" => LaunchType.Exe,
        ".ps1" => current is LaunchType.PowerShell or LaunchType.Pwsh ? current : LaunchType.Pwsh,
        ".cmd" or ".bat" => LaunchType.Cmd,
        _ => current,
    };
}
