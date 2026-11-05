using System.Buffers.Binary;
using System.Resources;
using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты многоразмерных ICO и выбора кадра для значка трея (SPEC F10).</summary>
public sealed class IconFileTests
{
    /// <summary>Набор ожидаемых размеров во встроенных ICO.</summary>
    private static readonly int[] _expectedSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    /// <summary>Обычный ICO читается и содержит все кадры от 16 до 256 px.</summary>
    [Fact]
    public void Parse_EmbeddedIcon_ContainsAllExpectedFrames()
    {
        var frames = IconFileParser.Parse(EmbeddedIcon("assets/keymend.ico"));
        Assert.Equal(_expectedSizes, frames.Select(f => f.Width).Order().ToArray());
        Assert.All(frames, frame =>
        {
            Assert.Equal(frame.Width, frame.Height);
            Assert.True(frame.Length > 0);
        });
    }

    /// <summary>Все состояния значка встроены в сборку.</summary>
    [Fact]
    public void EmbeddedIcons_AllStates_AreAvailable()
    {
        foreach (var name in new[]
        {
            "assets/keymend.ico",
            "assets/keymendpaused.ico",
            "assets/keymendadmin.ico",
            "assets/keymendpausedadmin.ico",
        })
        {
            var frames = IconFileParser.Parse(EmbeddedIcon(name));
            Assert.Contains(frames, frame => frame.Width == 16);
            Assert.Contains(frames, frame => frame.Width == 256);
        }
    }

    /// <summary>Правильный каталог разбирается, нулевой байт размера означает 256 px.</summary>
    [Fact]
    public void Parse_ValidDirectory_ReadsFrames()
    {
        var frames = IconFileParser.Parse(SampleIcon());
        Assert.Equal([16, 32, 256], frames.Select(f => f.Width).ToArray());
        Assert.Equal(new IconFrame(256, 256, 70, 4), frames[2]);
    }

    /// <summary>Не-ICO и пустой каталог отклоняются.</summary>
    [Fact]
    public void Parse_InvalidData_Throws()
    {
        Assert.Throws<InvalidDataException>(() => IconFileParser.Parse([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => IconFileParser.Parse(SampleIcon(count: 0)));
    }

    /// <summary>Точный кадр выбирается предпочтительнее большего.</summary>
    [Fact]
    public void Select_ExactSize_ReturnsExactFrame()
    {
        var frames = IconFileParser.Parse(SampleIcon());
        Assert.Equal(32, IconFileParser.Select(frames, 32).Width);
    }

    /// <summary>Между кадрами берётся ближайший больший, а не уменьшенный более крупный.</summary>
    [Fact]
    public void Select_BetweenFrames_ReturnsNextLargerFrame()
    {
        var frames = IconFileParser.Parse(SampleIcon());
        Assert.Equal(32, IconFileParser.Select(frames, 20).Width);
        Assert.Equal(256, IconFileParser.Select(frames, 200).Width);
        Assert.Equal(256, IconFileParser.Select(frames, 300).Width);
    }

    /// <summary>Без кадров выбор не может вернуть значение.</summary>
    [Fact]
    public void Select_EmptyFrames_Throws() =>
        Assert.Throws<InvalidOperationException>(() => IconFileParser.Select([], 16));

    /// <summary>Читает ICO из скомпилированных ресурсов WPF-сборки, без обращения к диску.</summary>
    /// <param name="name">Имя ресурса в нижнем регистре.</param>
    /// <returns>Байты ICO.</returns>
    private static byte[] EmbeddedIcon(string name)
    {
        using var resources = typeof(AppIcons).Assembly.GetManifestResourceStream("KeyMend.g.resources")!;
        using var reader = new ResourceReader(resources);
        foreach (System.Collections.DictionaryEntry entry in reader)
        {
            if (entry.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                using var icon = Assert.IsAssignableFrom<Stream>(entry.Value);
                using var memory = new MemoryStream();
                icon.CopyTo(memory);
                return memory.ToArray();
            }
        }

        throw new InvalidOperationException($"embedded resource '{name}' was not found");
    }

    /// <summary>Создаёт минимальный трёхкадровый ICO с кадрами 16, 32 и 256 px.</summary>
    /// <param name="count">Число записей в каталоге.</param>
    /// <returns>Байты ICO.</returns>
    private static byte[] SampleIcon(ushort count = 3)
    {
        var data = new byte[74];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), count);

        WriteEntry(data, 0, 16, 16, 54, 4);
        WriteEntry(data, 1, 32, 32, 58, 4);
        WriteEntry(data, 2, 0, 0, 70, 4);
        return data;
    }

    /// <summary>Записывает одну запись каталога ICO.</summary>
    /// <param name="data">Буфер ICO.</param>
    /// <param name="index">Номер записи.</param>
    /// <param name="width">Байт ширины; 0 означает 256.</param>
    /// <param name="height">Байт высоты; 0 означает 256.</param>
    /// <param name="offset">Смещение данных.</param>
    /// <param name="length">Размер данных.</param>
    private static void WriteEntry(byte[] data, int index, byte width, byte height, int offset, int length)
    {
        var entry = data.AsSpan(6 + index * 16, 16);
        entry[0] = width;
        entry[1] = height;
        BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)length);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], (uint)offset);
    }
}
