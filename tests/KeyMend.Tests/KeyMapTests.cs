using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Native;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты таблиц раскладок: запасные таблицы, обратный поиск и сверка запасных таблиц с системными (<see cref="KeyMap"/>).</summary>
public sealed class KeyMapTests
{
    /// <summary>Находит среди установленных раскладок ту, у которой младшее слово HKL равно коду языка.</summary>
    private static nint FindInstalled(int languageId)
    {
        var count = User32.GetKeyboardLayoutList(0, []);
        var list = new nint[Math.Max(count, 0)];
        User32.GetKeyboardLayoutList(list.Length, list);
        return list.FirstOrDefault(hkl => (hkl & 0xFFFF) == languageId);
    }

    /// <summary>Символьные клавиши, которые есть в раскладках US и RU: без 0xDF (OEM_8) и 0xE2 (ISO-клавиша).</summary>
    private static IEnumerable<VirtualKey> StandardKeys => CharacterKeys.All.Where(k => (int)k is not (0xDF or 0xE2));

    /// <summary>Известные клавиши запасной таблицы US.</summary>
    [Theory]
    [InlineData(0x41, false, false, 'a')]
    [InlineData(0x41, true, false, 'A')]
    [InlineData(0x41, false, true, 'A')]
    [InlineData(0x41, true, true, 'a')]
    [InlineData(0x32, true, false, '@')]
    [InlineData(0x32, true, true, '@')]
    [InlineData(0xBC, false, true, ',')]
    [InlineData(0xDB, true, false, '{')]
    public void Get_Us_KnownKeys(int vk, bool shift, bool caps, char expected) =>
        Assert.Equal(Translation.Of(expected), FallbackLayouts.Us.Get((VirtualKey)vk, shift, caps));

    /// <summary>Известные клавиши запасной таблицы ЙЦУКЕН.</summary>
    [Theory]
    [InlineData(0x47, false, false, 'п')]
    [InlineData(0x47, true, false, 'П')]
    [InlineData(0x47, false, true, 'П')]
    [InlineData(0x47, true, true, 'п')]
    [InlineData(0xBC, false, false, 'б')]
    [InlineData(0xBE, false, false, 'ю')]
    [InlineData(0xBA, false, false, 'ж')]
    [InlineData(0xDB, false, false, 'х')]
    [InlineData(0xC0, false, false, 'ё')]
    [InlineData(0xC0, false, true, 'Ё')]
    [InlineData(0xBF, false, false, '.')]
    [InlineData(0xBF, true, false, ',')]
    [InlineData(0xBC, false, true, 'Б')]
    [InlineData(0x32, true, false, '"')]
    [InlineData(0x33, true, false, '№')]
    [InlineData(0x34, true, false, ';')]
    [InlineData(0x31, false, true, '1')]
    public void Get_Ru_KnownKeys(int vk, bool shift, bool caps, char expected) =>
        Assert.Equal(Translation.Of(expected), FallbackLayouts.Ru.Get((VirtualKey)vk, shift, caps));

    /// <summary>Запасные таблицы покрывают все стандартные клавиши.</summary>
    [Fact]
    public void Fallback_EveryStandardKey_HasCharacter()
    {
        foreach (var vk in StandardKeys)
        {
            Assert.Equal(TranslationKind.Char, FallbackLayouts.Us.Get(vk, false, false).Kind);
            Assert.Equal(TranslationKind.Char, FallbackLayouts.Ru.Get(vk, false, false).Kind);
        }
    }

    /// <summary>Запасная таблица есть только для US и RU.</summary>
    [Fact]
    public void ForLanguage_KnownAndUnknown_ReturnsTableOrNull()
    {
        Assert.Same(FallbackLayouts.Us, FallbackLayouts.ForLanguage(0x0409));
        Assert.Same(FallbackLayouts.Ru, FallbackLayouts.ForLanguage(0x0419));
        Assert.Null(FallbackLayouts.ForLanguage(0x0407));
    }

    /// <summary>Обратный поиск предпочитает состояние без CapsLock и без Shift.</summary>
    [Theory]
    [InlineData('a', 0x41, false)]
    [InlineData('A', 0x41, true)]
    [InlineData('@', 0x32, true)]
    [InlineData(' ', 0x20, false)]
    public void TryFind_Us_PrefersNoCapsAndNoShift(char c, int vk, bool shift)
    {
        Assert.True(FallbackLayouts.Us.TryFind(c, out var stroke));
        Assert.Equal(new KeyStroke((VirtualKey)vk, shift, false), stroke);
    }

    /// <summary>Символа вне таблицы нет.</summary>
    [Fact]
    public void TryFind_CharacterOutsideTable_ReturnsFalse() =>
        Assert.False(FallbackLayouts.Us.TryFind('я', out _));

    /// <summary>Изменение таблицы сбрасывает обратную карту.</summary>
    [Fact]
    public void TryFind_AfterSet_SeesNewCharacter()
    {
        var table = new LayoutTable();
        Assert.False(table.TryFind('x', out _));
        table.Set((VirtualKey)0x41, false, false, Translation.Of('x'));
        Assert.True(table.TryFind('x', out var stroke));
        Assert.Equal((VirtualKey)0x41, stroke.Vk);
    }

    /// <summary>Системная таблица совпадает с запасной (пропускается, если раскладки нет).</summary>
    [Theory]
    [InlineData(0x0409)]
    [InlineData(0x0419)]
    public void BuildFromSystem_InstalledLayout_MatchesFallback(int languageId)
    {
        var hkl = FindInstalled(languageId);
        Assert.SkipWhen(hkl == 0, $"Раскладка {languageId:X4} не установлена");

        var system = KeyMap.BuildFromSystem(hkl);
        var fallback = FallbackLayouts.ForLanguage(languageId)!;
        Assert.True(system.HasLetters());

        foreach (var vk in StandardKeys)
        {
            foreach (var shift in new[] { false, true })
            {
                foreach (var caps in new[] { false, true })
                {
                    Assert.True(
                        fallback.Get(vk, shift, caps) == system.Get(vk, shift, caps),
                        $"Расхождение: VK 0x{(int)vk:X2}, shift={shift}, caps={caps}, язык {languageId:X4}");
                }
            }
        }
    }

    /// <summary>Таблица кэшируется по HKL.</summary>
    [Fact]
    public void GetTable_InstalledLayout_ReturnsCachedTable()
    {
        var hkl = FindInstalled(0x0409);
        Assert.SkipWhen(hkl == 0, "Раскладка 0409 не установлена");

        var keyMap = new KeyMap();
        var table = keyMap.GetTable(hkl);
        Assert.Same(table, keyMap.GetTable(hkl));
    }
}
