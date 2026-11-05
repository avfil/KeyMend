using System.Text;
using KeyMend.Native;

namespace KeyMend.Infrastructure;

/// <summary>
/// Буфер обмена через WinAPI. Окно-владелец обязательно: при <c>OpenClipboard(NULL)</c> вызов <c>EmptyClipboard</c> делает
/// владельца пустым, и <c>SetClipboardData</c> перестаёт работать (документация Win32). Содержимое буфера никуда не
/// пишется, в лог идут только размеры и счётчики.
/// </summary>
internal sealed class Win32Clipboard : IClipboard
{
    /// <summary>Сколько раз пробовать открыть буфер, если его держит другое окно.</summary>
    internal const int OpenAttempts = 5;

    /// <summary>Пауза между попытками открыть буфер.</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Предел размера копии буфера. Больше — копировать не берёмся и ничего не исправляем: потерять чужое содержимое
    /// хуже, чем отказаться.
    /// </summary>
    internal const long MaxSnapshotBytes = 64L * 1024 * 1024;

    /// <summary>Окно, которому принадлежит открытый буфер.</summary>
    private readonly nint _owner;

    /// <summary>Источник времени для пауз между попытками.</summary>
    private readonly TimeProvider _time;

    /// <summary>Журнал.</summary>
    private readonly ILog _log;

    /// <summary>Код формата «не обрабатывать содержимое монитором буфера» (история, облако).</summary>
    private readonly uint _excludeFormat = User32.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");

    /// <summary>Код формата «можно ли включать в историю» (значение 0 — нельзя).</summary>
    private readonly uint _historyFormat = User32.RegisterClipboardFormat("CanIncludeInClipboardHistory");

    /// <summary>Код формата «можно ли отправлять в облачный буфер» (значение 0 — нельзя).</summary>
    private readonly uint _cloudFormat = User32.RegisterClipboardFormat("CanUploadToCloudClipboard");

    /// <summary>Создаёт доступ к буферу обмена.</summary>
    /// <param name="owner">Окно-владелец, например скрытое окно значка в трее.</param>
    /// <param name="time">Источник времени.</param>
    /// <param name="log">Журнал.</param>
    public Win32Clipboard(nint owner, TimeProvider time, ILog log)
    {
        _owner = owner;
        _time = time;
        _log = log;
    }

    /// <inheritdoc />
    public uint SequenceNumber => User32.GetClipboardSequenceNumber();

    /// <inheritdoc />
    public Task<ClipboardSnapshot?> SaveAsync(CancellationToken cancellationToken) =>
        WithClipboardAsync(ReadAll, null, cancellationToken);

    /// <inheritdoc />
    public Task<string?> GetTextAsync(CancellationToken cancellationToken) =>
        WithClipboardAsync(ReadText, null, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken) =>
        WithClipboardAsync(() => WriteText(text), false, cancellationToken);

