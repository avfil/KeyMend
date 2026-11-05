using Microsoft.Win32;

namespace KeyMend.Infrastructure;

/// <summary>Автозапуск для текущего пользователя через HKCU\Software\Microsoft\Windows\CurrentVersion\Run (SPEC 2.6).</summary>
internal sealed class Autostart
{
    /// <summary>Раздел реестра Run по умолчанию.</summary>
    internal const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Имя значения в разделе Run по умолчанию.</summary>
    internal const string DefaultValueName = "KeyMend";

    /// <summary>Раздел реестра, в котором лежит запись автозапуска.</summary>
    private readonly string _keyPath;

    /// <summary>Имя значения реестра.</summary>
    private readonly string _valueName;

    /// <summary>Путь к исполняемому файлу, который нужно запускать.</summary>
    private readonly string? _exePath;

    /// <summary>Создаёт объект; параметры нужны тестам, чтобы не трогать настоящий раздел Run.</summary>
    /// <param name="keyPath">Раздел реестра; по умолчанию Run.</param>
    /// <param name="valueName">Имя значения; по умолчанию «KeyMend».</param>
    /// <param name="exePath">Путь к exe; по умолчанию текущий процесс.</param>
    public Autostart(string? keyPath = null, string? valueName = null, string? exePath = null)
    {
        _keyPath = keyPath ?? DefaultKeyPath;
        _valueName = valueName ?? DefaultValueName;
        _exePath = exePath ?? Environment.ProcessPath;
    }

    /// <summary>Возвращает <c>true</c>, если запись Run существует и указывает на этот исполняемый файл.</summary>
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(_valueName) is string value
            && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Создаёт (или обновляет) запись автозапуска.</summary>
    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        key.SetValue(_valueName, Command, RegistryValueKind.String);
    }

    /// <summary>Удаляет запись автозапуска; если её нет — ничего не делает.</summary>
    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    /// <summary>Команда запуска: путь к exe в кавычках (защита от пробелов в пути).</summary>
    private string Command => $"\"{_exePath}\"";
}
