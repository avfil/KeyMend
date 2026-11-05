using System.Security.Cryptography;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SettingsTransfer"/> и <see cref="BackupStore"/> на временных папках: экспорт, загрузка, откат, копии (SPEC F8).</summary>
public sealed class SettingsTransferTests : IDisposable
{
    /// <summary>«Шифр»-заглушка вместо DPAPI: разворачивает байты и ставит метку.</summary>
    private sealed class ReversingProtector : ISecretProtector
    {
        /// <inheritdoc />
        public byte[] Protect(byte[] data) => [.. data.Reverse(), 0x7F];

        /// <inheritdoc />
        public byte[] Unprotect(byte[] data) =>
            data.Length > 0 && data[^1] == 0x7F ? [.. data[..^1].Reverse()] : throw new CryptographicException("bad data");
    }

    /// <summary>Таблицы раскладок без обращения к системе.</summary>
    private sealed class FakeTables : IInstalledTables
    {
        /// <inheritdoc />
        public IReadOnlyList<LayoutTable> Tables() => [FallbackLayouts.Us, FallbackLayouts.Ru];
    }

    /// <summary>Один «компьютер»: своя папка данных, настройки, секреты и перенос.</summary>
    private sealed class Computer
    {
        /// <summary>Создаёт компьютер с заданными настройками и папкой.</summary>
        /// <param name="dir">Папка данных.</param>
        /// <param name="initial">Настройки.</param>
        /// <param name="time">Часы.</param>
        public Computer(string dir, AppSettings initial, TimeProvider time)
        {
            Paths = new AppPaths(dir, Path.Combine(dir, "logs"));
            Directory.CreateDirectory(Paths.ScriptsDir);
            Settings = new SettingsService(initial, new SettingsStore(Paths.SettingsFile, Log), Log);
            _ = Settings.Update(_ => initial);
            Secrets = new SecretStore(Paths.SecretsFile, new ReversingProtector(), Log);
            Backups = new BackupStore(Paths, time, Log);
            var counter = 0;
            Transfer = new SettingsTransfer(
                Paths, Settings, Secrets, new ReversingProtector(), Backups, new FakeTables(), () => new Guid(++counter, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Log);
        }

        /// <summary>Журнал.</summary>
        public TestLog Log { get; } = new();

        /// <summary>Пути.</summary>
        public AppPaths Paths { get; }

        /// <summary>Настройки.</summary>
        public SettingsService Settings { get; }

        /// <summary>Секреты.</summary>
        public SecretStore Secrets { get; }

        /// <summary>Копии.</summary>
        public BackupStore Backups { get; }

        /// <summary>Перенос.</summary>
        public SettingsTransfer Transfer { get; }
    }

    /// <summary>Корневая временная папка теста.</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "KeyMendTests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Часы теста.</summary>
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Удаляет временные папки.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Создаёт компьютер в подпапке.</summary>
    private Computer NewComputer(string name, AppSettings settings) => new(Path.Combine(_root, name), settings, _time);

    /// <summary>Настройки «источника»: секретное сокращение и обычное правило.</summary>
    private static AppSettings SourceSettings() => new()
    {
        WordBufferLimit = 300,
        Rules = [new Rule { Id = "r-1", Name = "Сон", Trigger = new PressTrigger { Key = (VirtualKey)0x7E }, Steps = [new ServiceStep { Action = ServiceAction.TogglePause }] }],
        Snippets = [new Snippet { Id = "a-1", Keys = "abc ", Secret = true, SecretRef = "s-src", Insert = InsertMode.Clipboard }],
    };

    /// <summary>Источник с секретом и скриптом.</summary>
    private async Task<Computer> NewSourceAsync()
    {
        var source = NewComputer("source", SourceSettings());
        Assert.True(source.Secrets.SetMany(new Dictionary<string, string> { ["s-src"] = "test-secret-1" }));
        await File.WriteAllTextAsync(Path.Combine(source.Paths.ScriptsDir, "run.cmd"), "echo source", TestContext.Current.CancellationToken);
        return source;
    }

