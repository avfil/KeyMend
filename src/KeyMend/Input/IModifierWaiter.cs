namespace KeyMend.Input;

/// <summary>Ожидание отпускания модификаторов перед вводом: иначе наш Backspace станет Ctrl+Backspace, а символы — сочетаниями (SPEC 3.2).</summary>
internal interface IModifierWaiter
{
    /// <summary>Ждёт, пока отпущены Ctrl, Alt и Win, а при <paramref name="ignoreShift"/> равном <c>false</c> — и Shift.</summary>
    /// <param name="ignoreShift">Не ждать Shift: печать символов Unicode и Backspace от него не зависят.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns><c>false</c>, если нужные модификаторы не отпущены за отведённое время (1 с).</returns>
    Task<bool> WaitReleasedAsync(bool ignoreShift, CancellationToken cancellationToken);
}
