using KeyMend.Input;

namespace KeyMend.Infrastructure;

/// <summary>Чем закончилась замена выделенного текста через буфер обмена.</summary>
internal enum ReplaceResult
{
    /// <summary>Результат вставлен.</summary>
    Replaced,

    /// <summary>Ничего не выделено: буфер обмена не менялся.</summary>
    NothingSelected,

    /// <summary>Текст скопирован, но не подошёл (пустой, слишком длинный, нечего исправлять): выделение осталось на месте.</summary>
    Rejected,

    /// <summary>Не вышло: буфер обмена недоступен, система не приняла ввод или не удалось положить текст.</summary>
    Failed,
}

/// <summary>Замена выделенного текста через буфер обмена с возвратом прежнего содержимого буфера (SPEC F3, F5.3).</summary>
internal interface IClipboardGuard
{
    /// <summary>
    /// Копирует выделенное (Ctrl+C), передаёт текст в <paramref name="convert"/>, вставляет результат (Ctrl+V) и возвращает
    /// в буфер прежнее содержимое.
    /// </summary>
    /// <param name="convert">Превращает выделенный текст в новый; <c>null</c> — ничего не вставлять.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Итог замены.</returns>
    Task<ReplaceResult> ReplaceSelectionAsync(Func<string, string?> convert, CancellationToken cancellationToken);

    /// <summary>
    /// Вставляет <paramref name="text"/> через буфер обмена (Ctrl+V) и возвращает прежнее содержимое буфера. Текст в буфере
    /// скрыт от истории Win+V и облака.
    /// </summary>
    /// <param name="text">Что вставить.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns><c>true</c>, если вставка отправлена.</returns>
    Task<bool> PasteTextAsync(string text, CancellationToken cancellationToken);
}

/// <summary>
/// Порядок работы: сохранить буфер → Ctrl+C → дождаться смены номера буфера (до 500 мс) → прочитать текст → положить результат →
/// Ctrl+V → подождать 300 мс → восстановить буфер. Если номер не сменился, ничего не выделено: буфер не тронут и не
/// восстанавливается. Восстановление идёт в <c>finally</c> и не отменяется. Содержимое не пишется в лог.
/// </summary>
/// <param name="clipboard">Буфер обмена.</param>
/// <param name="sender">Отправка ввода.</param>
/// <param name="time">Источник времени и таймеров.</param>
/// <param name="log">Журнал.</param>
internal sealed class ClipboardGuard(IClipboard clipboard, IInputSender sender, TimeProvider time, ILog log) : IClipboardGuard
{
    /// <summary>Сколько ждать, пока приложение положит выделенное в буфер.</summary>
    internal static readonly TimeSpan CopyTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>Как часто сверять номер изменения буфера.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Сколько ждать после Ctrl+V, прежде чем вернуть прежний буфер: приложение читает его не мгновенно.</summary>
    internal static readonly TimeSpan PasteDelay = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc />
    public async Task<ReplaceResult> ReplaceSelectionAsync(Func<string, string?> convert, CancellationToken cancellationToken)
    {
        var snapshot = await clipboard.SaveAsync(cancellationToken);
        if (snapshot is null)
        {
            log.Info("selection skipped: clipboard unavailable");
            return ReplaceResult.Failed;
        }

        var before = clipboard.SequenceNumber;
        if (!sender.SendChord(VirtualKey.LControl, VirtualKey.C))
        {
            return ReplaceResult.Failed;
        }

        // После принятого Ctrl+C буфер мог уже измениться: снимок возвращаем при любой отмене или сбое, кроме точно пустого результата копирования.
        var restoreClipboard = true;
        try
        {
            if (!await WaitForChangeAsync(before, cancellationToken))
            {
                log.Info("selection skipped: nothing selected");
                restoreClipboard = false;
                return ReplaceResult.NothingSelected;
            }

            var text = await clipboard.GetTextAsync(cancellationToken);
            var converted = text is null ? null : convert(text);
            if (converted is null)
            {
                log.Info("selection skipped: text is not convertible");
                return ReplaceResult.Rejected;
            }

            if (!await clipboard.SetTextAsync(converted, cancellationToken) || !sender.SendChord(VirtualKey.LControl, VirtualKey.V))
            {
                return ReplaceResult.Failed;
            }

            await Task.Delay(PasteDelay, time, cancellationToken);
            return ReplaceResult.Replaced;
        }
        finally
        {
            // Восстановление не отменяется: иначе при выходе из программы пользователь остался бы без своего буфера.
            if (restoreClipboard && !await clipboard.RestoreAsync(snapshot, CancellationToken.None))
            {
                log.Error("clipboard was not restored");
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> PasteTextAsync(string text, CancellationToken cancellationToken)
    {
        var snapshot = await clipboard.SaveAsync(cancellationToken);
        if (snapshot is null)
        {
            log.Info("paste skipped: clipboard unavailable");
            return false;
        }

        try
        {
            if (!await clipboard.SetTextAsync(text, cancellationToken) || !sender.SendChord(VirtualKey.LControl, VirtualKey.V))
            {
                return false;
            }

            await Task.Delay(PasteDelay, time, cancellationToken);
            return true;
        }
        finally
        {
            if (!await clipboard.RestoreAsync(snapshot, CancellationToken.None))
            {
                log.Error("clipboard was not restored");
            }
        }
    }

    /// <summary>Ждёт, пока номер изменения буфера станет другим.</summary>
    /// <param name="before">Номер до Ctrl+C.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns><c>true</c>, если буфер изменился за <see cref="CopyTimeout"/>.</returns>
    private async Task<bool> WaitForChangeAsync(uint before, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow() + CopyTimeout;
        while (clipboard.SequenceNumber == before)
        {
            if (time.GetUtcNow() >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, time, cancellationToken);
        }

        return true;
    }
}