    /// <inheritdoc />
    public Task<bool> RestoreAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken) =>
        WithClipboardAsync(() => WriteAll(snapshot), false, cancellationToken);

    /// <summary>Можно ли копировать формат: форматы с объектами GDI и «отображения» не передаются как память.</summary>
    /// <param name="format">Код формата.</param>
    /// <param name="hasUnicodeText">В буфере есть <c>CF_UNICODETEXT</c>: тогда <c>CF_TEXT</c> и <c>CF_OEMTEXT</c> — лишь его синтез.</param>
    /// <returns><c>true</c>, если формат надо пропустить.</returns>
    internal static bool ShouldSkip(uint format, bool hasUnicodeText) =>
        format is Win32.CfBitmap or Win32.CfMetafilePict or Win32.CfPalette or Win32.CfEnhMetafile or Win32.CfOwnerDisplay
        || format is >= Win32.CfDspFirst and <= Win32.CfDspLast
        || format is >= Win32.CfPrivateFirst and <= Win32.CfGdiObjLast
        || (hasUnicodeText && format is Win32.CfText or Win32.CfOemText);

    /// <summary>
    /// Открывает буфер (с повторами, пока его держит другое окно), выполняет работу синхронно и закрывает. Между
    /// <c>OpenClipboard</c> и <c>CloseClipboard</c> нет <c>await</c>: открытый буфер привязан к потоку.
    /// </summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <param name="work">Работа с открытым буфером.</param>
    /// <param name="failed">Результат, если буфер открыть не удалось.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Результат работы или <paramref name="failed"/>.</returns>
    private async Task<T> WithClipboardAsync<T>(Func<T> work, T failed, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (User32.OpenClipboard(_owner))
            {
                try
                {
                    return work();
                }
                finally
                {
                    User32.CloseClipboard();
                }
            }

            await Task.Delay(RetryDelay, _time, cancellationToken);
        }

        _log.Info("clipboard is busy");
        return failed;
    }

    /// <summary>Копирует все форматы буфера, кроме пропускаемых.</summary>
    /// <returns>Копия или <c>null</c>, если она больше предела.</returns>
    private ClipboardSnapshot? ReadAll()
    {
        var formats = new List<uint>();
        for (var format = User32.EnumClipboardFormats(0); format != 0; format = User32.EnumClipboardFormats(format))
        {
            formats.Add(format);
        }

        var hasUnicodeText = formats.Contains(Win32.CfUnicodeText);
        var items = new List<ClipboardItem>();
        long total = 0;
        foreach (var format in formats)
        {
            if (ShouldSkip(format, hasUnicodeText) || ReadFormat(format) is not { } data)
            {
                continue;
            }

            total += data.Length;
            if (total > MaxSnapshotBytes)
            {
                _log.Info("clipboard is too large to copy");
                return null;
            }

            items.Add(new ClipboardItem(format, data));
        }

        return new ClipboardSnapshot(items);
    }

    /// <summary>Копирует байты одного формата из открытого буфера.</summary>
    /// <param name="format">Код формата.</param>
    /// <returns>Байты или <c>null</c>, если формат не отдал память (например, владелец не смог его отрисовать).</returns>
    private static unsafe byte[]? ReadFormat(uint format)
    {
        var handle = User32.GetClipboardData(format);
        if (handle == 0)
        {
            return null;
        }

        var size = Kernel32.GlobalSize(handle);
        if (size == 0 || size > int.MaxValue)
        {
            return null;
        }

        var pointer = Kernel32.GlobalLock(handle);
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            return new ReadOnlySpan<byte>((void*)pointer, (int)size).ToArray();
        }
        finally
        {
            Kernel32.GlobalUnlock(handle);
        }
    }

    /// <summary>Читает текст Unicode из открытого буфера.</summary>
    /// <returns>Текст без завершающего нуля или <c>null</c>.</returns>
    private static unsafe string? ReadText()
    {
        var handle = User32.GetClipboardData(Win32.CfUnicodeText);
        if (handle == 0)
        {
            return null;
        }

        var size = Kernel32.GlobalSize(handle);
        var pointer = size == 0 ? 0 : Kernel32.GlobalLock(handle);
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            var chars = new ReadOnlySpan<char>((void*)pointer, (int)(size / sizeof(char)));
            var end = chars.IndexOf('\0');
            return new string(end < 0 ? chars : chars[..end]);
        }
        finally
        {
            Kernel32.GlobalUnlock(handle);
        }
    }

    /// <summary>Очищает открытый буфер и кладёт текст Unicode, помеченный «не в историю».</summary>
    /// <param name="text">Текст.</param>
    /// <returns><c>true</c>, если текст положен.</returns>
    private bool WriteText(string text)
    {
        if (!User32.EmptyClipboard())
        {
            return false;
        }

        var ok = SetFormat(Win32.CfUnicodeText, Encoding.Unicode.GetBytes(text + '\0'));
        MarkPrivate();
        return ok;
    }

    /// <summary>Очищает открытый буфер и кладёт в него копию, помеченную «не в историю».</summary>
    /// <param name="snapshot">Копия.</param>
    /// <returns><c>true</c>, если буфер очищен и записан.</returns>
    private bool WriteAll(ClipboardSnapshot snapshot)
    {
        if (!User32.EmptyClipboard())
        {
            return false;
        }

        foreach (var item in snapshot.Items)
        {
            SetFormat(item.Format, item.Data);
        }

        MarkPrivate();
        return true;
    }

    /// <summary>Добавляет форматы, по которым Windows не кладёт содержимое в историю Win+V и облако.</summary>
    private void MarkPrivate()
    {
        if (_excludeFormat != 0)
        {
            SetFormat(_excludeFormat, [1, 0, 0, 0]);
        }

        if (_historyFormat != 0)
        {
            SetFormat(_historyFormat, [0, 0, 0, 0]);
        }

        if (_cloudFormat != 0)
        {
            SetFormat(_cloudFormat, [0, 0, 0, 0]);
        }
    }

    /// <summary>Кладёт байты в буфер в указанном формате; при успехе память переходит системе.</summary>
    /// <param name="format">Код формата.</param>
    /// <param name="data">Байты.</param>
    /// <returns><c>true</c>, если формат принят.</returns>
    private static unsafe bool SetFormat(uint format, ReadOnlySpan<byte> data)
    {
        var handle = Kernel32.GlobalAlloc(Win32.GmemMoveable, (nuint)Math.Max(data.Length, 1));
        if (handle == 0)
        {
            return false;
        }

        var pointer = Kernel32.GlobalLock(handle);
        if (pointer == 0)
        {
            Kernel32.GlobalFree(handle);
            return false;
        }

        data.CopyTo(new Span<byte>((void*)pointer, data.Length));
        Kernel32.GlobalUnlock(handle);
        if (User32.SetClipboardData(format, handle) != 0)
        {
            return true;
        }

        Kernel32.GlobalFree(handle);
        return false;
    }
}
