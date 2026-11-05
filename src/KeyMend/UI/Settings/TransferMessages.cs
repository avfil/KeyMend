using KeyMend.Resources;
using KeyMend.Storage.Transfer;

namespace KeyMend.UI.Settings;

/// <summary>Тексты раздела «Перенос» по итогам операций: чистая логика без окна, чтобы проверяться тестами.</summary>
internal static class TransferMessages
{
    /// <summary>Сообщение о файле, который не удалось открыть.</summary>
    /// <param name="error">Вид ошибки.</param>
    /// <returns>Текст для владельца.</returns>
    public static string ForError(TransferError error) => error switch
    {
        TransferError.MissingSettings => Strings.TransferErrMissingSettings,
        TransferError.BadSettings => Strings.TransferErrBadSettings,
        TransferError.NewerVersion => Strings.TransferErrNewer,
        TransferError.BadSecrets => Strings.TransferErrBadSecrets,
        TransferError.UnexpectedEntry => Strings.TransferErrUnexpected,
        TransferError.TooLarge => Strings.TransferErrTooLarge,
        _ => Strings.TransferErrUnreadable,
    };

    /// <summary>Сообщение о неудачной загрузке или восстановлении.</summary>
    /// <param name="failure">Причина.</param>
    /// <returns>Текст для владельца.</returns>
    public static string ForFailure(ImportFailure failure) => failure switch
    {
        ImportFailure.WrongPassword => Strings.TransferFailWrongPassword,
        ImportFailure.BackupFailed => Strings.TransferFailBackup,
        ImportFailure.SecretsFailed => Strings.TransferFailSecrets,
        ImportFailure.ScriptsFailed => Strings.TransferFailScripts,
        ImportFailure.BadBackup => Strings.TransferFailBadBackup,
        _ => Strings.TransferFailSettings,
    };

    /// <summary>Сообщение об экспорте.</summary>
    /// <param name="result">Итог экспорта.</param>
    /// <returns>Текст для владельца.</returns>
    public static string ForExport(ExportResult result) => result switch
    {
        ExportResult.Saved => Strings.TransferExportDone,
        ExportResult.TooLarge => Strings.TransferExportTooLarge,
        _ => Strings.TransferExportFailed,
    };

    /// <summary>Строки итога удачной загрузки; строки без сообщаемого пропускаются.</summary>
    /// <param name="result">Итог загрузки.</param>
    /// <param name="mode">Как загружали.</param>
    /// <returns>Строки по порядку.</returns>
    public static IReadOnlyList<string> Summary(ImportResult result, ImportMode mode)
    {
        var lines = new List<string>();
        if (result.Report is not { } report)
        {
            lines.Add(Strings.TransferRestoreDone);
        }
        else
        {
            var template = mode == ImportMode.Replace ? Strings.TransferDoneLoaded : Strings.TransferDoneAdded;
            lines.Add(Strings.Fill(template, Number(report.Rules), Number(report.Snippets), Number(report.Exclusions)));
            if (mode == ImportMode.Merge && (report.Replaced + report.KeptCurrent + report.Identical) > 0)
            {
                lines.Add(Strings.Fill(Strings.TransferDoneReplaced, Number(report.Replaced), Number(report.KeptCurrent), Number(report.Identical)));
            }

            if (report.SnippetsWithoutSecret > 0)
            {
                lines.Add(Strings.Fill(Strings.TransferDoneNoSecret, Number(report.SnippetsWithoutSecret)));
            }
        }

        if (result.SkippedScripts.Count > 0)
        {
            lines.Add(Strings.Fill(Strings.TransferDoneSkippedScripts, string.Join(", ", result.SkippedScripts)));
        }

        if (result.MissingPrograms.Count > 0)
        {
            lines.Add(Strings.Fill(Strings.TransferDoneMissing, string.Join(", ", result.MissingPrograms)));
        }

        return lines;
    }

    /// <summary>Описание конфликта одной строкой.</summary>
    /// <param name="conflict">Конфликт.</param>
    /// <returns>Текст строки списка конфликтов.</returns>
    public static string ForConflict(ImportConflict conflict)
    {
        var current = string.Join(", ", conflict.CurrentNames);
        return conflict.Key.Kind switch
        {
            ConflictKind.Rule => Strings.Fill(Strings.TransferConflictRule, conflict.ImportedName, current),
            ConflictKind.Snippet => Strings.Fill(Strings.TransferConflictSnippet, conflict.ImportedName, current),
            _ => Strings.Fill(Strings.TransferConflictExclusion, conflict.ImportedName),
        };
    }

    /// <summary>Подпись «правил: N, сокращений: M, исключений: K» для выбранного файла.</summary>
    /// <param name="preview">Открытый файл.</param>
    /// <returns>Текст строки.</returns>
    public static string FileSummary(ImportPreview preview) => Strings.Fill(
        Strings.TransferFileSummary,
        Number(preview.Package.Settings.Rules.Count),
        Number(preview.Package.Settings.Snippets.Count),
        Number(preview.Package.Settings.Exclusions.Count));

    /// <summary>Число для подстановки в текст.</summary>
    /// <param name="value">Число.</param>
    /// <returns>Строка без разделителей групп.</returns>
    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
