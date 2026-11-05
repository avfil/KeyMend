using System.Diagnostics.CodeAnalysis;
using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>
/// Распознаёт триггеры по потоку событий (SPEC 3.1): короткие нажатия модификаторов (tap), клавиши с модификаторами (press)
/// и двойные нажатия. Чистый класс без WinAPI; вызывается только из потока хука, не выделяет память в обычном случае и не
/// блокируется. Содержимое ввода не хранит: только номера нажатий и время.
/// </summary>
/// <param name="isPhysicallyDown">
/// Проверка физического состояния клавиши (<c>GetAsyncKeyState</c>). Нужна, чтобы «залипшее» после блокировки экрана
/// состояние не мешало распознаванию; для текущей клавиши в колбэке хука не вызывается. В тестах — <c>null</c>.
/// </param>
internal sealed class HotkeyEngine(Func<VirtualKey, bool>? isPhysicallyDown = null)
{
    /// <summary>Число виртуальных кодов.</summary>
    internal const int KeyCount = 256;

    /// <summary>Клавиша сейчас нажата (по нашим данным).</summary>
    private readonly bool[] _isDown = new bool[KeyCount];

    /// <summary>Номер нажатия, при котором клавиша была нажата в последний раз (0 — не нажималась).</summary>
    private readonly ulong[] _downSeq = new ulong[KeyCount];

    /// <summary>Время последнего нажатия клавиши, мс.</summary>
    private readonly uint[] _downTime = new uint[KeyCount];

    /// <summary>Отпускание клавиши надо подавить, потому что её нажатие сработало как press.</summary>
    private readonly bool[] _suppressUp = new bool[KeyCount];

    /// <summary>Одиночные правила, дождавшиеся конца интервала двойного; их забирает <see cref="TryTakeDue"/>.</summary>
    private readonly Queue<Rule> _ready = new();

    /// <summary>Текущий набор привязок.</summary>
    private BindingTable _table = BindingTable.Empty;

    /// <summary>Счётчик нажатий клавиш и кликов мыши (автоповтор не считается).</summary>
    private ulong _seq;

    /// <summary>Номер последнего клика мыши или прокрутки колеса.</summary>
    private ulong _mouseSeq;

    /// <summary>Значение <see cref="_seq"/> в момент последнего срабатывания tap: клавиши, нажатые не позже, уже «израсходованы».</summary>
    private ulong _lastFiredSeq;

    /// <summary>Есть первое нажатие двойного триггера, ждущее второго.</summary>
    private bool _hasPending;

    /// <summary>Основа ожидающего триггера.</summary>
    private BindingBase _pendingBase;

    /// <summary>Одиночное правило той же основы, которое сработает, если второго нажатия не будет; может отсутствовать.</summary>
    private Rule? _pendingSingle;

    /// <summary>Время первого нажатия (отпускания для tap), мс.</summary>
    private uint _pendingTime;

    /// <summary>Интервал двойного нажатия, мс.</summary>
    private int _pendingDoubleMs;

    /// <summary>Пришёл чужой ввод: ожидание закончено досрочно.</summary>
    private bool _pendingDue;

    /// <summary>Время, к которому надо проверить отложенное одиночное нажатие, или <c>null</c>, если ничего не ждём.</summary>
    public uint? NextDeadline =>
        _hasPending && !_pendingDue && _pendingSingle is not null ? _pendingTime + (uint)_pendingDoubleMs : null;

    /// <summary>Обрабатывает клавишу (инжектированный ввод уже отфильтрован).</summary>
    /// <param name="e">Событие клавиатуры.</param>
    /// <returns>Решение: подавить ли клавишу и какое правило запустить.</returns>
    public HookDecision OnKey(in KeyEvent e)
    {
        var vk = (int)e.Vk & 0xFF;
        return e.IsUp ? OnKeyUp(in e, vk) : OnKeyDown(in e, vk);
    }

    /// <summary>Сообщает о клике мышью или прокрутке колеса: для детектора это «чужое событие».</summary>
    /// <param name="time">Время события, мс.</param>
    public void OnMouseDown(uint time)
    {
        _mouseSeq = ++_seq;
        Expire(time);
        if (_hasPending)
        {
            _pendingDue = true;
        }
    }

