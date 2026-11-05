using KeyMend.Infrastructure;
using Microsoft.Win32;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="Autostart"/>; работают с временным разделом реестра, настоящий Run не затрагивается.</summary>
public sealed class AutostartTests : IDisposable
{
    /// <summary>Временный раздел реестра, уникальный для каждого теста.</summary>
    private readonly string _keyPath = @"Software\KeyMendTests_" + Guid.NewGuid().ToString("N");

    /// <summary>Удаляет временный раздел реестра.</summary>
    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    /// <summary>Создаёт <see cref="Autostart"/> на временном разделе для указанного exe.</summary>
    private Autostart Create(string exe = @"C:\Apps\KeyMend.exe") => new(_keyPath, "Test", exe);

    /// <summary>Без записи в реестре автозапуск выключен.</summary>
    [Fact]
    public void IsEnabled_FalseByDefault() => Assert.False(Create().IsEnabled());

    /// <summary>Путь пишется в кавычках, чтобы пробелы в пути не ломали запуск.</summary>
    [Fact]
    public void Enable_WritesQuotedPath()
    {
        Create().Enable();

        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        Assert.Equal(@"""C:\Apps\KeyMend.exe""", key!.GetValue("Test"));
        Assert.True(Create().IsEnabled());
    }

    /// <summary>Disable удаляет запись.</summary>
    [Fact]
    public void Disable_RemovesEntry()
    {
        var autostart = Create();
        autostart.Enable();

        autostart.Disable();

        Assert.False(autostart.IsEnabled());
    }

    /// <summary>Disable без записи ничего не делает.</summary>
    [Fact]
    public void Disable_WhenNotEnabled_DoesNotThrow() => Create().Disable();

    /// <summary>Если запись указывает на другой exe (программу перенесли), автозапуск считается выключенным.</summary>
    [Fact]
    public void IsEnabled_FalseWhenEntryPointsElsewhere()
    {
        Create(@"C:\Old\KeyMend.exe").Enable();

        Assert.False(Create(@"C:\New\KeyMend.exe").IsEnabled());
    }
}
