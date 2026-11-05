using KeyMend.Storage.Transfer;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TransferMessages"/>: тексты итогов раздела «Перенос».</summary>
public sealed class TransferMessagesTests
{
    /// <summary>Каждая ошибка файла и каждая причина отказа имеют своё непустое сообщение.</summary>
    [Fact]
    public void ForErrorAndForFailure_AllValues_GiveText()
    {
        foreach (var error in Enum.GetValues<TransferError>().Where(e => e != TransferError.None))
        {
            Assert.False(string.IsNullOrWhiteSpace(TransferMessages.ForError(error)));
        }

        foreach (var failure in Enum.GetValues<ImportFailure>().Where(f => f != ImportFailure.None))
        {
            Assert.False(string.IsNullOrWhiteSpace(TransferMessages.ForFailure(failure)));
        }

        Assert.NotEqual(TransferMessages.ForError(TransferError.NewerVersion), TransferMessages.ForError(TransferError.BadSettings));
    }

    /// <summary>Итог объединения называет добавленное, а при пропусках — и остальное.</summary>
    [Fact]
    public void Summary_MergeWithSkips_ListsEverything()
    {
        var result = new ImportResult(ImportFailure.None, new ImportReport(2, 3, 1, 4, 5, 6, 1), ["run.cmd"], ["Монитор"]);

        var lines = TransferMessages.Summary(result, ImportMode.Merge);

        Assert.Equal(5, lines.Count);
        Assert.Contains("2", lines[0], StringComparison.Ordinal);
        Assert.Contains("run.cmd", lines[3], StringComparison.Ordinal);
        Assert.Contains("Монитор", lines[4], StringComparison.Ordinal);
    }

    /// <summary>Восстановление без отчёта даёт одну строку.</summary>
    [Fact]
    public void Summary_Restore_IsOneLine() =>
        Assert.Single(TransferMessages.Summary(new ImportResult(ImportFailure.None, null, [], []), ImportMode.Replace));
}
