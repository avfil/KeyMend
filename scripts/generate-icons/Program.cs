using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var outputDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "KeyMend", "Assets"));
Directory.CreateDirectory(outputDir);

IconGenerator.Generate(outputDir);
Console.WriteLine($"icons written to {outputDir}");

/// <summary>Генерирует исходный SVG и многоразмерные ICO логотипа KeyMend (SPEC F10).</summary>
internal static class IconGenerator
{
    /// <summary>Размеры кадров: обычные значения малого значка и крупные значки Windows.</summary>
    private static readonly int[] _sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    /// <summary>Цвета утверждённого логотипа (SPEC F10).</summary>
    private static readonly Color _blue = Color.FromRgb(0x60, 0xCD, 0xFF);
    private static readonly Color _red = Color.FromRgb(0xFF, 0x6B, 0x6B);
    private static readonly Color _dark = Color.FromRgb(0x33, 0x33, 0x33);
    private static readonly Color _greyTile = Color.FromRgb(0x7A, 0x7A, 0x7A);
    private static readonly Color _greyCore = Color.FromRgb(0x8F, 0x8F, 0x8F);

    /// <summary>Записывает SVG и ICO во вложенную папку.</summary>
    /// <param name="outputDir">Конечная папка.</param>
    public static void Generate(string outputDir)
    {
        File.WriteAllText(Path.Combine(outputDir, "KeyMend.svg"), Svg(false, false));
        var variants = new (string File, bool Paused, bool Admin)[]
        {
            ("KeyMend.ico", false, false),
            ("KeyMendPaused.ico", true, false),
            ("KeyMendAdmin.ico", false, true),
            ("KeyMendPausedAdmin.ico", true, true),
        };

        foreach (var variant in variants)
        {
            var frames = new Dictionary<int, byte[]>();
            foreach (var size in _sizes)
            {
                var image = size == 16 ? Render16(variant.Paused, variant.Admin) : RenderVector(size, variant.Paused, variant.Admin);
                frames[size] = EncodePng(image);
            }

            File.WriteAllBytes(Path.Combine(outputDir, variant.File), BuildIco(frames));
            Console.WriteLine($"wrote {variant.File}: {string.Join(", ", _sizes.Select(s => $"{s}px"))}");
        }
    }

