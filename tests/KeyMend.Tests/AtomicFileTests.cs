using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="AtomicFile"/> на временной папке.</summary>
public sealed class AtomicFileTests : IDisposable
{
    /// <summary>Временная папка, уникальная для каждого теста.</summary>
    private readonly string _dir = Directory.CreateTempSubdirectory("KeyMendTests_").FullName;

    /// <summary>Удаляет временную папку вместе с файлами, снимая «только чтение».</summary>
    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_dir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Содержимое попадает в целевой файл, временный не остаётся.</summary>
    [Fact]
    public void Write_NewFile_WritesContentAndLeavesNoTemp()
    {
        var path = Path.Combine(_dir, "a.bin");

        AtomicFile.Write(path, [1, 2, 3]);

        Assert.Equal<byte[]>([1, 2, 3], File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>Целевой файл только для чтения: запись падает, прежнее содержимое цело, временный файл убран.</summary>
    [Fact]
    public void Write_TargetReadOnly_ThrowsAndRemovesTemp()
    {
        var path = Path.Combine(_dir, "b.bin");
        File.WriteAllBytes(path, [9]);
        File.SetAttributes(path, FileAttributes.ReadOnly);

        Assert.ThrowsAny<Exception>(() => AtomicFile.Write(path, [1, 2, 3]));

        Assert.Equal<byte[]>([9], File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
