using KeyMend.Input;

namespace KeyMend.Text;

/// <summary>Что даёт клавиша в раскладке.</summary>
internal enum TranslationKind
{
    /// <summary>Клавиша не даёт символа (в этой раскладке или в этом состоянии Shift/CapsLock).</summary>
    None,

    /// <summary>Клавиша даёт символ <see cref="Translation.Char"/>.</summary>
    Char,

    /// <summary>Мёртвая клавиша (акцент): символ появится только после следующей клавиши.</summary>
    Dead,
}

/// <summary>Результат перевода клавиши в символ.</summary>
/// <param name="Kind">Вид результата.</param>
/// <param name="Char">Символ при <see cref="TranslationKind.Char"/>, иначе нуль.</param>
internal readonly record struct Translation(TranslationKind Kind, char Char = '\0')
{
    /// <summary>Клавиша не даёт символа.</summary>
    public static Translation None => default;

    /// <summary>Мёртвая клавиша.</summary>
    public static Translation Dead => new(TranslationKind.Dead);

    /// <summary>Клавиша даёт символ <paramref name="c"/>.</summary>
    /// <param name="c">Символ.</param>
    /// <returns>Перевод в символ.</returns>
    public static Translation Of(char c) => new(TranslationKind.Char, c);
}

/// <summary>
/// Таблица одной раскладки: (клавиша, Shift, CapsLock) → символ и обратный поиск символ → клавиша.
/// Без WinAPI; строится из системы в <see cref="Layouts.KeyMap"/> или берётся из запасной таблицы <see cref="FallbackLayouts"/>.
/// После построения таблица не меняется, поэтому её можно читать из любых потоков.
/// </summary>
internal sealed class LayoutTable
{
    /// <summary>Число виртуальных кодов.</summary>
    private const int _keyCount = 256;

    /// <summary>Число комбинаций Shift × CapsLock на клавишу.</summary>
    private const int _stateCount = 4;

    /// <summary>Переводы по индексу <c>vk * 4 + состояние</c>.</summary>
    private readonly Translation[] _entries = new Translation[_keyCount * _stateCount];

    /// <summary>Обратная карта символ → клавиша; строится при первом поиске и сбрасывается при изменении таблицы.</summary>
    private volatile Dictionary<char, KeyStroke>? _reverse;

    /// <summary>Возвращает перевод клавиши с учётом Shift и CapsLock.</summary>
    /// <param name="stroke">Клавиша с состоянием Shift и CapsLock.</param>
    /// <returns>Перевод.</returns>
    public Translation Get(KeyStroke stroke) => Get(stroke.Vk, stroke.Shift, stroke.CapsLock);

    /// <summary>Возвращает перевод клавиши для заданного состояния Shift и CapsLock.</summary>
    /// <param name="vk">Виртуальный код.</param>
    /// <param name="shift">Shift зажат.</param>
    /// <param name="capsLock">CapsLock включён.</param>
    /// <returns>Перевод.</returns>
    public Translation Get(VirtualKey vk, bool shift, bool capsLock) => _entries[Index(vk, shift, capsLock)];

    /// <summary>Задаёт перевод клавиши для заданного состояния Shift и CapsLock.</summary>
    /// <param name="vk">Виртуальный код.</param>
    /// <param name="shift">Shift зажат.</param>
    /// <param name="capsLock">CapsLock включён.</param>
    /// <param name="translation">Перевод.</param>
    public void Set(VirtualKey vk, bool shift, bool capsLock, Translation translation)
    {
        _entries[Index(vk, shift, capsLock)] = translation;
        _reverse = null;
    }

    /// <summary>
    /// Задаёт символы клавиши без Shift и с Shift; для CapsLock правило Windows: он меняет регистр только у букв,
    /// а Shift при включённом CapsLock возвращает строчную букву.
    /// </summary>
    /// <param name="vk">Виртуальный код.</param>
    /// <param name="normal">Символ без Shift.</param>
    /// <param name="shifted">Символ с Shift.</param>
    public void SetPair(VirtualKey vk, char normal, char shifted)
    {
        var letter = char.IsLetter(normal);
        Set(vk, false, false, Translation.Of(normal));
        Set(vk, true, false, Translation.Of(shifted));
        Set(vk, false, true, Translation.Of(letter ? shifted : normal));
        Set(vk, true, true, Translation.Of(letter ? normal : shifted));
    }

    /// <summary>Есть ли в таблице хотя бы одна буква: признак того, что построение из системы удалось.</summary>
    /// <returns><c>true</c>, если есть буква.</returns>
    public bool HasLetters()
    {
        foreach (var entry in _entries)
        {
            if (entry.Kind == TranslationKind.Char && char.IsLetter(entry.Char))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ищет клавишу, которая даёт символ. Предпочитает состояние без CapsLock, затем без Shift, затем меньший код клавиши.
    /// </summary>
    /// <param name="c">Символ.</param>
    /// <param name="stroke">Найденная клавиша; CapsLock всегда выключен.</param>
    /// <returns><c>true</c>, если символ есть в таблице.</returns>
    public bool TryFind(char c, out KeyStroke stroke)
    {
        var reverse = _reverse ?? BuildReverse();
        return reverse.TryGetValue(c, out stroke);
    }

    /// <summary>Строит обратную карту и сохраняет её.</summary>
    /// <returns>Карта символ → клавиша.</returns>
    private Dictionary<char, KeyStroke> BuildReverse()
    {
        var map = new Dictionary<char, KeyStroke>();
        foreach (var shift in new[] { false, true })
        {
            foreach (var vk in CharacterKeys.All)
            {
                var translation = Get(vk, shift, false);
                if (translation.Kind == TranslationKind.Char)
                {
                    map.TryAdd(translation.Char, new KeyStroke(vk, shift, false));
                }
            }
        }

        _reverse = map;
        return map;
    }

    /// <summary>Индекс записи в массиве.</summary>
    private static int Index(VirtualKey vk, bool shift, bool capsLock) =>
        (((int)vk & 0xFF) * _stateCount) + (shift ? 1 : 0) + (capsLock ? 2 : 0);
}
