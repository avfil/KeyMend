using System.Security.Cryptography;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SecretStore"/> и <see cref="SecretSealer"/>: запись, чтение, повреждённый файл, перенос секретов из настроек.</summary>
public sealed class SecretStoreTests : IDisposable
{
    /// <summary>«Шифр»-заглушка: разворачивает байты и ставит метку, чтобы чужие данные не расшифровывались.</summary>
    private sealed class ReversingProtector : ISecretProtector
    {
        /// <inheritdoc />
        public byte[] Protect(byte[] data) => [.. data.Reverse(), 0x7F];

        /// <inheritdoc />
        public byte[] Unprotect(byte[] data) =>
            data.Length > 0 && data[^1] == 0x7F ? [.. data[..^1].Reverse()] : throw new CryptographicException("bad data");
    }

    /// <summary>Временная папка теста.</summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KeyMendTests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Путь к <c>secrets.dat</c> теста.</summary>
    private string SecretsPath => Path.Combine(_dir, "secrets.dat");

    /// <summary>Удаляет временную папку.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>Записанные секреты читаются новым экземпляром, а в файле нет открытого текста.</summary>
    [Fact]
    public void SetMany_ThenNewInstance_ReadsBackAndFileIsNotPlain()
    {
        var first = new SecretStore(SecretsPath, new ReversingProtector(), _log);

        Assert.True(first.SetMany(new Dictionary<string, string> { ["k1"] = "test-secret-1", ["k2"] = "test-secret-2" }));
        var second = new SecretStore(SecretsPath, new ReversingProtector(), _log);

        Assert.True(second.TryGet("k1", out var text));
        Assert.Equal("test-secret-1", text);
        Assert.False(second.TryGet("missing", out _));
        Assert.DoesNotContain("test-secret-1", File.ReadAllText(SecretsPath), StringComparison.Ordinal);
    }

    /// <summary>Повторная запись добавляет новые ключи и заменяет старые, остальные остаются.</summary>
    [Fact]
    public void SetMany_Twice_MergesKeys()
    {
        var store = new SecretStore(SecretsPath, new ReversingProtector(), _log);
        store.SetMany(new Dictionary<string, string> { ["a"] = "test-secret-1", ["b"] = "test-secret-2" });

        store.SetMany(new Dictionary<string, string> { ["b"] = "test-secret-3" });

        Assert.True(store.TryGet("a", out var a));
        Assert.True(store.TryGet("b", out var b));
        Assert.Equal("test-secret-1", a);
        Assert.Equal("test-secret-3", b);
    }

    /// <summary>Удалённый ключ пропадает и из памяти, и из файла; остальные остаются, неизвестные ключи не мешают.</summary>
    [Fact]
    public void Remove_KnownAndUnknownKeys_DropsOnlyKnownOnDisk()
    {
        var store = new SecretStore(SecretsPath, new ReversingProtector(), _log);
        store.SetMany(new Dictionary<string, string> { ["a"] = "test-secret-1", ["b"] = "test-secret-2" });

        Assert.True(store.Remove(["a", "missing"]));

        var reopened = new SecretStore(SecretsPath, new ReversingProtector(), _log);
        Assert.False(reopened.TryGet("a", out _));
        Assert.True(reopened.TryGet("b", out var b));
        Assert.Equal("test-secret-2", b);
    }

    /// <summary>Нечитаемый файл сохраняется как <c>.bad</c>, хранилище пустое, в логе нет текстов.</summary>
    [Fact]
    public void TryGet_CorruptFile_KeepsBadCopyAndIsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(SecretsPath, [1, 2, 3]);
        var store = new SecretStore(SecretsPath, new ReversingProtector(), _log);

        var found = store.TryGet("k", out _);

        Assert.False(found);
        Assert.True(File.Exists(SecretsPath + ".bad"));
        Assert.Contains(_log.Entries, e => e.StartsWith("ERROR secrets.dat cannot be read", StringComparison.Ordinal));
    }

    /// <summary>Открытые секреты переносятся в ключи, обычные сокращения остаются нетронутыми.</summary>
    [Fact]
    public void Seal_SecretWithText_MovesTextToSecretsAndKeepsRef()
    {
        var settings = new AppSettings
        {
            Snippets =
            [
                new Snippet { Id = "a", Keys = "sec1 ", Secret = true, Text = "test-secret-1" },
                new Snippet { Id = "b", Keys = "дд ", Text = "Добрый день!" },
                new Snippet { Id = "c", Keys = "sec2 ", Secret = true, Text = "test-secret-2", SecretRef = "old" },
                new Snippet { Id = "d", Keys = "sec4 ", Secret = true, SecretRef = "kept" },
            ],
        };
        var counter = 0;

        var result = SecretSealer.Seal(settings, () => "new" + counter++);

        Assert.Equal("test-secret-1", result.Secrets["new0"]);
        Assert.Equal("test-secret-2", result.Secrets["old"]);
        Assert.Equal(2, result.Secrets.Count);
        Assert.All(result.Settings.Snippets.Where(s => s.Secret), s => Assert.Null(s.Text));
        Assert.Equal(["new0", null, "old", "kept"], result.Settings.Snippets.Select(s => s.SecretRef));
        Assert.Equal("Добрый день!", result.Settings.Snippets[1].Text);
    }

    /// <summary>Если переносить нечего, возвращаются те же настройки.</summary>
    [Fact]
    public void Seal_NothingToSeal_ReturnsSameSettings()
    {
        var settings = new AppSettings { Snippets = [new Snippet { Id = "b", Keys = "дд ", Text = "Добрый день!" }] };

        var result = SecretSealer.Seal(settings, () => "x");

        Assert.Same(settings, result.Settings);
        Assert.Empty(result.Secrets);
    }
}
