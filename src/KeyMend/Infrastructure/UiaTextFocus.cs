using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using KeyMend.Input;
using KeyMend.Native;
using KeyMend.Text;

namespace KeyMend.Infrastructure;

/// <summary>
/// Реализация <see cref="ITextFocus"/> на UI Automation (<c>TextPattern</c>). Каждый запрос идёт в пуле потоков с
/// таймаутом: зависшее окно не должно тормозить поток функций. Любой отказ UI Automation («нет паттерна», «элемент исчез»)
/// даёт результат «недоступно», по которому вызывающий код переходит на запасной способ через клавиши. В лог идут только
/// причины, не текст.
/// </summary>
/// <param name="log">Журнал.</param>
internal sealed class UiaTextFocus(ILog log) : ITextFocus
{
    /// <summary>Сколько ждать ответ чужого процесса, мс.</summary>
    internal const int TimeoutMs = 700;

    /// <inheritdoc />
    public unsafe bool IsTerminalForeground()
    {
        var foreground = User32.GetForegroundWindow();
        if (foreground == 0)
        {
            return false;
        }

        var buffer = stackalloc char[64];
        var length = User32.GetClassName(foreground, buffer, 64);
        return TerminalWindows.IsTerminal(new ReadOnlySpan<char>(buffer, length));
    }

    /// <inheritdoc />
    public Task<SelectionState> GetSelectionStateAsync(CancellationToken cancellationToken) =>
        QueryAsync(ReadSelectionState, SelectionState.Unavailable, cancellationToken);

    /// <inheritdoc />
    public Task<LineSelectResult> SelectToLineStartAsync(int maxChars, CancellationToken cancellationToken) =>
        QueryAsync(() => SelectToLineStart(maxChars), LineSelectResult.Unavailable, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SelectBeforeCaretAsync(string expected, CancellationToken cancellationToken) =>
        QueryAsync(() => SelectBeforeCaret(expected), false, cancellationToken);

    /// <summary>Ошибки UI Automation, которые означают «элемент недоступен», а не сбой программы.</summary>
    /// <param name="ex">Исключение.</param>
    /// <returns><c>true</c>, если это ожидаемый отказ.</returns>
    private static bool IsUiaFailure(Exception ex) =>
        ex is ElementNotAvailableException or InvalidOperationException or COMException
            or NotSupportedException or ArgumentException or TimeoutException;

    /// <summary>Единственный диапазон-каретка или выделение элемента с фокусом.</summary>
    /// <returns>Диапазон либо <c>null</c>, если паттерна нет или диапазонов не один.</returns>
    private static TextPatternRange? FocusedRange()
    {
        var element = AutomationElement.FocusedElement;
        if (element is null
            || !element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
            || pattern is not TextPattern text)
        {
            return null;
        }

        var selection = text.GetSelection();
        return selection.Length == 1 ? selection[0] : null;
    }

    /// <summary>Пуст ли диапазон: началом и концом совпадает с кареткой.</summary>
    /// <param name="range">Диапазон.</param>
    /// <returns><c>true</c> для каретки без выделения.</returns>
    private static bool IsCaret(TextPatternRange range) =>
        range.CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.End) == 0;

    /// <summary>Сравнивает тексты без учёта разницы между CRLF и LF: приложения нормализуют переводы строк по-разному.</summary>
    private static bool SameText(string a, string b) =>
        string.Equals(a.Replace("\r\n", "\n", StringComparison.Ordinal), b.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);

    /// <summary>Выполняет запрос в пуле потоков с таймаутом; при отказе возвращает запасное значение.</summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <param name="query">Запрос.</param>
    /// <param name="fallback">Результат при отказе или таймауте.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Результат запроса или запасное значение.</returns>
    private async Task<T> QueryAsync<T>(Func<T> query, T fallback, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(query, cancellationToken).WaitAsync(TimeSpan.FromMilliseconds(TimeoutMs), cancellationToken);
        }
        catch (TimeoutException)
        {
            log.Info("text query timed out");
            return fallback;
        }
    }

    /// <summary>Читает состояние выделения; любая ошибка UI Automation означает «недоступно».</summary>
    private static SelectionState ReadSelectionState()
    {
        try
        {
            return FocusedRange() is { } range
                ? (IsCaret(range) ? SelectionState.Caret : SelectionState.Selected)
                : SelectionState.Unavailable;
        }
        catch (Exception ex) when (IsUiaFailure(ex))
        {
            return SelectionState.Unavailable;
        }
    }

    /// <summary>
    /// Выделяет текст от начала абзаца до каретки. Каретка в начале абзаца определяется по охватывающему абзацу: иначе
    /// сдвиг на «абзац назад» захватил бы предыдущий.
    /// </summary>
    /// <param name="maxChars">Наибольшая длина текста.</param>
    private static LineSelectResult SelectToLineStart(int maxChars)
    {
        try
        {
            if (FocusedRange() is not { } caret)
            {
                return LineSelectResult.Unavailable;
            }

            if (!IsCaret(caret))
            {
                return LineSelectResult.HasSelection;
            }

            var paragraph = caret.Clone();
            paragraph.ExpandToEnclosingUnit(TextUnit.Paragraph);
            if (paragraph.CompareEndpoints(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.Start) == 0)
            {
                return LineSelectResult.Empty;
            }

            var range = caret.Clone();
            range.MoveEndpointByRange(TextPatternRangeEndpoint.Start, paragraph, TextPatternRangeEndpoint.Start);
            var raw = range.GetText(maxChars + 1);
            if (raw.Length > maxChars)
            {
                return LineSelectResult.TooLong;
            }

            var tail = CorrectionText.LineTail(raw);
            if (CorrectionText.IsBlank(tail))
            {
                return LineSelectResult.Empty;
            }

            if (tail.Length != raw.Length)
            {
                // В абзаце есть мягкий перевод строки (Shift+Enter): берём только последнюю строку и сверяем её текст.
                range = caret.Clone();
                if (range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -CorrectionText.CaretSteps(tail)) == 0
                    || !SameText(range.GetText(tail.Length + 1), tail))
                {
                    return LineSelectResult.Unavailable;
                }
            }

            range.Select();
            return LineSelectResult.Selected;
        }
        catch (Exception ex) when (IsUiaFailure(ex))
        {
            return LineSelectResult.Unavailable;
        }
    }

    /// <summary>Выделяет назад от каретки столько символов, сколько в ожидаемом тексте, и сверяет их с ним.</summary>
    /// <param name="expected">Ожидаемый текст.</param>
    private static bool SelectBeforeCaret(string expected)
    {
        try
        {
            if (FocusedRange() is not { } caret || !IsCaret(caret))
            {
                return false;
            }

            var steps = CorrectionText.CaretSteps(expected);
            var range = caret.Clone();
            if (range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -steps) != -steps
                || !SameText(range.GetText(expected.Length + 2), expected))
            {
                return false;
            }

            range.Select();
            return true;
        }
        catch (Exception ex) when (IsUiaFailure(ex))
        {
            return false;
        }
    }
}
