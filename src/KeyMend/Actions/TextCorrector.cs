using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.Actions;

/// <summary>Исправление текста, набранного не в той раскладке (SPEC F3). Вызывается из потока функций.</summary>
internal interface ITextCorrector
{
    /// <summary>Исправляет выделенный текст или текст от каретки до начала строки (по <see cref="CorrectStep.Scope"/>).</summary>
    /// <param name="step">Шаг исправления: область, пара раскладок, лимит длины, переключение языка.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача исправления.</returns>
    Task CorrectAsync(CorrectStep step, CancellationToken cancellationToken);
}

/// <summary>
/// Исправляет текст, который уже есть в окне: выделенный или от каретки до начала строки. Выделение делает UI Automation,
/// а если оно недоступно, то клавиши (Shift+Home). Сам текст берётся и заменяется через <see cref="IClipboardGuard"/>.
/// Направление перевода определяет <see cref="TextConverter"/> по числу букв пары; при равенстве решает текущая раскладка
/// окна. После исправления выделенного результат снова выделяется (повторное нажатие возвращает текст), после исправления
/// строки каретка остаётся на месте. В консолях ничего не делает. В лог идут только пути, счётчики и коды языков.
/// </summary>
/// <param name="layouts">Сервис раскладок.</param>
/// <param name="tables">Таблицы «клавиша → символ» раскладок.</param>
/// <param name="guard">Замена выделенного через буфер обмена.</param>
/// <param name="focus">Доступ к тексту окна с фокусом.</param>
/// <param name="sender">Отправка ввода.</param>
/// <param name="waiter">Ожидание отпускания модификаторов.</param>
/// <param name="notifier">Получает сообщение о своей смене раскладки (подсказка).</param>
/// <param name="log">Журнал.</param>
internal sealed class TextCorrector(
    ILayoutService layouts,
    IKeyTables tables,
    IClipboardGuard guard,
    ITextFocus focus,
    IInputSender sender,
    IModifierWaiter waiter,
    ILayoutNotifier notifier,
    ILog log) : ITextCorrector
{
    /// <summary>Как подготовлено выделение к копированию.</summary>
    private enum Prepared
    {
        /// <summary>Исправлять не надо.</summary>
        Skip,

        /// <summary>Выделение подтверждено или сделано через UI Automation.</summary>
        Uia,

        /// <summary>UI Automation не помог: выделение сделано (или предполагается) клавишами.</summary>
        Keys,
    }

    /// <inheritdoc />
    public async Task CorrectAsync(CorrectStep step, CancellationToken cancellationToken)
    {
        if (step.Pair.Count != 2
            || !LayoutTags.TryGetLanguageId(step.Pair[0], out var languageA)
            || !LayoutTags.TryGetLanguageId(step.Pair[1], out var languageB))
        {
            log.Error("correction skipped: layout pair is invalid");
            return;
        }

        if (layouts.FindInstalled(languageA) is not { } first || layouts.FindInstalled(languageB) is not { } second)
        {
            log.Info("correction skipped: layouts not found");
            return;
        }

        if (focus.IsTerminalForeground())
        {
            log.Info("correction skipped: terminal window");
            return;
        }

        // Пока Shift или Ctrl жеста зажаты, наши Ctrl+C, Ctrl+V и стрелки превратились бы в другие сочетания.
        if (!await waiter.WaitReleasedAsync(ignoreShift: false, cancellationToken))
        {
            log.Info("correction skipped: modifiers were not released");
            return;
        }

        var line = step.Scope == CorrectScope.Line;
        var prepared = line ? await PrepareLineAsync(step.MaxChars, cancellationToken) : await PrepareSelectionAsync(cancellationToken);
        if (prepared == Prepared.Skip)
        {
            return;
        }

        var current = layouts.GetCurrent();
        bool? tieFromFirst = current is null ? null : LayoutPairSelector.LanguageOf(current.Hkl) switch
        {
            var language when language == languageA => true,
            var language when language == languageB => false,
            _ => null,
        };
        var firstTable = tables.GetTable(first.Hkl);
        var secondTable = tables.GetTable(second.Hkl);
        TextConversion? conversion = null;
        var length = 0;
        var result = await guard.ReplaceSelectionAsync(
            text =>
            {
                length = text.Length;
                if (text.Length > step.MaxChars
                    || (line && (CorrectionText.HasLineBreak(text) || CorrectionText.IsBlank(text)))
                    || (!line && prepared == Prepared.Keys && CorrectionText.LooksLikeCopiedLine(text)))
                {
                    return null;
                }

                conversion = TextConverter.Convert(text, firstTable, secondTable, tieFromFirst);
                return conversion?.Text;
            },
            cancellationToken);

        if (result != ReplaceResult.Replaced || conversion is not { } converted)
        {
            // Выделение для «до начала строки» сделали мы: возвращаем каретку на место, а не оставляем текст выделенным.
            if (line && (prepared == Prepared.Uia || result != ReplaceResult.NothingSelected))
            {
                _ = sender.SendKeyRepeat(VirtualKey.Right, 1, shift: false);
            }

            return;
        }

        var (source, target) = converted.FromFirst ? (first, second) : (second, first);
        log.Info($"corrected {(line ? "line" : "selection")} {length} chars {source.Code} -> {target.Code} via {prepared.ToString().ToLowerInvariant()}");
        if (!line)
        {
            await PlaceCaretAsync(step.CaretAfter, converted.Text, cancellationToken);
        }

        if (step.SwitchLayout)
        {
            var switched = layouts.SwitchTo(LayoutPairSelector.LanguageOf(target.Hkl));
            await notifier.OnOwnSwitchAsync(switched ?? target, cancellationToken);
        }
    }

    /// <summary>Выясняет, есть ли выделение, для исправления выделенного.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Способ копирования или <see cref="Prepared.Skip"/>, если выделения нет.</returns>
    private async Task<Prepared> PrepareSelectionAsync(CancellationToken cancellationToken)
    {
        switch (await focus.GetSelectionStateAsync(cancellationToken))
        {
            case SelectionState.Caret:
                log.Info("correction skipped: nothing selected");
                return Prepared.Skip;
            case SelectionState.Selected:
                return Prepared.Uia;
            default:
                return Prepared.Keys;
        }
    }

    /// <summary>Выделяет текст от каретки до начала строки; без UI Automation — клавишами Shift+Home.</summary>
    /// <param name="maxChars">Наибольшая длина текста.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Способ выделения или <see cref="Prepared.Skip"/>, если исправлять нечего.</returns>
    private async Task<Prepared> PrepareLineAsync(int maxChars, CancellationToken cancellationToken)
    {
        switch (await focus.SelectToLineStartAsync(maxChars, cancellationToken))
        {
            case LineSelectResult.Selected:
                return Prepared.Uia;
            case LineSelectResult.HasSelection:
                log.Info("correction skipped: text is already selected");
                return Prepared.Skip;
            case LineSelectResult.Empty:
                log.Info("correction skipped: nothing before the caret");
                return Prepared.Skip;
            case LineSelectResult.TooLong:
                log.Info("correction skipped: text is too long");
                return Prepared.Skip;
            default:
                if (!sender.SendKeyRepeat(VirtualKey.Home, 1, shift: true))
                {
                    return Prepared.Skip;
                }

                return Prepared.Keys;
        }
    }

    /// <summary>
    /// Расставляет каретку и выделение после вставки результата. Сразу после Ctrl+V каретка стоит справа от текста.
    /// </summary>
    /// <param name="caretAfter">Выбранный режим.</param>
    /// <param name="text">Вставленный текст.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task PlaceCaretAsync(CaretAfter caretAfter, string text, CancellationToken cancellationToken)
    {
        var steps = CorrectionText.CaretSteps(text);
        switch (caretAfter)
        {
            case CaretAfter.Restore:
                // Влево на всю длину (каретка у начала), затем Shift+Вправо: выделено, каретка справа.
                _ = sender.SendKeyRepeat(VirtualKey.Left, steps, shift: false);
                _ = sender.SendKeyRepeat(VirtualKey.Right, steps, shift: true);
                break;
            case CaretAfter.Left:
                _ = sender.SendKeyRepeat(VirtualKey.Left, steps, shift: false);
                break;
            case CaretAfter.Right:
                break;
            default:
                await ReselectAsync(text, cancellationToken);
                break;
        }
    }

    /// <summary>Снова выделяет вставленный результат, чтобы повторное нажатие вернуло текст назад.</summary>
    /// <param name="text">Вставленный текст.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task ReselectAsync(string text, CancellationToken cancellationToken)
    {
        if (!await focus.SelectBeforeCaretAsync(text, cancellationToken))
        {
            _ = sender.SendKeyRepeat(VirtualKey.Left, CorrectionText.CaretSteps(text), shift: true);
        }
    }
}