    /// <summary>Сбрасывает нажатые клавиши, счётчики и ожидающее двойное нажатие.</summary>
    public void Reset()
    {
        Array.Clear(_isDown);
        Array.Clear(_downSeq);
        Array.Clear(_downTime);
        Array.Clear(_suppressUp);
        _seq = 0;
        _mouseSeq = 0;
        _lastFiredSeq = 0;
        ClearPending();
        _ready.Clear();
    }

    /// <summary>Подменяет набор привязок и сбрасывает состояние.</summary>
    /// <param name="table">Новый набор.</param>
    public void SetBindings(BindingTable table)
    {
        _table = table;
        Reset();
    }

    /// <summary>Забирает правило, чьё одиночное нажатие дождалось конца интервала двойного.</summary>
    /// <param name="now">Текущее время, мс.</param>
    /// <param name="rule">Правило, которое надо запустить.</param>
    /// <returns><c>true</c>, если правило есть.</returns>
    public bool TryTakeDue(uint now, [NotNullWhen(true)] out Rule? rule)
    {
        Expire(now);
        return _ready.TryDequeue(out rule);
    }

    /// <summary>Нажатие: учитывает клавишу, отмечает чужой ввод для ожидающего двойного и проверяет привязки press.</summary>
    private HookDecision OnKeyDown(in KeyEvent e, int vk)
    {
        Expire(e.Time);

        // Автоповтор подавленной клавиши тоже подавляем, чтобы в окно не попадали «хвосты» сочетания.
        if (_suppressUp[vk])
        {
            return new HookDecision(true, null);
        }

        if (_isDown[vk])
        {
            return HookDecision.Pass;
        }

        _isDown[vk] = true;
        _downSeq[vk] = ++_seq;
        _downTime[vk] = e.Time;

        if (_hasPending && !IsPartOf(_pendingBase, e.Vk))
        {
            _pendingDue = true;
        }

        return ModifierSets.IsModifier(e.Vk) ? HookDecision.Pass : CheckPresses(in e, vk);
    }

    /// <summary>Отпускание: подавляет клавишу press или проверяет привязки tap.</summary>
    private HookDecision OnKeyUp(in KeyEvent e, int vk)
    {
        Expire(e.Time);
        if (_suppressUp[vk])
        {
            _suppressUp[vk] = false;
            _isDown[vk] = false;
            return new HookDecision(true, null);
        }

        var decision = HookDecision.Pass;
        if (_isDown[vk] && ModifierSets.IsModifier(e.Vk))
        {
            decision = CheckTaps(in e, vk);
        }

        _isDown[vk] = false;
        return decision;
    }

    /// <summary>Ищет привязку press для нажатой клавиши с точно совпадающими модификаторами.</summary>
    private HookDecision CheckPresses(in KeyEvent e, int vk)
    {
        Binding? single = null;
        Binding? dbl = null;
        BindingBase? matched = null;
        var synced = false;
        foreach (var binding in _table.Presses)
        {
            if (binding.Key != e.Vk)
            {
                continue;
            }

            if (!synced)
            {
                SyncHeldKeys(vk);
                synced = true;
            }

            if (!ModifierSets.Matches(binding.Hold, HeldSided(ModifierSet.None))
                || (matched is { } other && other != binding.Base))
            {
                continue;
            }

            matched = binding.Base;
            if (binding.DoubleMs is null)
            {
                single ??= binding;
            }
            else
            {
                dbl ??= binding;
            }
        }

        if (matched is not { } found)
        {
            return HookDecision.Pass;
        }

        _suppressUp[vk] = true;

        // Пока Alt или Win зажаты, окно не увидело клавишу: без маски отпускание модификатора откроет меню.
        return Activate(found, single, dbl, e.Time, suppress: true, ModifierSets.HasAltOrWin(found.Hold), VirtualKey.None);
    }

