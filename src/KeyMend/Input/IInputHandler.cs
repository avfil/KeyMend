namespace KeyMend.Input;

/// <summary>
/// Приёмник событий ввода от <see cref="KeyboardHook"/>. Все методы вызываются только из потока хука
/// и обязаны отрабатывать за доли миллисекунды: без блокировок, ввода-вывода и логирования.
/// </summary>
internal interface IInputHandler
{
    /// <summary>Время, к которому надо проверить отложенное одиночное нажатие, или <c>null</c>, если ничего не ждём.</summary>
    uint? NextDeadline { get; }

    /// <summary>Обрабатывает клавишу (инжектированный ввод уже отфильтрован).</summary>
    /// <param name="e">Событие клавиатуры.</param>
    /// <returns>Решение: подавить ли клавишу и что запустить.</returns>
    HookDecision OnKey(in KeyEvent e);

    /// <summary>Сообщает о клике мышью или прокрутке колеса: для детектора это «чужое событие».</summary>
    /// <param name="time">Время события, мс.</param>
    void OnMouseDown(uint time);

    /// <summary>Сбрасывает внутреннее состояние (смена окна, пауза, переустановка хука).</summary>
    void Reset();

    /// <summary>Подменяет набор привязок (смена программы переднего плана или настроек) и сбрасывает состояние.</summary>
    /// <param name="table">Новый набор.</param>
    void SetBindings(BindingTable table);

    /// <summary>Забирает решение для правила, чьё одиночное нажатие дождалось конца интервала двойного.</summary>
    /// <param name="now">Текущее время, мс.</param>
    /// <param name="decision">Решение с правилом, которое надо запустить.</param>
    /// <returns><c>true</c>, если правило есть.</returns>
    bool TryTakeDue(uint now, out HookDecision decision);
}
