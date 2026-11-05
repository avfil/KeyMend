using System.Diagnostics.CodeAnalysis;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SnippetSaver"/>: запись секретов в хранилище, снятие секрета, удаление и сбои записи.</summary>
public sealed class SnippetSaverTests
{
    /// <summary>Писатель настроек-заглушка.</summary>
    private sealed class FakeWriter : ISettingsWriter
    {
        /// <summary>Падать ли при записи.</summary>
        public bool Fail { get; set; }

        /// <summary>Что записано последним.</summary>
        public AppSettings? Saved { get; private set; }

        /// <inheritdoc />
        public void Save(AppSettings settings)
        {
            if (Fail)
            {
                throw new IOException("disk full");
            }

            Saved = settings;
        }
    }

    /// <summary>Хранилище секретов-заглушка.</summary>
    private sealed class FakeSecrets : ISecretStore
    {
        /// <summary>Секреты по ключам.</summary>
        public Dictionary<string, string> Items { get; } = [];

        /// <summary>Принимает ли хранилище записи.</summary>
        public bool Fail { get; set; }

        /// <inheritdoc />
        public bool TryGet(string key, [NotNullWhen(true)] out string? text) => Items.TryGetValue(key, out text);

        /// <inheritdoc />
        public bool SetMany(IReadOnlyDictionary<string, string> secrets)
        {
            if (Fail)
            {
                return false;
            }

            foreach (var (key, text) in secrets)
            {
                Items[key] = text;
            }

            return true;
        }

        /// <inheritdoc />
        public bool Remove(IReadOnlyCollection<string> keys)
        {
            foreach (var key in keys)
            {
                Items.Remove(key);
            }

            return true;
        }
    }

    /// <summary>Писатель настроек.</summary>
    private readonly FakeWriter _writer = new();

    /// <summary>Хранилище секретов.</summary>
    private readonly FakeSecrets _secrets = new();

    /// <summary>Служба настроек с заданными сокращениями.</summary>
    private readonly SettingsService _settings;

    /// <summary>Записывающий.</summary>
    private readonly SnippetSaver _saver;

    /// <summary>Создаёт стенд с одним обычным сокращением.</summary>
    public SnippetSaverTests()
    {
        var log = new TestLog();
        _settings = new SettingsService(
            new AppSettings { Snippets = [new Snippet { Id = "dd", Keys = "дд ", Text = "Добрый день!" }] }, _writer, log);
        _saver = new SnippetSaver(_settings, _secrets, Guid.NewGuid, log);
    }

    /// <summary>Новый секрет: текст в хранилище, в настройках только ключ и вставка через буфер.</summary>
    [Fact]
    public void Save_NewSecret_StoresTextAndKeepsOnlyRefInSettings()
    {
        var draft = new SnippetDraft { Keys = "sec2 ", Secret = true, Text = "test-secret-1" };

        Assert.Equal(SnippetSaveResult.Saved, _saver.Save(null, draft));

        var saved = _settings.Current.Snippets[^1];
        Assert.True(saved.Secret);
        Assert.Null(saved.Text);
        Assert.Equal(InsertMode.Clipboard, saved.Insert);
        Assert.StartsWith("s-", saved.SecretRef, StringComparison.Ordinal);
        Assert.Equal("test-secret-1", _secrets.Items[saved.SecretRef!]);
        Assert.DoesNotContain("test-secret-1", SettingsJson.Serialize(_writer.Saved!), StringComparison.Ordinal);
    }

    /// <summary>Правка секрета оставляет тот же ключ и заменяет текст.</summary>
    [Fact]
    public void Save_EditSecret_KeepsKeyAndReplacesText()
    {
        _saver.Save(null, new SnippetDraft { Keys = "sec2 ", Secret = true, Text = "test-secret-1" });
        var original = _settings.Current.Snippets[^1];

        _saver.Save(original, SnippetDraft.From(original, "test-secret-1") with { Text = "test-secret-2" });

        Assert.Equal(original.SecretRef, _settings.Current.Snippets[^1].SecretRef);
        Assert.Equal("test-secret-2", _secrets.Items[original.SecretRef!]);
        Assert.Single(_secrets.Items);
    }

    /// <summary>Секрет перестал быть секретом: текст в настройках, ключ ушёл из хранилища.</summary>
    [Fact]
    public void Save_SecretTurnedOff_MovesTextToSettingsAndDropsKey()
    {
        _saver.Save(null, new SnippetDraft { Keys = "sec2 ", Secret = true, Text = "test-secret-1" });
        var original = _settings.Current.Snippets[^1];

        _saver.Save(original, SnippetDraft.From(original, "test-secret-1") with { Secret = false, Insert = InsertMode.Type });

        var saved = _settings.Current.Snippets[^1];
        Assert.False(saved.Secret);
        Assert.Equal("test-secret-1", saved.Text);
        Assert.Null(saved.SecretRef);
        Assert.Empty(_secrets.Items);
    }

    /// <summary>Удаление секретного сокращения убирает и его текст из хранилища.</summary>
    [Fact]
    public void Delete_Secret_RemovesKeyFromStore()
    {
        _saver.Save(null, new SnippetDraft { Keys = "sec2 ", Secret = true, Text = "test-secret-1" });
        var original = _settings.Current.Snippets[^1];

        Assert.Equal(SnippetSaveResult.Saved, _saver.Delete(original));

        Assert.DoesNotContain(_settings.Current.Snippets, s => s.Id == original.Id);
        Assert.Empty(_secrets.Items);
    }

    /// <summary>Хранилище не приняло секрет: настройки не менялись.</summary>
    [Fact]
    public void Save_SecretStoreFails_LeavesSettings()
    {
        _secrets.Fail = true;

        var result = _saver.Save(null, new SnippetDraft { Keys = "sec2 ", Secret = true, Text = "test-secret-1" });

        Assert.Equal(SnippetSaveResult.SecretsFailed, result);
        Assert.Single(_settings.Current.Snippets);
    }

    /// <summary>Сбой записи настроек: только что созданный ключ убирается из хранилища.</summary>
    [Fact]
    public void Save_SettingsFail_DropsFreshKey()
    {
        _writer.Fail = true;

        var result = _saver.Save(null, new SnippetDraft { Keys = "sec2 ", Secret = true, Text = "test-secret-1" });

        Assert.Equal(SnippetSaveResult.SettingsFailed, result);
        Assert.Empty(_secrets.Items);
    }
}