    /// <summary>Создаёт обычный SVG-мастер логотипа на холсте 64 px.</summary>
    /// <param name="paused">Серый вариант.</param>
    /// <param name="admin">Показывает щит администратора.</param>
    /// <returns>Текст SVG.</returns>
    private static string Svg(bool paused, bool admin)
    {
        var tile = paused ? "#7A7A7A" : "#60CDFF";
        var core = paused ? "#8F8F8F" : "#FF6B6B";
        var shield = admin
            ? """
                  <circle cx="52.8" cy="52.8" r="11.2" fill="#FF6B6B"/>
                  <path d="M52.8 47.2 L57 49.2 V52.1 C57 54.7 55.2 56.6 52.8 57.8 C50.4 56.6 48.6 54.7 48.6 52.1 V49.2 Z" fill="#333333"/>
              """
            : "";
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
              <rect x="0" y="0" width="64" height="64" rx="12.8" fill="{tile}"/>
              <g transform="scale(2)" stroke="white" stroke-width="4" stroke-linecap="round" fill="none">
                <path d="M12.5 5.5 V9 M16 5.5 V9 M19.5 5.5 V9 M12.5 23 V26.5 M16 23 V26.5 M19.5 23 V26.5 M5.5 12.5 H9 M5.5 16 H9 M5.5 19.5 H9 M23 12.5 H26.5 M23 16 H26.5 M23 19.5 H26.5"/>
              </g>
              <rect x="14.4" y="14.4" width="28" height="28" rx="4" fill="white"/>
              <rect x="20" y="20" width="14" height="14" rx="1.9" fill="{core}"/>
              {shield}
            </svg>
            """;
    }

    /// <summary>Растрирует векторный вариант указанного размера; 16 px рисуется отдельной пиксельной сеткой.</summary>
    /// <param name="size">Размер кадра в пикселях.</param>
    /// <param name="paused">Серый вариант.</param>
    /// <param name="admin">Показывает щит администратора.</param>
    /// <returns>Готовое изображение.</returns>
    private static RenderTargetBitmap RenderVector(int size, bool paused, bool admin)
    {
        var unit = size / 32d;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var tileBrush = new SolidColorBrush(paused ? _greyTile : _blue);
            var coreBrush = new SolidColorBrush(paused ? _greyCore : _red);
            var pen = new Pen(Brushes.White, Math.Max(1, size / 16d))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };

            dc.DrawRoundedRectangle(tileBrush, null, new Rect(0, 0, size, size), size / 4d, size / 4d);
            foreach (var pin in Pins(size))
            {
                dc.DrawLine(pen, pin.Start, pin.End);
            }

            dc.DrawRoundedRectangle(
                Brushes.White,
                null,
                new Rect(9 * unit, 9 * unit, 14 * unit, 14 * unit),
                2.5 * unit,
                2.5 * unit);
            dc.DrawRoundedRectangle(
                coreBrush,
                null,
                new Rect(12.5 * unit, 12.5 * unit, 7 * unit, 7 * unit),
                1.2 * unit,
                1.2 * unit);

            if (admin)
            {
                var shieldScale = size / 40d;
                var redBrush = new SolidColorBrush(_red);
                var darkBrush = new SolidColorBrush(_dark);
                dc.DrawEllipse(redBrush, null, new Point(33 * shieldScale, 33 * shieldScale), 7 * shieldScale, 7 * shieldScale);
                var shield = StreamGeometry.Parse("M33,29.5 L35.6,30.5 V32.3 C35.6,33.9 34.5,35.1 33,35.8 C31.5,35.1 30.4,33.9 30.4,32.3 V30.5 Z");
                dc.DrawGeometry(darkBrush, null, shield);
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Рисует 16 px вручную без сглаживания: прямоугольники и линии ложатся на целые пиксели.</summary>
    /// <param name="paused">Серый вариант.</param>
    /// <param name="admin">Показывает щит администратора.</param>
    /// <returns>Готовое изображение.</returns>
    private static BitmapSource Render16(bool paused, bool admin)
    {
        const int size = 16;
        var pixels = new byte[size * size * 4];
        var tile = paused ? _greyTile : _blue;
        var core = paused ? _greyCore : _red;

        // Плитка занимает 14×14 с невидимыми углами: так значок хорошо отделяется от панели задач.
        for (var y = 1; y <= 14; y++)
        {
            for (var x = 1; x <= 14; x++)
            {
                var roundedCorner = (y is 1 or 14 && x <= 2 || y is 1 or 14 && x >= 13) ||
                                    (y is 2 or 13 && (x == 1 || x == 14));
                if (!roundedCorner)
                {
                    SetPixel(pixels, x, y, tile);
                }
            }
        }

        var white = Color.FromRgb(255, 255, 255);
        foreach (var x in new[] { 5, 8, 11 })
        {
            SetRect(pixels, x, 3, x, 4, white);
            SetRect(pixels, x, 11, x, 12, white);
        }

        foreach (var y in new[] { 5, 8, 11 })
        {
            SetRect(pixels, 3, y, 4, y, white);
            SetRect(pixels, 11, y, 12, y, white);
        }

        SetRect(pixels, 5, 5, 10, 10, white);
        SetRect(pixels, 7, 7, 8, 8, core);

        if (admin)
        {
            for (var y = 10; y <= 15; y++)
            {
                for (var x = 10; x <= 15; x++)
                {
                    var dx = x - 12.5;
                    var dy = y - 12.5;
                    if (dx * dx + dy * dy <= 10)
                    {
                        SetPixel(pixels, x, y, _red);
                    }
                }
            }

            foreach (var (x, y) in new[] { (12, 11), (11, 12), (12, 13), (13, 12), (12, 14) })
            {
                SetPixel(pixels, x, y, _dark);
            }
        }

        var source = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        source.Freeze();
        return source;
    }

    /// <summary>Возвращает белые выводы микросхемы в координатах из векторного логотипа.</summary>
    /// <param name="size">Размер кадра.</param>
    /// <returns>Начало и конец каждой линии.</returns>
    private static List<(Point Start, Point End)> Pins(int size)
    {
        var u = size / 32d;
        Point P(double x, double y) => new(x * u, y * u);
        List<(Point Start, Point End)> pins = [];
        foreach (var x in new[] { 12.5d, 16d, 19.5d })
        {
            pins.Add((P(x, 5.5), P(x, 9)));
            pins.Add((P(x, 23), P(x, 26.5)));
        }

        foreach (var y in new[] { 12.5d, 16d, 19.5d })
        {
            pins.Add((P(5.5, y), P(9, y)));
            pins.Add((P(23, y), P(26.5, y)));
        }

        return pins;
    }

    /// <summary>Записывает один цветной пиксель в буфер BGRA.</summary>
    /// <param name="pixels">Буфер изображения.</param>
    /// <param name="x">Горизонтальная координата.</param>
    /// <param name="y">Вертикальная координата.</param>
    /// <param name="color">Цвет.</param>
    private static void SetPixel(byte[] pixels, int x, int y, Color color)
    {
        var offset = ((y * 16) + x) * 4;
        pixels[offset] = color.B;
        pixels[offset + 1] = color.G;
        pixels[offset + 2] = color.R;
        pixels[offset + 3] = 255;
    }

    /// <summary>Записывает заполненный прямоугольник включительно по краям.</summary>
    /// <param name="pixels">Буфер изображения.</param>
    /// <param name="left">Левый край.</param>
    /// <param name="top">Верхний край.</param>
    /// <param name="right">Правый край.</param>
    /// <param name="bottom">Нижний край.</param>
    /// <param name="color">Цвет.</param>
    private static void SetRect(byte[] pixels, int left, int top, int right, int bottom, Color color)
    {
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                SetPixel(pixels, x, y, color);
            }
        }
    }

    /// <summary>Кодирует изображение в PNG: ICO принимает PNG-кадры, поэтому отдельный BMP-код не нужен.</summary>
    /// <param name="image">Изображение.</param>
    /// <returns>PNG-данные.</returns>
    private static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Собирает ICO-файл из PNG-кадров: заголовок, каталог изображений и данные кадров.</summary>
    /// <param name="frames">PNG по размеру кадра.</param>
    /// <returns>Байты ICO.</returns>
    private static byte[] BuildIco(IReadOnlyDictionary<int, byte[]> frames)
    {
        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)frames.Count);

        var offset = 6 + frames.Count * 16;
        foreach (var (size, png) in frames)
        {
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)png.Length);
            writer.Write((uint)offset);
            offset += png.Length;
        }

        foreach (var png in frames.Values)
        {
            writer.Write(png);
        }

        return result.ToArray();
    }
}
