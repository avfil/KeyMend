namespace KeyMend.Input;

/// <summary>Классификация клавиш, которые дают символ и потому попадают в буфер набранного (SPEC F3).</summary>
internal static class CharacterKeys
{
    /// <summary>Все символьные клавиши: пробел, цифры, буквы и клавиши OEM (знаки препинания) по возрастанию кода.</summary>
    public static IReadOnlyList<VirtualKey> All { get; } = Build();

    /// <summary>Является ли клавиша символьной. NumPad сюда не входит: он очищает буфер.</summary>
    /// <param name="vk">Виртуальный код клавиши.</param>
    /// <returns><c>true</c>, если клавиша даёт символ.</returns>
    public static bool IsCharacter(VirtualKey vk)
    {
        var code = (int)vk;
        return vk == VirtualKey.Space
            || code is >= 0x30 and <= 0x39
            || code is >= 0x41 and <= 0x5A
            || code is >= 0xBA and <= 0xC0
            || code is >= 0xDB and <= 0xDF
            || code == 0xE2;
    }

    /// <summary>Строит список всех символьных клавиш в порядке возрастания кода.</summary>
    /// <returns>Массив символьных клавиш.</returns>
    private static VirtualKey[] Build()
    {
        var keys = new List<VirtualKey>();
        for (var code = 0; code < 256; code++)
        {
            if (IsCharacter((VirtualKey)code))
            {
                keys.Add((VirtualKey)code);
            }
        }

        return [.. keys];
    }
}
