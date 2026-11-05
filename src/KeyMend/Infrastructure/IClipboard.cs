namespace KeyMend.Infrastructure;

/// <summary>Один формат буфера обмена: код формата и сырые байты.</summary>
/// <param name="Format">Код формата (<c>CF_*</c> или зарегистрированный).</param>
/// <param name="Data">Содержимое формата.</param>
internal sealed record ClipboardItem(uint Format, byte[] Data);

/// <summary>
/// Полная копия содержимого буфера обмена в памяти, чтобы вернуть его после временного использования.
/// Хранится только на время операции; ни в лог, ни на диск не попадает.
/// </summary>
/// <param name="Items">Форматы, которые удалось скопировать (без форматов с объектами GDI).</param>
internal sealed record ClipboardSnapshot(IReadOnlyList<ClipboardItem> Items);

/// <summary>
/// Доступ к буферу обмена Windows. Каждая операция открывает буфер, делает работу и закрывает его без <c>await</c> внутри:
/// открытый буфер привязан к потоку. Вызывается из потока функций.
/// </summary>
internal interface IClipboard
{
    /// <summary>Номер изменения буфера: растёт при каждой смене содержимого.</summary>
    uint SequenceNumber { get; }

    /// <summary>Копирует содержимое буфера.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Копия или <c>null</c>, если буфер занят или слишком велик для копирования.</returns>
    Task<ClipboardSnapshot?> SaveAsync(CancellationToken cancellationToken);

    /// <summary>Читает текст Unicode из буфера.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Текст или <c>null</c>, если текста в буфере нет или он недоступен.</returns>
    Task<string?> GetTextAsync(CancellationToken cancellationToken);

    /// <summary>Заменяет содержимое буфера текстом и помечает его «не добавлять в историю и облако».</summary>
    /// <param name="text">Текст.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns><c>true</c>, если текст положен.</returns>
    Task<bool> SetTextAsync(string text, CancellationToken cancellationToken);

    /// <summary>Возвращает в буфер ранее скопированное содержимое; в историю оно не добавляется.</summary>
    /// <param name="snapshot">Копия.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns><c>true</c>, если содержимое возвращено.</returns>
    Task<bool> RestoreAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken);
}
