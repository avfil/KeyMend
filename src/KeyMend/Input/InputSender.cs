using KeyMend.Infrastructure;
using KeyMend.Native;
using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>
/// Отправка синтетического ввода через <c>SendInput</c>. Каждое событие помечается <see cref="Signature"/> в
/// <c>dwExtraInfo</c>; хук всё равно игнорирует любой инжектированный ввод. Вызывается из потока функций, не из колбэка хука.
/// </summary>
/// <param name="log">Журнал; пишутся только числа, не ввод.</param>
internal sealed unsafe class InputSender(ILog log) : IInputSender
{
    /// <summary>Метка нашего ввода в <c>dwExtraInfo</c> (ASCII «KMND»).</summary>
    internal const nuint Signature = 0x4B4D4E44;

    /// <inheritdoc />
    public void SendMask(VirtualKey release)
    {
        Span<NativeInput> buffer = stackalloc NativeInput[3];
        buffer[0] = Key(VirtualKey.Mask, isUp: false);
        buffer[1] = Key(VirtualKey.Mask, isUp: true);
        var count = 2;
        if (release != VirtualKey.None)
        {
            buffer[count++] = Key(release, isUp: true);
        }

        _ = Send(buffer[..count]);
    }

    /// <inheritdoc />
    public bool SendChord(VirtualKey modifier, VirtualKey key)
    {
        Span<NativeInput> buffer =
        [
            Key(modifier, isUp: false),
            Key(key, isUp: false),
            Key(key, isUp: true),
            Key(modifier, isUp: true),
        ];
        return Send(buffer);
    }

    /// <summary>Сколько событий отправлять за один вызов <c>SendInput</c>: длинная фраза иначе переполнит очередь окна.</summary>
    internal const int MaxBatch = 400;

    /// <inheritdoc />
    public bool SendTyped(int backspaces, string text, NewlineMode newline)
    {
        var inputs = new List<NativeInput>((backspaces + text.Length) * 2);
        for (var i = 0; i < backspaces; i++)
        {
            inputs.Add(Key(VirtualKey.Back, isUp: false));
            inputs.Add(Key(VirtualKey.Back, isUp: true));
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                // «\r\n» — один перевод строки.
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                AddNewline(inputs, newline);
                continue;
            }

            inputs.Add(UnicodeChar(c, isUp: false));
            inputs.Add(UnicodeChar(c, isUp: true));
        }

