using System.Buffers.Binary;
using System.IO;

namespace KeyMend.UI;

/// <summary>Одно изображение внутри многоразмерного ICO.</summary>
/// <param name="Width">Ширина в пикселях (0 в заголовке означает 256).</param>
/// <param name="Height">Высота в пикселях (0 в заголовке означает 256).</param>
/// <param name="Offset">Смещение данных изображения от начала файла.</param>
/// <param name="Length">Размер данных изображения в байтах.</param>
internal readonly record struct IconFrame(int Width, int Height, int Offset, int Length);

/// <summary>Разбор ICO и выбор кадра нужного размера (SPEC F10: значки 16–256 px).</summary>
internal static class IconFileParser
{
    /// <summary>Разбирает каталог многоразмерного ICO.</summary>
    /// <param name="file">Байты ICO.</param>
    /// <returns>Кадры в порядке следования в файле.</returns>
    /// <exception cref="InvalidDataException">Формат файла не является ICO или каталог выходит за его границу.</exception>
    public static IReadOnlyList<IconFrame> Parse(ReadOnlySpan<byte> file)
    {
        if (file.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(file) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(file[2..]) != 1)
        {
            throw new InvalidDataException("not an ICO file");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(file[4..]);
        if (count == 0 || file.Length < 6 + count * 16)
        {
            throw new InvalidDataException("ICO directory is incomplete");
        }

        var frames = new List<IconFrame>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = file[(6 + i * 16)..(22 + i * 16)];
            var width = entry[0] == 0 ? 256 : entry[0];
            var height = entry[1] == 0 ? 256 : entry[1];
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
            var offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]));
            if (width < 1 || width > 256 || height < 1 || height > 256 ||
                length < 1 || offset < 0 || offset > file.Length - length)
            {
                throw new InvalidDataException("ICO frame points outside the file");
            }

            frames.Add(new IconFrame(width, height, offset, length));
        }

        return frames;
    }

    /// <summary>Выбирает точный кадр, иначе ближайший больший (масштаб вниз чётче), иначе самый крупный.</summary>
    /// <param name="frames">Кадры из <see cref="Parse"/>.</param>
    /// <param name="size">Желаемый размер в пикселях.</param>
    /// <returns>Подходящий кадр.</returns>
    /// <exception cref="InvalidOperationException">Список кадров пуст.</exception>
    public static IconFrame Select(IReadOnlyList<IconFrame> frames, int size)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("ICO has no frames");
        }

        var best = frames[0];
        foreach (var frame in frames)
        {
            var width = Math.Min(frame.Width, frame.Height);
            var bestWidth = Math.Min(best.Width, best.Height);
            if (width == size)
            {
                return frame;
            }

            if ((width > size && (bestWidth < size || width < bestWidth)) ||
                (width < size && bestWidth < size && width > bestWidth))
            {
                best = frame;
            }
        }

        return best;
    }

    /// <summary>Возвращает байты выбранного изображения.</summary>
    /// <param name="file">Байты ICO.</param>
    /// <param name="frame">Кадр из <see cref="Parse"/>.</param>
    /// <returns>Данные изображения, пригодные для <c>CreateIconFromResourceEx</c>.</returns>
    public static ReadOnlySpan<byte> Image(ReadOnlySpan<byte> file, IconFrame frame) => file.Slice(frame.Offset, frame.Length);
}
