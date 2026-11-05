using KeyMend.Input;
using KeyMend.Native;
using KeyMend.Text;

namespace KeyMend.Layouts;

/// <summary>
/// Таблицы соответствия клавиш символам для установленных раскладок. Таблица строится из системы через
/// <c>ToUnicodeEx</c> и кэшируется по HKL; если система не дала ни одной буквы, берётся запасная таблица
/// <see cref="FallbackLayouts"/>. Вызывается из потока функций.
/// </summary>
internal sealed unsafe class KeyMap : IKeyTables
{
    /// <summary>Размер массива состояния клавиатуры для <c>ToUnicodeEx</c>.</summary>
    private const int _keyStateSize = 256;

    /// <summary>Размер буфера символов: с запасом на лигатуры; нужен только один символ.</summary>
    private const int _charBufferSize = 8;

    /// <summary>Защита кэша.</summary>
    private readonly Lock _lock = new();

    /// <summary>Готовые таблицы по HKL.</summary>
    private readonly Dictionary<nint, LayoutTable> _cache = [];

    /// <inheritdoc />
    public LayoutTable GetTable(nint hkl)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(hkl, out var cached))
            {
                return cached;
            }

            var table = BuildFromSystem(hkl);
            if (!table.HasLetters())
            {
                table = FallbackLayouts.ForLanguage((int)(hkl & 0xFFFF)) ?? table;
            }

            _cache[hkl] = table;
            return table;
        }
    }

    /// <summary>Строит таблицу через <c>ToUnicodeEx</c> для каждой символьной клавиши и четырёх состояний Shift × CapsLock.</summary>
    /// <param name="hkl">Описатель раскладки.</param>
    /// <returns>Таблица (может быть без букв, если система не отдала символы).</returns>
    internal static LayoutTable BuildFromSystem(nint hkl)
    {
        var table = new LayoutTable();
        var state = stackalloc byte[_keyStateSize];
        var buffer = stackalloc char[_charBufferSize];

        foreach (var vk in CharacterKeys.All)
        {
            var scan = User32.MapVirtualKeyEx((uint)vk, Win32.MapVkToVsc, hkl);
            if (scan == 0)
            {
                continue;
            }

            for (var shift = 0; shift < 2; shift++)
            {
                for (var caps = 0; caps < 2; caps++)
                {
                    new Span<byte>(state, _keyStateSize).Clear();
                    state[Win32.VkShift] = (byte)(shift != 0 ? 0x80 : 0);
                    state[Win32.VkCapital] = (byte)(caps != 0 ? 0x01 : 0);

                    var result = User32.ToUnicodeEx((uint)vk, scan, state, buffer, _charBufferSize, Win32.ToUnicodeKeepState, hkl);
                    var translation = result switch
                    {
                        < 0 => Translation.Dead,
                        1 => Translation.Of(buffer[0]),

                        // 0 — перевода нет; больше одного символа (лигатура, суррогатная пара) не поддерживаем.
                        _ => Translation.None,
                    };
                    table.Set(vk, shift != 0, caps != 0, translation);
                }
            }
        }

        return table;
    }
}
