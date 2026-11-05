namespace KeyMend.UI.Settings;

/// <summary>Разбор имён программ в поле «Где работает»: правило привязывается к имени exe без пути.</summary>
internal static class ProcessNames
{
    /// <summary>Приводит введённое к имени exe.</summary>
    /// <param name="text">Введённый текст: <c>chrome</c>, <c>chrome.exe</c>, <c>C:\Program Files\Chrome\chrome.exe</c>.</param>
    /// <returns>Имя файла; без расширения дописывается <c>.exe</c>; <c>null</c>, если имя пустое или недопустимое.</returns>
    public static string? Normalize(string? text)
    {
        var name = text?.Trim().Trim('"') ?? string.Empty;
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        if (name.Length == 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.Trim('.').Length == 0)
        {
            return null;
        }

        return name.Contains('.', StringComparison.Ordinal) ? name : name + ".exe";
    }

    /// <summary>Добавляет программу в список без повторов (имена сравниваются без учёта регистра).</summary>
    /// <param name="current">Список сейчас.</param>
    /// <param name="text">Введённый текст.</param>
    /// <returns>Новый список; прежний, если ввод недопустим или имя уже есть.</returns>
    public static IReadOnlyList<string> Add(IReadOnlyList<string> current, string? text)
    {
        var name = Normalize(text);
        return name is null || current.Contains(name, StringComparer.OrdinalIgnoreCase) ? current : [.. current, name];
    }
}