        return SendBatched([.. inputs], inputs.Count);
    }

    /// <inheritdoc />
    public bool SendKeys(KeyChord chord)
    {
        var modifiers = ExpandModifiers(chord.Modifiers);
        var inputs = new List<NativeInput>((modifiers.Length * 2) + 2);
        foreach (var modifier in modifiers)
        {
            inputs.Add(Key(modifier, isUp: false));
        }

        inputs.Add(Key(chord.Key, isUp: false));
        inputs.Add(Key(chord.Key, isUp: true));
        for (var i = modifiers.Length - 1; i >= 0; i--)
        {
            inputs.Add(Key(modifiers[i], isUp: true));
        }

        return Send([.. inputs]);
    }

    /// <summary>Превращает набор модификаторов в клавиши: сторонние биты — как есть, «любой» — левая клавиша семейства, если сторонней нет.</summary>
    /// <param name="set">Набор модификаторов.</param>
    /// <returns>Клавиши в порядке Ctrl, Shift, Alt, Win.</returns>
    internal static VirtualKey[] ExpandModifiers(ModifierSet set)
    {
        var keys = new List<VirtualKey>();
        AddFamily(keys, set, ModifierSet.LCtrl | ModifierSet.RCtrl, ModifierSet.AnyCtrl, VirtualKey.LControl);
        AddFamily(keys, set, ModifierSet.LShift | ModifierSet.RShift, ModifierSet.AnyShift, VirtualKey.LShift);
        AddFamily(keys, set, ModifierSet.LAlt | ModifierSet.RAlt, ModifierSet.AnyAlt, VirtualKey.LMenu);
        AddFamily(keys, set, ModifierSet.LWin | ModifierSet.RWin, ModifierSet.AnyWin, VirtualKey.LWin);
        return [.. keys];
    }

    /// <summary>Добавляет клавиши одного семейства модификаторов.</summary>
    /// <param name="keys">Приёмник.</param>
    /// <param name="set">Весь набор.</param>
    /// <param name="sided">Сторонние биты семейства.</param>
    /// <param name="any">Бит «любой» семейства.</param>
    /// <param name="left">Клавиша для «любой».</param>
    private static void AddFamily(List<VirtualKey> keys, ModifierSet set, ModifierSet sided, ModifierSet any, VirtualKey left)
    {
        var present = set & sided;
        if (present == ModifierSet.None)
        {
            if ((set & any) != 0)
            {
                keys.Add(left);
            }

            return;
        }

        foreach (var key in ModifierSets.SidedKeys)
        {
            if ((present & ModifierSets.FromKey(key)) != 0)
            {
                keys.Add(key);
            }
        }
    }

    /// <summary>Добавляет перевод строки: Enter или Shift+Enter.</summary>
    /// <param name="inputs">Приёмник событий.</param>
    /// <param name="newline">Способ.</param>
    private static void AddNewline(List<NativeInput> inputs, NewlineMode newline)
    {
        var shift = newline == NewlineMode.ShiftEnter;
        if (shift)
        {
            inputs.Add(Key(VirtualKey.LShift, isUp: false));
        }

        inputs.Add(Key(VirtualKey.Return, isUp: false));
        inputs.Add(Key(VirtualKey.Return, isUp: true));
        if (shift)
        {
            inputs.Add(Key(VirtualKey.LShift, isUp: true));
        }
    }

    /// <inheritdoc />
    public bool SendKeyRepeat(VirtualKey key, int count, bool shift)
    {
        if (count <= 0)
        {
            return true;
        }

        var inputs = new NativeInput[(count * 2) + (shift ? 2 : 0)];
        var length = 0;
        if (shift)
        {
            inputs[length++] = Key(VirtualKey.LShift, isUp: false);
        }

        for (var i = 0; i < count; i++)
        {
            inputs[length++] = Key(key, isUp: false);
            inputs[length++] = Key(key, isUp: true);
        }

        if (shift)
        {
            inputs[length++] = Key(VirtualKey.LShift, isUp: true);
        }

        return SendBatched(inputs, length);
    }

    /// <summary>Отправляет события порциями по <see cref="MaxBatch"/>; останавливается на первой неудаче.</summary>
    /// <param name="inputs">События.</param>
    /// <param name="count">Сколько первых событий отправить.</param>
    /// <returns><c>false</c>, если система приняла не все события.</returns>
    private bool SendBatched(NativeInput[] inputs, int count)
    {
        for (var offset = 0; offset < count; offset += MaxBatch)
        {
            if (!Send(inputs.AsSpan(offset, Math.Min(MaxBatch, count - offset))))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Собирает запись нажатия или отпускания символа Unicode (<c>KEYEVENTF_UNICODE</c>): код символа идёт в скан-код.</summary>
    /// <param name="c">Символ.</param>
    /// <param name="isUp"><c>true</c> — отпускание.</param>
    /// <returns>Запись для <c>SendInput</c>.</returns>
    private static NativeInput UnicodeChar(char c, bool isUp) => new()
    {
        Type = Win32.InputKeyboard,
        U = new InputUnion
        {
            Ki = new KeybdInput
            {
                WScan = c,
                DwFlags = Win32.KeyEventFUnicode | (isUp ? Win32.KeyEventFKeyUp : 0u),
                DwExtraInfo = Signature,
            },
        },
    };

    /// <summary>Собирает запись нажатия или отпускания клавиши.</summary>
    /// <param name="key">Клавиша.</param>
    /// <param name="isUp"><c>true</c> — отпускание.</param>
    /// <returns>Запись для <c>SendInput</c>.</returns>
    private static NativeInput Key(VirtualKey key, bool isUp)
    {
        var flags = (isUp ? Win32.KeyEventFKeyUp : 0u) | (IsExtended(key) ? Win32.KeyEventFExtendedKey : 0u);
        return new NativeInput
        {
            Type = Win32.InputKeyboard,
            U = new InputUnion
            {
                Ki = new KeybdInput { WVk = (ushort)key, DwFlags = flags, DwExtraInfo = Signature },
            },
        };
    }

    /// <summary>
    /// Расширенные клавиши (правые Ctrl/Alt, Win, стрелки и блок навигации) надо отправлять с флагом
    /// <c>KEYEVENTF_EXTENDEDKEY</c>: иначе стрелки и Home станут клавишами цифрового блока.
    /// </summary>
    /// <param name="key">Клавиша.</param>
    /// <returns><c>true</c> для расширенных клавиш.</returns>
    private static bool IsExtended(VirtualKey key) =>
        key is VirtualKey.RControl or VirtualKey.RMenu or VirtualKey.LWin or VirtualKey.RWin
            or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down
            or VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown
            or VirtualKey.Insert or VirtualKey.Delete;

    /// <summary>Отправляет события и пишет в лог, если система приняла не все (например, из-за блокировки ввода).</summary>
    /// <param name="inputs">События.</param>
    /// <returns><c>true</c>, если система приняла все события.</returns>
    private bool Send(ReadOnlySpan<NativeInput> inputs)
    {
        var sent = User32.SendInput((uint)inputs.Length, inputs, sizeof(NativeInput));
        if (sent != inputs.Length)
        {
            log.Error($"SendInput accepted {sent} of {inputs.Length} events");
            return false;
        }

        return true;
    }
}
