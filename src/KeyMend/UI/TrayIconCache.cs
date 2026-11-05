using System.Runtime.InteropServices;
using System.Windows;
using KeyMend.Infrastructure;
using KeyMend.Native;

namespace KeyMend.UI;

/// <summary>
/// Создаёт и кэширует HICON встроенных вариантов иконки трея для текущего состояния и DPI. Кэш живёт не дольше
/// значка трея: освобождать дескриптор до <c>NIM_DELETE</c> нельзя.
/// </summary>
internal sealed class TrayIconCache : IDisposable
{
    /// <summary>Ключ кэша: вариант и фактический размер малого значка.</summary>
    private readonly record struct IconKey(bool Paused, bool Admin, int Size);

    /// <summary>Журнал: ошибку загрузки видно и без значка в трее.</summary>
    private readonly ILog _log;

    /// <summary>Дескрипторы, переданные оболочке; повторно не создаются.</summary>
    private readonly Dictionary<IconKey, SafeIconHandle> _icons = [];

    /// <summary>Создаёт кэш.</summary>
    /// <param name="log">Журнал.</param>
    public TrayIconCache(ILog log)
    {
        _log = log;
    }

    /// <summary>Возвращает HICON варианта нужного размера; при сбое загрузки возвращает ноль.</summary>
    /// <param name="paused">KeyMend на паузе.</param>
    /// <param name="elevated">Программа работает от администратора.</param>
    /// <param name="size">Фактический размер малого значка.</param>
    /// <returns>Дескриптор из кэша или 0 для запасного системного значка.</returns>
    public nint Handle(bool paused, bool elevated, int size)
    {
        var key = new IconKey(paused, elevated, size);
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached.DangerousGetHandle();
        }

        SafeIconHandle? owned = null;
        try
        {
            owned = Create(AppIcons.File(paused, elevated), size);
            if (owned is null || owned.IsInvalid)
            {
                owned?.SetHandleAsInvalid();
                return 0;
            }

            _icons[key] = owned;
            owned = null;
            return _icons[key].DangerousGetHandle();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Error("could not load embedded tray icon", ex);
            return 0;
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>Освобождает кэш; вызывать только после удаления значка из области уведомлений.</summary>
    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
    }

    /// <summary>Читает встроенный ICO, выбирает кадр под DPI и создаёт HICON из данных кадра.</summary>
    /// <param name="file">Имя ресурса.</param>
    /// <param name="size">Фактический размер малого значка.</param>
    /// <returns>Созданный дескриптор или <c>null</c>, если ресурс не удалось прочитать.</returns>
    private SafeIconHandle? Create(string file, int size)
    {
        using var stream = Application.GetResourceStream(AppIcons.ResourceUri(file))?.Stream;
        if (stream is null)
        {
            _log.Error($"embedded icon '{file}' was not found");
            return null;
        }

        var bytes = new byte[stream.Length];
        var read = stream.Read(bytes, 0, bytes.Length);
        if (read != bytes.Length)
        {
            _log.Error($"embedded icon '{file}' was not read completely");
            return null;
        }

        var image = IconFileParser.Image(bytes, IconFileParser.Select(IconFileParser.Parse(bytes), size));
        var handle = CreateHandle(image, size);
        if (handle.IsInvalid)
        {
            _log.Error($"could not create tray icon from '{file}' ({size}px)");
        }

        return handle;
    }

    /// <summary>Копирует кадр в выровненную нативную память и передаёт её WinAPI.</summary>
    /// <param name="image">Данные кадра ICO.</param>
    /// <param name="size">Фактический размер значка.</param>
    /// <returns>Дескриптор до проверки на ошибку.</returns>
    private static unsafe SafeIconHandle CreateHandle(ReadOnlySpan<byte> image, int size)
    {
        var bits = (byte*)NativeMemory.Alloc((nuint)image.Length);
        try
        {
            image.CopyTo(new Span<byte>(bits, image.Length));
            return new SafeIconHandle(User32.CreateIconFromResourceEx(
                bits,
                (uint)image.Length,
                fIcon: true,
                Win32.IconResourceVersion,
                size,
                size,
                Win32.LrDefaultColor));
        }
        finally
        {
            NativeMemory.Free(bits);
        }
    }
}
