using System.IO.Compression;
using System.Text;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TransferArchive"/>: запись и проверяющее чтение файла <c>.keymend</c> (SPEC F8).</summary>
public sealed class TransferArchiveTests
{
    /// <summary>Настройки для проверок.</summary>
    private static readonly AppSettings _settings = new()
    {
        WordBufferLimit = 300,
        Snippets = [new Snippet { Id = "a-1", Keys = "ll ", Text = "Добрый день!" }],
    };

    /// <summary>Собирает архив с произвольными записями.</summary>
    private static MemoryStream Zip(params (string Name, string Text)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                using var writer = zip.CreateEntry(name).Open();
                writer.Write(Encoding.UTF8.GetBytes(text));
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Записанное читается обратно: настройки, скрипты и секреты.</summary>
    [Fact]
    public void ReadWrite_RoundTrip_KeepsEverything()
    {
        var secrets = TransferCrypto.Encrypt(new Dictionary<string, string> { ["s-1"] = "test-secret-1" }, "test-password-1");
        using var stream = new MemoryStream();
        TransferArchive.Write(stream, _settings, [new ScriptFile("tools/run.cmd", [1, 2, 3])], secrets);
        stream.Position = 0;

        var result = TransferArchive.Read(stream);

        Assert.Equal(TransferError.None, result.Error);
        Assert.Equal(300, result.Package!.Settings.WordBufferLimit);
        Assert.Equal("Добрый день!", result.Package.Settings.Snippets[0].Text);
        Assert.Equal("tools/run.cmd", Assert.Single(result.Package.Scripts).RelativePath);
        Assert.Equal([1, 2, 3], result.Package.Scripts[0].Content);
        Assert.Equal(secrets, result.Package.Secrets);
    }

    /// <summary>Без секретов в архиве нет <c>secrets.json</c>.</summary>
    [Fact]
    public void Write_NoSecrets_HasNoSecretsEntry()
    {
        using var stream = new MemoryStream();
        TransferArchive.Write(stream, _settings, [], null);
        stream.Position = 0;
        using var zip = new ZipArchive(stream);

        Assert.Equal(["settings.json"], zip.Entries.Select(e => e.FullName));
    }

    /// <summary>Небезопасные и лишние имена записей отвергаются целиком.</summary>
    [Theory]
    [InlineData("scripts/../evil.txt")]
    [InlineData("scripts/a/../../evil.txt")]
    [InlineData("../evil.txt")]
    [InlineData("/scripts/x.cmd")]
    [InlineData("C:/evil.txt")]
    [InlineData("scripts/C:evil.txt")]
    [InlineData("scripts/a\\b.cmd")]
    [InlineData("scripts//x.cmd")]
    [InlineData("scripts/x.")]
    [InlineData("Scripts/x.cmd")]
    [InlineData("other.txt")]
    public void Read_UnsafeOrUnexpectedEntry_IsRejected(string name)
    {
        using var stream = Zip(("settings.json", "{}"), (name, "x"));

        var result = TransferArchive.Read(stream);

        Assert.Equal(TransferError.UnexpectedEntry, result.Error);
        Assert.Null(result.Package);
    }

    /// <summary>Не архив — ошибка «не читается».</summary>
    [Fact]
    public void Read_NotAZip_IsUnreadable()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not a zip file, just some text"));

        Assert.Equal(TransferError.Unreadable, TransferArchive.Read(stream).Error);
    }

    /// <summary>Без настроек архив бесполезен.</summary>
    [Fact]
    public void Read_NoSettings_IsMissingSettings()
    {
        using var stream = Zip(("scripts/a.cmd", "x"));

        Assert.Equal(TransferError.MissingSettings, TransferArchive.Read(stream).Error);
    }

    /// <summary>Битый JSON настроек и более новая схема различаются.</summary>
    [Fact]
    public void Read_BrokenOrNewerSettings_ReportsKind()
    {
        using var broken = Zip(("settings.json", "{ broken"));
        using var newer = Zip(("settings.json", """{ "schemaVersion": 99 }"""));

        Assert.Equal(TransferError.BadSettings, TransferArchive.Read(broken).Error);
        Assert.Equal(TransferError.NewerVersion, TransferArchive.Read(newer).Error);
    }

    /// <summary>Повреждённый <c>secrets.json</c> отвергается до применения.</summary>
    [Fact]
    public void Read_BrokenSecrets_IsBadSecrets()
    {
        using var stream = Zip(("settings.json", "{}"), ("secrets.json", "{ broken"));

        Assert.Equal(TransferError.BadSecrets, TransferArchive.Read(stream).Error);
    }

    /// <summary>Слишком много записей — отказ.</summary>
    [Fact]
    public void Read_TooManyEntries_IsTooLarge()
    {
        var entries = Enumerable.Range(0, TransferArchive.MaxEntries + 1).Select(i => ($"scripts/{i}.txt", "x")).ToList();
        entries.Add(("settings.json", "{}"));
        using var stream = Zip([.. entries]);

        Assert.Equal(TransferError.TooLarge, TransferArchive.Read(stream).Error);
    }

    /// <summary>Распакованный размер считается по факту: сжатая «бомба» отвергается.</summary>
    [Fact]
    public void Read_UnpackedDataOverLimit_IsTooLarge()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var settings = zip.CreateEntry("settings.json").Open())
            {
                settings.Write("{}"u8);
            }

            using var big = zip.CreateEntry("scripts/big.bin", CompressionLevel.SmallestSize).Open();
            var chunk = new byte[1024 * 1024];
            for (var i = 0; i < 65; i++)
            {
                big.Write(chunk);
            }
        }

        stream.Position = 0;

        Assert.Equal(TransferError.TooLarge, TransferArchive.Read(stream).Error);
    }

    /// <summary>Обычный <c>settings.json</c> читается как файл без скриптов и секретов.</summary>
    [Fact]
    public void ReadSettings_PlainJson_HasNoScriptsOrSecrets()
    {
        var result = TransferArchive.ReadSettings(SettingsJson.Serialize(_settings));

        Assert.Empty(result.Package!.Scripts);
        Assert.Null(result.Package.Secrets);
        Assert.Equal(300, result.Package.Settings.WordBufferLimit);
    }
}
