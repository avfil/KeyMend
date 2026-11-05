using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Actions;

/// <summary>Шаги, которые вводят текст и клавиши в окно с фокусом: «Вставить текст» и «Нажать» (SPEC 3.2, F5.3).</summary>
internal interface IInputStepRunner
{
    /// <summary>Стирает набранное сокращение и вставляет текст.</summary>
    /// <param name="rule">Правило (для сообщений об ошибках).</param>
    /// <param name="step">Шаг.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача вставки.</returns>
    /// <exception cref="InvalidOperationException">Ввод не удался: цепочка прерывается, шаги после вставки не выполняются.</exception>
    Task InsertAsync(Rule rule, InsertTextStep step, CancellationToken cancellationToken);

    /// <summary>Нажимает клавишу или сочетание.</summary>
    /// <param name="rule">Правило (для сообщений об ошибках).</param>
    /// <param name="step">Шаг.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача нажатия.</returns>
    /// <exception cref="InvalidOperationException">Ввод не удался: цепочка прерывается.</exception>
    Task PressAsync(Rule rule, PressStep step, CancellationToken cancellationToken);
}

/// <summary>
/// Выполняет ввод шагов: ждёт отпускания модификаторов (иначе Backspace и символы станут сочетаниями), затем стирает
/// набранное и вставляет текст печатью или через буфер обмена. Секретный текст берётся из <see cref="ISecretStore"/> и
/// всегда идёт через буфер. Ни текст, ни ключ секрета в журнал не попадают.
/// </summary>
/// <param name="sender">Отправка ввода.</param>
/// <param name="guard">Вставка через буфер обмена.</param>
/// <param name="secrets">Хранилище секретов.</param>
/// <param name="waiter">Ожидание отпускания модификаторов.</param>
/// <param name="log">Журнал.</param>
internal sealed class InputStepRunner(
    IInputSender sender, IClipboardGuard guard, ISecretStore secrets, IModifierWaiter waiter, ILog log) : IInputStepRunner
{
    /// <inheritdoc />
    public async Task InsertAsync(Rule rule, InsertTextStep step, CancellationToken cancellationToken)
    {
        var text = ResolveText(rule, step);
        var viaClipboard = step.SecretRef is not null || step.Insert == InsertMode.Clipboard;

        // Печать Unicode и Backspace от Shift не зависят, поэтому «ДД » с зажатым Shift вставляется сразу.
        // Ctrl+V с Shift превратился бы в Ctrl+Shift+V, поэтому для буфера Shift ждём.
        if (!await waiter.WaitReleasedAsync(ignoreShift: !viaClipboard, cancellationToken))
        {
            throw new InvalidOperationException($"rule '{rule.Name}': modifiers are still held, text was not inserted");
        }

        if (viaClipboard)
        {
            if (!sender.SendKeyRepeat(VirtualKey.Back, step.Erase, shift: false)
                || !await guard.PasteTextAsync(text, cancellationToken))
            {
                throw new InvalidOperationException($"rule '{rule.Name}': paste failed");
            }
        }
        else if (!sender.SendTyped(step.Erase, text, step.Newline))
        {
            throw new InvalidOperationException($"rule '{rule.Name}': typing failed");
        }

        log.Info($"rule '{rule.Name}': inserted {text.Length} chars via {(viaClipboard ? "clipboard" : "typing")}");
    }

    /// <inheritdoc />
    public async Task PressAsync(Rule rule, PressStep step, CancellationToken cancellationToken)
    {
        if (!await waiter.WaitReleasedAsync(ignoreShift: false, cancellationToken))
        {
            throw new InvalidOperationException($"rule '{rule.Name}': modifiers are still held, keys were not pressed");
        }

        if (!sender.SendKeys(step.Keys))
        {
            throw new InvalidOperationException($"rule '{rule.Name}': key press failed");
        }
    }

    /// <summary>Берёт текст шага: из хранилища секретов или из самого шага.</summary>
    /// <param name="rule">Правило (для сообщений).</param>
    /// <param name="step">Шаг.</param>
    /// <returns>Текст для вставки.</returns>
    /// <exception cref="InvalidOperationException">Секрет не найден или у шага нет текста.</exception>
    private string ResolveText(Rule rule, InsertTextStep step)
    {
        if (step.SecretRef is { } key)
        {
            return secrets.TryGet(key, out var secret)
                ? secret
                : throw new InvalidOperationException($"rule '{rule.Name}': secret text is not found");
        }

        return step.Text ?? throw new InvalidOperationException($"rule '{rule.Name}': insert step has no text");
    }
}