    /// <summary>Сохраняет файл переноса источника и открывает его на приёмнике.</summary>
    private async Task<(string Path, ImportPreview Preview)> ExportAndOpenAsync(Computer source, Computer target, string? password)
    {
        var path = Path.Combine(_root, "export.keymend");
        Assert.Equal(ExportResult.Saved, await source.Transfer.ExportAsync(path, password, TestContext.Current.CancellationToken));
        var opened = await target.Transfer.OpenAsync(path, TestContext.Current.CancellationToken);
        return (path, Assert.IsType<ImportPreview>(opened.Preview));
    }

    /// <summary>Туда и обратно: настройки, скрипт и секрет переезжают на другой компьютер, секрет под новым ключом.</summary>
    [Fact]
    public async Task ExportThenImportReplace_WithSecrets_MovesEverything()
    {
        var source = await NewSourceAsync();
        var target = NewComputer("target", new AppSettings());
        var (_, preview) = await ExportAndOpenAsync(source, target, "test-password-1");

        var result = await target.Transfer.ImportAsync(preview, ImportMode.Replace, new HashSet<ConflictKey>(), "test-password-1", TestContext.Current.CancellationToken);

        Assert.Equal(ImportFailure.None, result.Failure);
        Assert.Equal(300, target.Settings.Current.WordBufferLimit);
        var snippet = Assert.Single(target.Settings.Current.Snippets);
        Assert.NotEqual("s-src", snippet.SecretRef);
        Assert.True(target.Secrets.TryGet(snippet.SecretRef!, out var text));
        Assert.Equal("test-secret-1", text);
        Assert.Equal("echo source", await File.ReadAllTextAsync(Path.Combine(target.Paths.ScriptsDir, "run.cmd"), TestContext.Current.CancellationToken));
    }

