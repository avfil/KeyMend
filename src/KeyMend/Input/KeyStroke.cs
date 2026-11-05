namespace KeyMend.Input;

/// <summary>
/// Нажатая символьная клавиша вместе с состоянием Shift и CapsLock (SPEC F3). Хранит клавишу, а не символ:
/// символ получается только при исправлении по таблице нужной раскладки.
/// </summary>
/// <param name="Vk">Виртуальный код клавиши.</param>
/// <param name="Shift">Shift был зажат в момент нажатия.</param>
/// <param name="CapsLock">CapsLock был включён в момент нажатия.</param>
internal readonly record struct KeyStroke(VirtualKey Vk, bool Shift, bool CapsLock);
