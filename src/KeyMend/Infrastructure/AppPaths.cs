using System.IO;

namespace KeyMend.Infrastructure;

/// <summary>Пути данных программы (SPEC 2.2). Тесты передают временные папки.</summary>
/// <param name="DataDir">Папка настроек и скриптов (<c>%AppData%\KeyMend</c>).</param>
/// <param name="LogDir">Папка логов (<c>%LocalAppData%\KeyMend\logs</c>).</param>
internal sealed record AppPaths(string DataDir, string LogDir)
{
    /// <summary>Файл настроек и правил.</summary>
    public string SettingsFile => Path.Combine(DataDir, "settings.json");

    /// <summary>Папка скриптов и утилит по умолчанию.</summary>
    public string ScriptsDir => Path.Combine(DataDir, "scripts");

    /// <summary>Зашифрованные секретные тексты (SPEC 2.2).</summary>
    public string SecretsFile => Path.Combine(DataDir, "secrets.dat");

    /// <summary>Резервные копии перед загрузкой настроек (SPEC 2.2).</summary>
    public string BackupDir => Path.Combine(DataDir, "backup");

    /// <summary>Возвращает пути для текущего пользователя Windows.</summary>
    /// <returns>Пути в профиле пользователя.</returns>
    public static AppPaths ForCurrentUser() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyMend"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMend", "logs"));
}
