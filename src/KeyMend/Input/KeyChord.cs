namespace KeyMend.Input;

/// <summary>Разобранная строка клавиш: модификаторы и необязательная основная клавиша (<c>LCtrl+RCtrl+1</c>, <c>F18</c>, <c>RCtrl</c>).</summary>
/// <param name="Modifiers">Модификаторы из строки.</param>
/// <param name="Key">Основная (немодификаторная) клавиша или <see cref="VirtualKey.None"/>, если в строке только модификаторы.</param>
internal readonly record struct KeyChord(ModifierSet Modifiers, VirtualKey Key);