    /// <summary>В файле нет открытого текста секрета и пароля.</summary>
    [Fact]
    public async Task Export_WithSecrets_FileHasNoPlainText()
    {
        var source = await NewSourceAsync();
        var path = Path.Combine(_root, "plain-check.keymend");

        _ = await source.Transfer.ExportAsync(path, "test-password-1", TestContext.Current.CancellationToken);
        var text = System.Text.Encoding.Latin1.GetString(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("test-secret-1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("test-password-1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret-1", string.Join('\n', source.Log.Entries), StringComparison.Ordinal);
    }

    /// <summary>Неверный пароль: ничего не меняется и копия не создаётся.</summary>
    [Fact]
    public async Task Import_WrongPassword_ChangesNothing()
    {
        var source = await NewSourceAsync();
        var target = NewComputer("target", new AppSettings());
        var (_, preview) = await ExportAndOpenAsync(source, target, "test-password-1");

        var result = await target.Transfer.ImportAsync(preview, ImportMode.Replace, new HashSet<ConflictKey>(), "test-password-2", TestContext.Current.CancellationToken);

        Assert.Equal(ImportFailure.WrongPassword, result.Failure);
        Assert.Empty(target.Settings.Current.Snippets);
        Assert.Empty(target.Backups.List());
    }

    /// <summary>Без пароля секретное сокращение приходит выключенным.</summary>
    [Fact]
    public async Task Import_WithoutPassword_DisablesSecretSnippet()
    {
        var source = await NewSourceAsync();
        var target = NewComputer("target", new AppSettings());
        var (_, preview) = await ExportAndOpenAsync(source, target, "test-password-1");

        var result = await target.Transfer.ImportAsync(preview, ImportMode.Replace, new HashSet<ConflictKey>(), null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Report!.SnippetsWithoutSecret);
        Assert.False(Assert.Single(target.Settings.Current.Snippets).Enabled);
    }

    /// <summary>Объединение не трогает отличающийся файл владельца и сообщает о нём.</summary>
    [Fact]
    public async Task ImportMerge_DifferentScript_KeepsOwnAndReportsSkipped()
    {
        var source = await NewSourceAsync();
        var target = NewComputer("target", new AppSettings());
        await File.WriteAllTextAsync(Path.Combine(target.Paths.ScriptsDir, "run.cmd"), "echo mine", TestContext.Current.CancellationToken);
        var (_, preview) = await ExportAndOpenAsync(source, target, null);

        var result = await target.Transfer.ImportAsync(preview, ImportMode.Merge, new HashSet<ConflictKey>(), null, TestContext.Current.CancellationToken);

        Assert.Equal(["run.cmd"], result.SkippedScripts);
        Assert.Equal("echo mine", await File.ReadAllTextAsync(Path.Combine(target.Paths.ScriptsDir, "run.cmd"), TestContext.Current.CancellationToken));
    }

    /// <summary>Перед загрузкой создаётся копия с именем файла; восстановление возвращает прежнее.</summary>
    [Fact]
    public async Task Restore_AfterImport_ReturnsPreviousState()
    {
        var source = await NewSourceAsync();
        var target = NewComputer("target", new AppSettings { WordBufferLimit = 55 });
        var (_, preview) = await ExportAndOpenAsync(source, target, "test-password-1");
        _ = await target.Transfer.ImportAsync(preview, ImportMode.Replace, new HashSet<ConflictKey>(), "test-password-1", TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(5));

        var backup = Assert.Single(target.Backups.List());
        var result = await target.Transfer.RestoreAsync(backup, TestContext.Current.CancellationToken);

        Assert.Equal("export.keymend", backup.SourceName);
        Assert.Equal(ImportFailure.None, result.Failure);
        Assert.Equal(55, target.Settings.Current.WordBufferLimit);
        Assert.Empty(target.Settings.Current.Snippets);
        Assert.Equal(2, target.Backups.List().Count);
    }

    /// <summary>Битый и неподходящий файл отвергаются, настройки не меняются.</summary>
    [Fact]
    public async Task Open_BrokenFile_ReturnsError()
    {
        var target = NewComputer("target", new AppSettings { WordBufferLimit = 55 });
        var path = Path.Combine(_root, "broken.keymend");
        await File.WriteAllTextAsync(path, "definitely not a zip archive, just text", TestContext.Current.CancellationToken);

        var result = await target.Transfer.OpenAsync(path, TestContext.Current.CancellationToken);

        Assert.Null(result.Preview);
        Assert.Equal(TransferError.Unreadable, result.Error);
        Assert.Equal(55, target.Settings.Current.WordBufferLimit);
    }

    /// <summary>Обычный <c>settings.json</c> (стартовый набор) открывается как файл без скриптов.</summary>
    [Fact]
    public async Task Open_PlainJson_Works()
    {
        var target = NewComputer("target", new AppSettings());
        var path = Path.Combine(_root, "starter.json");
        await File.WriteAllTextAsync(path, SettingsJson.Serialize(SourceSettings()), TestContext.Current.CancellationToken);

        var result = await target.Transfer.OpenAsync(path, TestContext.Current.CancellationToken);

        Assert.Single(result.Preview!.Package.Settings.Rules);
        Assert.Empty(result.Preview.Package.Scripts);
    }

    /// <summary>Хранится не больше десяти копий, самые новые первыми.</summary>
    [Fact]
    public void Create_MoreThanTenTimes_KeepsLastTen()
    {
        var computer = NewComputer("backups", new AppSettings());

        for (var i = 0; i < 12; i++)
        {
            Assert.NotNull(computer.Backups.Create($"file{i}.keymend"));
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var list = computer.Backups.List();
        Assert.Equal(BackupStore.MaxCopies, list.Count);
        Assert.Equal("file11.keymend", list[0].SourceName);
        Assert.Equal("file2.keymend", list[^1].SourceName);
    }
}
