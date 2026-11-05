using KeyMend.Text;

namespace KeyMend.Input;

/// <summary>
/// Обработчик событий для хука: передаёт клавиши <see cref="HotkeyEngine"/> и ведёт <see cref="WordBuffer"/> набранных
/// клавиш для автозамены (SPEC F5.1). Исправление ввода буфер не использует: оно берёт текст из окна (SPEC F3).
/// Вызывается только из потока хука: без блокировок, ввода-вывода и логирования. Буфер лежит только в памяти и
/// сбрасывается при смене окна, клике, Enter, Tab, Esc, навигации, сочетаниях с Ctrl/Alt/Win и срабатывании любого
/// правила. Содержимое ввода никуда не записывается.
/// </summary>
/// <param name="engine">Распознаватель триггеров с привязками.</param>
/// <param name="bufferLimit">Сколько последних клавиш помнить (<c>wordBufferLimit</c>).</param>
/// <param name="isPhysicallyDown">Проверка физического состояния клавиши (<c>GetAsyncKeyState</c>): Shift и Ctrl/Alt/Win в момент нажатия символа.</param>
/// <param name="currentLayout">Читает раскладку окна с фокусом (HKL) для определения начала слова; вызывается редко, только при совпадении сокращения. <c>null</c> — раскладка неизвестна.</param>
internal sealed class InputRouter(
    HotkeyEngine engine, int bufferLimit, Func<VirtualKey, bool> isPhysicallyDown, Func<nint>? currentLayout = null) : IInputHandler
{
    /// <summary>Клавиши, при удержании которых символьная клавиша — это сочетание, а не набор текста.</summary>
    private static readonly VirtualKey[] _shortcutModifiers =
    [
        VirtualKey.LControl, VirtualKey.RControl, VirtualKey.LMenu, VirtualKey.RMenu, VirtualKey.LWin, VirtualKey.RWin,
    ];

    /// <summary>
    /// Буфер набранных клавиш. Заменяется целиком при смене лимита из другого потока, поэтому поле <c>volatile</c>,
    /// а каждая операция берёт его в локальную переменную один раз.
    /// </summary>
    private volatile WordBuffer _buffer = new(bufferLimit);

    /// <summary>Сокращения для текущей программы; меняются вместе с набором привязок (<see cref="SetBindings"/>).</summary>
    private volatile SnippetMatcher _snippets = SnippetMatcher.Empty;

    /// <summary>Конечная клавиша сработавшего сокращения, пока она не отпущена; иначе <see cref="VirtualKey.None"/>.</summary>
    private VirtualKey _snippetKey;

    /// <summary>Сколько клавиш сейчас в буфере набранного (для диагностики и тестов; содержимое не раскрывается).</summary>
    public int BufferedCount => _buffer.Count;

    /// <inheritdoc />
    public uint? NextDeadline => engine.NextDeadline;

    /// <summary>Меняет размер буфера набранного; набранное до этого теряется. Вызывается при смене настроек из любого потока.</summary>
    /// <param name="limit">Новый лимит клавиш.</param>
    public void SetBufferLimit(int limit)
    {
        if (limit != _buffer.Capacity)
        {
            _buffer = new WordBuffer(limit);
        }
    }

    /// <inheritdoc />
    public HookDecision OnKey(in KeyEvent e)
    {
        var decision = engine.OnKey(in e);
        if (decision.Rule is not null)
        {
            return OnRule(decision);
        }

        // Конечная клавиша сработавшего сокращения: её автоповтор и отпускание тоже не должны дойти до окна.
        if (e.Vk == _snippetKey && _snippetKey != VirtualKey.None)
        {
            if (e.IsUp || !isPhysicallyDown(e.Vk))
            {
                // Отпускание, либо отпускание потерялось и это уже новое нажатие.
                _snippetKey = VirtualKey.None;
                if (e.IsUp)
                {
                    return decision with { Suppress = true };
                }
            }
            else
            {
                return decision with { Suppress = true };
            }
        }

        if (e.IsUp)
        {
            // Ctrl, Alt или Win отпущены: сочетание закончилось, набранное до него недействительно.
            if (IsShortcutModifier(e.Vk))
            {
                _buffer.Clear();
            }
        }
        else if (!decision.Suppress && OnKeyDown(in e) && _snippets.Count > 0
            && _snippets.TryMatch(_buffer, currentLayout, out var snippetRule))
        {
            // Конечную клавишу окно не получит, остальные клавиши сокращения стирает шаг вставки.
            _buffer.Clear();
            _snippetKey = e.Vk;
            return new HookDecision(true, snippetRule);
        }

        return decision;
    }

    /// <inheritdoc />
    public void OnMouseDown(uint time)
    {
        engine.OnMouseDown(time);
        ResetText();
    }

    /// <inheritdoc />
    public void Reset()
    {
        engine.Reset();
        _snippetKey = VirtualKey.None;
        ResetText();
    }

    /// <inheritdoc />
    public void SetBindings(BindingTable table)
    {
        engine.SetBindings(table);
        _snippets = table.Snippets;
        ResetText();
    }

    /// <inheritdoc />
    public bool TryTakeDue(uint now, out HookDecision decision)
    {
        if (engine.TryTakeDue(now, out var rule))
        {
            decision = OnRule(new HookDecision(false, rule));
            return true;
        }

        decision = default;
        return false;
    }

    /// <summary>Очищает буфер набранного.</summary>
    private void ResetText() => _buffer.Clear();

    /// <summary>
    /// Сработало правило: набранные клавиши больше не относятся к текущему состоянию (раскладка или текст могли
    /// измениться), поэтому буфер очищается.
    /// </summary>
    /// <param name="decision">Решение движка.</param>
    /// <returns>То же решение.</returns>
    private HookDecision OnRule(HookDecision decision)
    {
        ResetText();
        return decision;
    }

    /// <summary>Учитывает нажатие клавиши (не автоповтор подавленной и не отпускание).</summary>
    /// <param name="e">Событие клавиатуры.</param>
    /// <returns><c>true</c>, если клавиша добавлена в буфер как символ.</returns>
    private bool OnKeyDown(in KeyEvent e)
    {
        var vk = e.Vk;
        var buffer = _buffer;

        // Shift, CapsLock и NumLock/ScrollLock ничего не меняют: Shift учтём при нажатии символа, CapsLock приходит в событии.
        if (vk is VirtualKey.LShift or VirtualKey.RShift or VirtualKey.CapsLock
            or VirtualKey.NumLock or VirtualKey.ScrollLock)
        {
            return false;
        }

        // Alt и Win: следом будет сочетание, буфер недействителен. Ctrl при нажатии буфер не трогает: очистит его
        // символьная клавиша при зажатом Ctrl или отпускание Ctrl.
        if (ModifierSets.IsModifier(vk))
        {
            if (ModifierSets.IsAltOrWin(vk))
            {
                buffer.Clear();
            }

            return false;
        }

        var shortcut = IsShortcut();
        if (vk == VirtualKey.Back && !shortcut)
        {
            buffer.Backspace();
        }
        else if (CharacterKeys.IsCharacter(vk) && !shortcut)
        {
            var shift = isPhysicallyDown(VirtualKey.LShift) || isPhysicallyDown(VirtualKey.RShift);
            buffer.Push(new KeyStroke(vk, shift, e.CapsLock));
            return true;
        }
        else
        {
            // Enter, Tab, Esc, навигация, функциональные клавиши, сочетания с Ctrl/Alt/Win (в том числе Ctrl+Backspace).
            buffer.Clear();
        }

        return false;
    }

    /// <summary>Является ли клавиша Ctrl, Alt или Win.</summary>
    /// <param name="vk">Клавиша.</param>
    /// <returns><c>true</c> для любой стороны этих модификаторов.</returns>
    private static bool IsShortcutModifier(VirtualKey vk) => Array.IndexOf(_shortcutModifiers, vk) >= 0;

    /// <summary>Зажаты ли Ctrl, Alt или Win: тогда символьная клавиша — часть сочетания.</summary>
    /// <returns><c>true</c>, если зажат хотя бы один.</returns>
    private bool IsShortcut()
    {
        foreach (var key in _shortcutModifiers)
        {
            if (isPhysicallyDown(key))
            {
                return true;
            }
        }

        return false;
    }
}
