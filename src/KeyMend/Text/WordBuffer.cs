using KeyMend.Input;

namespace KeyMend.Text;

/// <summary>
/// Буфер последних набранных символьных клавиш (SPEC F3): кольцевой, ёмкость задаётся один раз, поэтому
/// <see cref="Push"/> и <see cref="Backspace"/> не выделяют память (вызываются из колбэка хука). Хранит только клавиши
/// в оперативной памяти и не больше ёмкости; при переполнении старые клавиши отбрасываются. Без WinAPI.
/// </summary>
internal sealed class WordBuffer
{
    /// <summary>Клавиши по кольцу; самая старая лежит в <see cref="_start"/>.</summary>
    private readonly KeyStroke[] _keys;

    /// <summary>Индекс самой старой клавиши.</summary>
    private int _start;

    /// <summary>Создаёт пустой буфер.</summary>
    /// <param name="capacity">Сколько последних клавиш хранить (больше нуля).</param>
    /// <exception cref="ArgumentOutOfRangeException">Ёмкость меньше единицы.</exception>
    public WordBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _keys = new KeyStroke[capacity];
        StartsAtBoundary = true;
    }

    /// <summary>Сколько клавиш сейчас в буфере.</summary>
    public int Count { get; private set; }

    /// <summary>Ёмкость буфера.</summary>
    public int Capacity => _keys.Length;

    /// <summary>
    /// Известно ли, что перед самой старой клавишей буфера текст не продолжается (буфер очищен). Становится
    /// <c>false</c>, когда старая клавиша вытеснена или Backspace стёр символ, которого в буфере нет.
    /// </summary>
    public bool StartsAtBoundary { get; private set; }

    /// <summary>Возвращает клавишу, отсчитывая от самой новой, без выделения памяти.</summary>
    /// <param name="offset">0 — последняя набранная клавиша, 1 — предпоследняя и т. д.</param>
    /// <returns>Клавиша.</returns>
    /// <exception cref="ArgumentOutOfRangeException">В буфере меньше клавиш.</exception>
    public KeyStroke FromEnd(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, Count);
        return _keys[(_start + Count - 1 - offset) % _keys.Length];
    }

    /// <summary>Добавляет клавишу; если буфер полон, самая старая отбрасывается.</summary>
    /// <param name="key">Нажатая клавиша.</param>
    public void Push(KeyStroke key)
    {
        if (Count == _keys.Length)
        {
            _keys[_start] = key;
            _start = (_start + 1) % _keys.Length;
            StartsAtBoundary = false;
            return;
        }

        _keys[(_start + Count) % _keys.Length] = key;
        Count++;
    }

    /// <summary>Удаляет последнюю клавишу (Backspace); в пустом буфере стёртый символ неизвестен, поэтому граница теряется.</summary>
    public void Backspace()
    {
        if (Count > 0)
        {
            Count--;
        }
        else
        {
            StartsAtBoundary = false;
        }
    }

    /// <summary>Очищает буфер.</summary>
    public void Clear()
    {
        _start = 0;
        Count = 0;
        StartsAtBoundary = true;
    }

    /// <summary>
    /// Копия содержимого от самой старой клавиши к самой новой. Единственная аллокация буфера: вызывается один раз
    /// при срабатывании правила исправления, а не на каждое нажатие.
    /// </summary>
    /// <returns>Новый массив клавиш.</returns>
    public KeyStroke[] Snapshot()
    {
        var result = new KeyStroke[Count];
        for (var i = 0; i < Count; i++)
        {
            result[i] = _keys[(_start + i) % _keys.Length];
        }

        return result;
    }
}
