using KeyMend.Infrastructure;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="AppPaths"/>: расположение данных по SPEC 2.2.</summary>
public sealed class AppPathsTests
{
    /// <summary>Файл настроек и скрипты лежат внутри папки данных.</summary>
    [Fact]
    public void Paths_FromDataDir_AreInsideIt()
    {
        var paths = new AppPaths(@"C:\Data\KeyMend", @"C:\Logs");

        Assert.Equal(@"C:\Data\KeyMend\settings.json", paths.SettingsFile);
        Assert.Equal(@"C:\Data\KeyMend\scripts", paths.ScriptsDir);
    }

    /// <summary>Для текущего пользователя данные в %AppData%\KeyMend, логи в %LocalAppData%\KeyMend\logs.</summary>
    [Fact]
    public void ForCurrentUser_Always_UsesProfileFolders()
    {
        var paths = AppPaths.ForCurrentUser();

        Assert.EndsWith(@"\KeyMend", paths.DataDir, StringComparison.Ordinal);
        Assert.EndsWith(@"\KeyMend\logs", paths.LogDir, StringComparison.Ordinal);
    }
}
