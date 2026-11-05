using System.Diagnostics.CodeAnalysis;
using System.IO;
using KeyMend.Rules;

namespace KeyMend.Actions;

/// <summary>Готовая к запуску команда: что именно и с какими аргументами стартует процесс.</summary>
/// <param name="FileName">Полный путь исполняемого файла (программа или интерпретатор).</param>
/// <param name="Arguments">Командная строка без имени файла.</param>
/// <param name="WorkingDirectory">Полный путь рабочей папки.</param>
/// <param name="Window">Окно процесса.</param>
internal sealed record LaunchCommand(string FileName, string Arguments, string WorkingDirectory, LaunchWindow Window);

/// <summary>Окружение, по которому разбираются пути шага «Запустить программу»; в тестах подменяется.</summary>
/// <param name="ScriptsDir">Папка скриптов: от неё считаются относительные пути, выходить из неё нельзя.</param>
/// <param name="SystemDir">Системная папка Windows (<c>System32</c>).</param>
/// <param name="ProgramFilesDir">Папка <c>Program Files</c>.</param>
/// <param name="PathDirs">Папки из переменной <c>PATH</c>.</param>
/// <param name="FileExists">Проверка существования файла.</param>
/// <param name="DirectoryExists">Проверка существования папки.</param>
internal sealed record LaunchEnvironment(
    string ScriptsDir,
    string SystemDir,
    string ProgramFilesDir,
    IReadOnlyList<string> PathDirs,
    Func<string, bool> FileExists,
    Func<string, bool> DirectoryExists)
{
    /// <summary>Создаёт окружение текущего компьютера.</summary>
    /// <param name="scriptsDir">Папка скриптов.</param>
    /// <returns>Окружение с настоящей файловой системой.</returns>
    public static LaunchEnvironment Current(string scriptsDir) => new(
        scriptsDir,
        Environment.SystemDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries),
        File.Exists,
        Directory.Exists);
}

/// <summary>Превращает шаг «Запустить программу» в команду: разбирает пути, выбирает интерпретатор (SPEC F4.1). Без запуска и без WinAPI.</summary>
internal static class LaunchCommandBuilder
{
    /// <summary>Символы, по которым имя файла считается путём, а не голым именем программы.</summary>
    private static readonly char[] _pathChars = ['\\', '/', ':'];

    /// <summary>Собирает команду по шагу.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="env">Окружение для разбора путей.</param>
    /// <param name="command">Команда, если шаг корректен.</param>
    /// <param name="error">Причина отказа без содержимого ввода, если шаг некорректен.</param>
    /// <returns><c>true</c>, если команду можно запускать.</returns>
    public static bool TryBuild(
        LaunchStep step, LaunchEnvironment env, [NotNullWhen(true)] out LaunchCommand? command, [NotNullWhen(false)] out string? error)
    {
        command = null;
        var file = step.File.Trim();
        if (file.Length == 0)
        {
            error = "file is not set";
            return false;
        }

        if (file.Contains('"') || file.Contains('\0'))
        {
            error = "file name has invalid characters";
            return false;
        }

        if (!TryResolveFile(file, step.Type == LaunchType.Exe, env, out var target, out error))
        {
            return false;
        }

        string workDir;
        if (string.IsNullOrWhiteSpace(step.WorkingDirectory))
        {
            workDir = Path.GetDirectoryName(target) ?? env.ScriptsDir;
        }
        else if (!TryResolvePath(step.WorkingDirectory.Trim(), env, out workDir, out error))
        {
            return false;
        }

        if (!env.DirectoryExists(workDir))
        {
            error = "working directory is not found";
            return false;
        }

        if (!TryGetInterpreter(step.Type, env, out var program, out error))
        {
            return false;
        }

        var arguments = step.Arguments.Trim();
        command = step.Type switch
        {
            LaunchType.Exe => new LaunchCommand(target, arguments, workDir, step.Window),
            LaunchType.Cmd => new LaunchCommand(program, $"/c \"\"{target}\" {arguments}\"", workDir, step.Window),
            _ => new LaunchCommand(
                program, $"-NoProfile -ExecutionPolicy Bypass -File \"{target}\" {arguments}".TrimEnd(), workDir, step.Window),
        };
        return true;
    }