    /// <summary>
    /// Проверяет привязки tap при отпускании модификатора. Условия (SPEC 3.1, п. 7 «жесты как попало»): все клавиши
    /// привязки нажаты; с начала удержания не нажималось ничего лишнего и не было кликов мыши; от нажатия последней
    /// клавиши жеста прошло не больше <see cref="Binding.MaxHoldMs"/>;
    /// удерживаемые модификаторы отвечают требованию <see cref="Binding.Hold"/>.
    /// </summary>
    private HookDecision CheckTaps(in KeyEvent e, int vk)
    {
        SyncHeldKeys(vk);
        var released = ModifierSets.FromKey(e.Vk);
        Binding? single = null;
        Binding? dbl = null;
        BindingBase? matched = null;
        foreach (var binding in _table.Taps)
        {
            // Сочетание «держать X + tap Y» собирается как попало: если X отпущен раньше Y, срабатываем на отпускании X,
            // пока Y ещё нажата. Остальные условия (время, лишний ввод) те же, а повторно правило на отпускании Y не сработает.
            var keyReleased = (binding.KeySet & released) != 0;
            var holdReleased = !keyReleased && ModifierSets.Permits(binding.Hold, e.Vk);
            if ((!keyReleased && !holdReleased) || !AllDown(binding.Keys))
            {
                continue;
            }

            var firstSeq = ulong.MaxValue;
            foreach (var key in binding.Keys)
            {
                firstSeq = Math.Min(firstSeq, _downSeq[(int)key]);
            }

            // Жест собирается как попало (SPEC 3.1, п. 7): порог считаем от нажатия последней клавиши жеста.
            var gestureTime = GestureStartTime(binding, firstSeq);
            if (firstSeq <= _lastFiredSeq
                || unchecked((int)(e.Time - gestureTime)) > binding.MaxHoldMs
                || !ModifierSets.Matches(binding.Hold, HeldSided(binding.KeySet))
                || HasForeignInput(binding)
                || (matched is { } other && other != binding.Base))
            {
                continue;
            }

            matched = binding.Base;
            if (binding.DoubleMs is null)
            {
                single ??= binding;
            }
            else
            {
                dbl ??= binding;
            }
        }

        if (matched is not { } found)
        {
            return HookDecision.Pass;
        }

        _lastFiredSeq = _seq;

        // Одиночный Alt или Win при отпускании открыл бы меню окна или «Пуск»: отпускание подавляем,
        // а поток функций отправит маскирующую клавишу и само отпускание (SPEC 3.1).
        var suppress = ModifierSets.IsAltOrWin(e.Vk);
        return Activate(found, single, dbl, e.Time, suppress, suppress, suppress ? e.Vk : VirtualKey.None);
    }

    /// <summary>
    /// Применяет распознанное нажатие: одиночное срабатывает сразу; если есть двойное, первое нажатие откладывается,
    /// а второе в срок запускает двойное. Одиночное той же основы, если оно есть, сработает по истечении интервала.
    /// </summary>
    private HookDecision Activate(
        BindingBase key, Binding? single, Binding? dbl, uint time, bool suppress, bool mask, VirtualKey release)
    {
        Rule? fire = null;
        if (_hasPending && _pendingBase == key && dbl?.DoubleMs is { } interval
            && unchecked((int)(time - _pendingTime)) < interval)
        {
            fire = dbl.Rule;
            ClearPending();
        }
        else
        {
            // Ожидавшее другое или просроченное нажатие завершается как одиночное.
            ReleasePending();
            if (dbl?.DoubleMs is { } doubleMs)
            {
                _hasPending = true;
                _pendingBase = key;
                _pendingSingle = single?.Rule;
                _pendingTime = time;
                _pendingDoubleMs = doubleMs;
                _pendingDue = false;
            }
            else
            {
                fire = single?.Rule;
            }
        }

        return new HookDecision(suppress, fire, mask, release);
    }

    /// <summary>Завершает ожидание, если интервал истёк или пришёл чужой ввод.</summary>
    private void Expire(uint now)
    {
        if (_hasPending && (_pendingDue || unchecked((int)(now - _pendingTime)) >= _pendingDoubleMs))
        {
            ReleasePending();
        }
    }

