using System.IO;
using System.Reflection;
using KeyMend.Infrastructure;

namespace KeyMend.UI.Settings;

/// <summary>Сведения для раздела «О программе»: версия, режим и папки приложения (SPEC 5).</summary>
/// <param name="Version">Версия сборки.</param>
/// <param name="Elevated">Запущен от администратора.</param>
/// <param name="ProgramDir">Папка, откуда запущен exe.</param>
/// <param name="DataDir">Папка настроек, секретов и резервных копий.</param>
/// <param name="ScriptsDir">Папка скриптов.</param>
/// <param name="LogDir">Папка журнала работы.</param>
/// <param name="RegistryKey">Полный путь раздела реестра, в который пишется автозапуск.</param>
/// <param name="RegistryValue">Имя значения автозапуска в этом разделе.</param>
internal sealed record AboutInfo(string Version, bool Elevated, string ProgramDir, string DataDir, string ScriptsDir, string LogDir, string RegistryKey, string RegistryValue)
{
    /// <summary>Адрес репозитория проекта: показывается и копируется в буфер, программа по нему не ходит (сеть не используется).</summary>
    public const string RepositoryUrl = "https://github.com/avfil/KeyMend";

    /// <summary>Собирает сведения о запущенной программе.</summary>
    /// <param name="paths">Пути данных.</param>
    /// <param name="elevated">Запущен от администратора.</param>
    /// <returns>Сведения для страницы.</returns>
    public static AboutInfo Create(AppPaths paths, bool elevated)
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus > 0)
        {
            version = version[..plus];
        }

        var program = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
        return new AboutInfo(version, elevated, program, paths.DataDir, paths.ScriptsDir, paths.LogDir, @"HKEY_CURRENT_USER\" + Autostart.DefaultKeyPath, Autostart.DefaultValueName);
    }
}