    /// <summary>Допустим ли файл шага по записи: без кавычек и нулевых символов, относительный путь не выходит из папки скриптов.</summary>
    /// <param name="file">Файл из шага, без пробелов по краям.</param>
    /// <param name="scriptsDir">Папка скриптов.</param>
    /// <returns><c>true</c>, если запись допустима.</returns>
    public static bool IsFileAllowed(string file, string scriptsDir) =>
        !file.Contains('"') && !file.Contains('\0')
        && TryResolvePath(file, new LaunchEnvironment(scriptsDir, string.Empty, string.Empty, [], _ => false, _ => false), out _, out _);

    /// <summary>Находит файл шага: полный путь, путь внутри папки скриптов или (для программ) имя из PATH.</summary>
    /// <param name="file">Файл из шага.</param>
    /// <param name="searchPath">Искать ли голое имя в PATH.</param>
    /// <param name="env">Окружение.</param>
    /// <param name="target">Полный путь найденного файла.</param>
    /// <param name="error">Причина отказа.</param>
    /// <returns><c>true</c>, если файл найден.</returns>
    private static bool TryResolveFile(
        string file, bool searchPath, LaunchEnvironment env, out string target, [NotNullWhen(false)] out string? error)
    {
        if (!TryResolvePath(file, env, out target, out error))
        {
            return false;
        }

        if (env.FileExists(target))
        {
            return true;
        }

        if (searchPath && file.IndexOfAny(_pathChars) < 0)
        {
            var found = FindInPath(file, env);
            if (found is not null)
            {
                target = found;
                return true;
            }
        }

        error = "file is not found";
        return false;
    }

    /// <summary>Приводит путь к полному; относительный считается от папки скриптов и не может из неё выйти.</summary>
    /// <param name="path">Путь из шага.</param>
    /// <param name="env">Окружение.</param>
    /// <param name="full">Полный путь.</param>
    /// <param name="error">Причина отказа.</param>
    /// <returns><c>true</c>, если путь допустим.</returns>
    private static bool TryResolvePath(string path, LaunchEnvironment env, out string full, [NotNullWhen(false)] out string? error)
    {
        error = null;
        if (Path.IsPathFullyQualified(path))
        {
            full = Path.GetFullPath(path);
            return true;
        }

        full = Path.GetFullPath(Path.Combine(env.ScriptsDir, path));
        if (Path.IsPathRooted(path) || !IsInside(env.ScriptsDir, full))
        {
            error = "path is outside the scripts folder";
            return false;
        }

        return true;
    }

    /// <summary>Лежит ли путь внутри папки (по полным путям, без учёта регистра).</summary>
    /// <param name="root">Папка.</param>
    /// <param name="path">Полный путь.</param>
    /// <returns><c>true</c>, если путь внутри папки.</returns>
    private static bool IsInside(string root, string path)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ищет имя программы в папках PATH.</summary>
    /// <param name="name">Имя файла; без расширения добавляется <c>.exe</c>.</param>
    /// <param name="env">Окружение.</param>
    /// <returns>Полный путь или <c>null</c>.</returns>
    private static string? FindInPath(string name, LaunchEnvironment env)
    {
        var fileName = Path.HasExtension(name) ? name : name + ".exe";
        return env.PathDirs.Select(dir => Path.Combine(dir, fileName)).FirstOrDefault(env.FileExists);
    }

    /// <summary>Выбирает интерпретатор для типа запуска.</summary>
    /// <param name="type">Тип запуска.</param>
    /// <param name="env">Окружение.</param>
    /// <param name="program">Полный путь интерпретатора; для программы пусто.</param>
    /// <param name="error">Причина отказа.</param>
    /// <returns><c>true</c>, если интерпретатор найден.</returns>
    private static bool TryGetInterpreter(LaunchType type, LaunchEnvironment env, out string program, [NotNullWhen(false)] out string? error)
    {
        error = null;
        switch (type)
        {
            case LaunchType.PowerShell:
                program = Path.Combine(env.SystemDir, "WindowsPowerShell", "v1.0", "powershell.exe");
                break;
            case LaunchType.Cmd:
                program = Path.Combine(env.SystemDir, "cmd.exe");
                break;
            case LaunchType.Pwsh:
                program = FindInPath("pwsh.exe", env) ?? Path.Combine(env.ProgramFilesDir, "PowerShell", "7", "pwsh.exe");
                if (!env.FileExists(program))
                {
                    error = "pwsh.exe is not found";
                    return false;
                }

                break;
            default:
                program = string.Empty;
                break;
        }

        return true;
    }
}