    /// <summary>Завершает ожидание: одиночное правило, если оно есть, становится готовым к запуску.</summary>
    private void ReleasePending()
    {
        if (_hasPending && _pendingSingle is { } single)
        {
            _ready.Enqueue(single);
        }

        ClearPending();
    }

    /// <summary>Забывает ожидающее нажатие без запуска правила.</summary>
    private void ClearPending()
    {
        _hasPending = false;
        _pendingSingle = null;
        _pendingDue = false;
    }

    /// <summary>
    /// Время нажатия последней клавиши жеста: клавиш tap и удерживаемых модификаторов, нажатых не раньше самой ранней
    /// клавиши tap. Модификатор, который держали ещё до жеста, в отсчёт не входит.
    /// </summary>
    /// <param name="binding">Привязка tap.</param>
    /// <param name="firstSeq">Номер нажатия самой ранней клавиши tap.</param>
    /// <returns>Время нажатия, мс.</returns>
    private uint GestureStartTime(Binding binding, ulong firstSeq)
    {
        var lastSeq = 0UL;
        var lastTime = 0u;
        foreach (var key in binding.Keys)
        {
            Consider((int)key);
        }

        foreach (var key in ModifierSets.SidedKeys)
        {
            var k = (int)key;
            if (_isDown[k] && _downSeq[k] > firstSeq && ModifierSets.Permits(binding.Hold, key))
            {
                Consider(k);
            }
        }

        return lastTime;

        void Consider(int k)
        {
            if (_downSeq[k] > lastSeq)
            {
                lastSeq = _downSeq[k];
                lastTime = _downTime[k];
            }
        }
    }

    /// <summary>Входит ли клавиша в основу триггера: нажимаемая клавиша или допустимый удерживаемый модификатор.</summary>
    private static bool IsPartOf(BindingBase key, VirtualKey vk) =>
        key.Kind == BindingKind.Tap
            ? (ModifierSets.FromKey(vk) & key.Keys) != 0 || ModifierSets.Permits(key.Hold, vk)
            : vk == key.Key || ModifierSets.Permits(key.Hold, vk);

    /// <summary>Нажаты ли все перечисленные клавиши.</summary>
    private bool AllDown(VirtualKey[] keys)
    {
        foreach (var key in keys)
        {
            if (!_isDown[(int)key])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Было ли с начала текущего удержания что-то лишнее: клик мыши или нажатие клавиши, которой нет среди клавиш
    /// привязки и допустимых удерживаемых модификаторов. «Начало» — самое раннее нажатие из всех сейчас удерживаемых клавиш.
    /// </summary>
    private bool HasForeignInput(Binding binding)
    {
        var start = ulong.MaxValue;
        for (var i = 0; i < KeyCount; i++)
        {
            if (_isDown[i] && _downSeq[i] < start)
            {
                start = _downSeq[i];
            }
        }

        if (_mouseSeq >= start)
        {
            return true;
        }

        for (var i = 0; i < KeyCount; i++)
        {
            if (_downSeq[i] < start || _downSeq[i] == 0)
            {
                continue;
            }

            if (!IsPartOf(binding.Base, (VirtualKey)i))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Модификаторы со стороной, которые сейчас удерживаются, кроме перечисленных.</summary>
    private ModifierSet HeldSided(ModifierSet excluding)
    {
        var result = ModifierSet.None;
        foreach (var key in ModifierSets.SidedKeys)
        {
            var bit = ModifierSets.FromKey(key);
            if (_isDown[(int)key] && (excluding & bit) == 0)
            {
                result |= bit;
            }
        }

        return result;
    }

    /// <summary>
    /// Снимает отметку «нажата» с клавиш, которые система считает отпущенными (пропущенное отпускание после блокировки
    /// экрана и т. п.). Текущую клавишу не трогает: в колбэке хука её асинхронное состояние ещё не обновлено.
    /// </summary>
    private void SyncHeldKeys(int currentVk)
    {
        if (isPhysicallyDown is null)
        {
            return;
        }

        for (var i = 0; i < KeyCount; i++)
        {
            if (i != currentVk && _isDown[i] && !isPhysicallyDown((VirtualKey)i))
            {
                _isDown[i] = false;
            }
        }
    }
}
