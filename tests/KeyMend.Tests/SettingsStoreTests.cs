using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SettingsStore"/> на временной папке.</summary>
public sealed class SettingsStoreTests : IDisposable
{
    /// <summary>Временная папка, уникальная для каждого теста.</summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KeyMendTests_" + Guid.NewGuid().ToString("N"));

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Путь к файлу настроек.</summary>
    private string FilePath => Path.Combine(_dir, "settings.json");

    /// <summary>Удаляет временную папку.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>Без файла возвращаются значения по умолчанию и признак первого запуска.</summary>
    [Fact]
    public void Load_NoFile_ReturnsDefaultsAndFirstRun()
    {
        var result = new SettingsStore(FilePath, _log).Load();

        Assert.True(result.IsFirstRun);
        Assert.Equal(DefaultSettings.Create().Rules.Count, result.Settings.Rules.Count);
    }

    /// <summary>Записанные настройки читаются обратно, временный файл не остаётся.</summary>
    [Fact]
    public void Save_ThenLoad_ReturnsSavedSettings()
    {
        var store = new SettingsStore(FilePath, _log);
        store.Save(DefaultSettings.Create() with { WordBufferLimit = 300 });

        var result = store.Load();

        Assert.False(result.IsFirstRun);
        Assert.Equal(300, result.Settings.WordBufferLimit);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    /// <summary>Битый файл копируется в .bad, возвращаются значения по умолчанию, ошибка попадает в лог.</summary>
    [Fact]
    public void Load_BrokenFile_SavesBadCopyAndReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        var result = new SettingsStore(FilePath, _log).Load();

        Assert.False(result.IsFirstRun);
        Assert.Equal(DefaultSettings.Create().Rules.Count, result.Settings.Rules.Count);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.Contains(_log.Entries, e => e.StartsWith("ERROR settings.json is invalid", StringComparison.Ordinal));
    }

    /// <summary>Перечитка битого файла не подставляет значения по умолчанию, а возвращает ошибку.</summary>
    [Fact]
    public void TryRead_BrokenFile_ReturnsErrorWithoutSettings()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        var result = new SettingsStore(FilePath, _log).TryRead();

        Assert.Null(result.Settings);
        Assert.NotNull(result.Error);
    }

    /// <summary>Пропущенные правила попадают в лог при чтении.</summary>
    [Fact]
    public void TryRead_SkippedRule_IsLogged()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "rules": [ { "name": "x", "trigger": { "kind": "Tap", "keys": "Banana" } } ] }""");

        var result = new SettingsStore(FilePath, _log).TryRead();

        Assert.NotNull(result.Settings);
        Assert.Contains(_log.Entries, e => e.Contains("rule #1 'x' skipped", StringComparison.Ordinal));
    }
}
