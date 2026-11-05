using System.Diagnostics;
using System.IO;
using KeyMend.Input;

namespace KeyMend.Infrastructure;

/// <summary>Запущенная программа с окном: имя exe и описание файла («Google Chrome»).</summary>
/// <param name="Exe">Имя exe без пути.</param>
/// <param name="Description">Описание из свойств файла или <c>null</c>.</param>
internal sealed record RunningApp(string Exe, string? Description);

/// <summary>Список запущенных программ для раздела «Исключения» (SPEC F7).</summary>
internal interface IRunningApps
{
    /// <summary>Возвращает программы текущего сеанса, у которых есть окно.</summary>
    /// <returns>Программы без повторов по имени exe.</returns>
    IReadOnlyList<RunningApp> List();

    /// <summary>Подбирает описания программ по именам exe: из запущенных, иначе из папки System32.</summary>
    /// <param name="exes">Имена exe.</param>
    /// <returns>Описание по имени exe (без учёта регистра); <c>null</c>, если описания нет.</returns>
    IReadOnlyDictionary<string, string?> Describe(IEnumerable<string> exes);
}

/// <summary>Запущенные программы Windows: процессы с главным окном, путь — через <c>QueryFullProcessImageName</c>.</summary>
internal sealed class WindowsRunningApps : IRunningApps
{
    /// <inheritdoc />
    public IReadOnlyList<RunningApp> List()
    {
        var result = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.MainWindowHandle == 0 || ForegroundWatcher.ResolveProcessPath((uint)process.Id) is not { } path)
                {
                    continue;
                }

                var exe = Path.GetFileName(path);
                if (!result.ContainsKey(exe))
                {
                    result[exe] = new RunningApp(exe, DescriptionOf(path));
                }
            }
        }

        return [.. result.Values];
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> Describe(IEnumerable<string> exes)
    {
        var names = exes.ToList();
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
        {
            return result;
        }

        var running = List().ToDictionary(a => a.Exe, a => a.Description, StringComparer.OrdinalIgnoreCase);
        foreach (var exe in names)
        {
            result[exe] = running.TryGetValue(exe, out var description)
                ? description
                : DescriptionOf(Path.Combine(Environment.SystemDirectory, exe));
        }

        return result;
    }

    /// <summary>Читает описание файла; нет файла или описания — <c>null</c>.</summary>
    /// <param name="path">Путь к exe.</param>
    /// <returns>Описание без пробелов по краям или <c>null</c>.</returns>
    private static string? DescriptionOf(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
        return string.IsNullOrEmpty(description) ? null : description;
    }
}
