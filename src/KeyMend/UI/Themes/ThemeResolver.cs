using KeyMend.Storage;

namespace KeyMend.UI.Themes;

/// <summary>Свёртка настройки темы в конкретную: «Как в Windows» превращается в светлую или тёмную (SPEC F1).</summary>
internal static class ThemeResolver
{
    /// <summary>Определяет, должна ли тема быть тёмной.</summary>
    /// <param name="theme">Настройка пользователя.</param>
    /// <param name="systemIsLight">Использует ли Windows светлую тему приложений.</param>
    /// <returns><c>true</c> — тёмная.</returns>
    public static bool IsDark(AppTheme theme, bool systemIsLight) => theme switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => !systemIsLight,
    };
}
