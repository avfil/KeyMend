using System.IO;
using Microsoft.Win32;

namespace KeyMend.Infrastructure;

/// <summary>Тема приложений Windows (SPEC F1): нужна, чтобы тема «Как в Windows» следовала за настройкой системы.</summary>
internal interface ISystemTheme
{
    /// <summary>Использует ли Windows светлую тему для приложений.</summary>
    /// <returns><c>true</c> — светлая; если настройка не читается, тоже <c>true</c>.</returns>
    bool IsLight();
}

/// <summary>Читает тему приложений из реестра (только чтение, SPEC 2.3).</summary>
internal sealed class RegistrySystemTheme : ISystemTheme
{
    /// <summary>Раздел реестра персонализации.</summary>
    private const string _keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Имя значения: 1 — светлая тема приложений, 0 — тёмная.</summary>
    private const string _valueName = "AppsUseLightTheme";

    /// <inheritdoc />
    public bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return key?.GetValue(_valueName) is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }
}
