namespace KeyMend.Input;

/// <summary>Сырое событие клавиатуры из хука. Содержит клавишу, а не символ.</summary>
/// <param name="Vk">Виртуальный код; модификаторы с указанием стороны.</param>
/// <param name="ScanCode">Скан-код.</param>
/// <param name="IsUp"><c>true</c> — клавиша отпускается, <c>false</c> — нажимается.</param>
/// <param name="IsExtended">Расширенная клавиша (правые Ctrl/Alt, стрелки и т. п.).</param>
/// <param name="IsInjected">Событие синтезировано программно (<c>LLKHF_INJECTED</c>).</param>
/// <param name="Time">Время события в миллисекундах; разность считать через <c>unchecked</c>.</param>
/// <param name="CapsLock">
/// CapsLock включён к этому событию. Хук ведёт флаг сам по нажатиям CapsLock: <c>GetKeyState</c> в потоке без окон
/// отдаёт устаревшее значение, потому что очередь потока не обрабатывает клавиатурные сообщения.
/// </param>
internal readonly record struct KeyEvent(
    VirtualKey Vk, ushort ScanCode, bool IsUp, bool IsExtended, bool IsInjected, uint Time, bool CapsLock = false);
