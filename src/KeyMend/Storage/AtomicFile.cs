using System.IO;

namespace KeyMend.Storage;

/// <summary>Атомарная запись файла: данные пишутся во временный файл рядом и переносятся на место; при сбое временный файл не остаётся.</summary>
internal static class AtomicFile
{
    /// <summary>Записывает содержимое в <paramref name="path"/> через временный файл.</summary>
    /// <param name="path">Конечный файл.</param>
    /// <param name="content">Содержимое.</param>
    /// <param name="tempSuffix">Расширение временного файла, добавляемое к имени.</param>
    /// <exception cref="IOException">Не удалось записать или перенести файл (например, целевой файл только для чтения).</exception>
    /// <exception cref="UnauthorizedAccessException">Нет доступа к файлу или папке.</exception>
    public static void Write(string path, byte[] content, string tempSuffix = ".tmp")
    {
        var tmp = path + tempSuffix;
        try
        {
            File.WriteAllBytes(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Остаток неудачной записи не оставляем: он мусор и при следующей записи только мешает.
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>Удаляет файл, не бросая исключений: уборка не должна заслонять исходную ошибку.</summary>
    /// <param name="path">Файл.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Нечего сделать: файл занят или недоступен, исходная ошибка важнее.
        }
    }
}
