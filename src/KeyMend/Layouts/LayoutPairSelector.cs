namespace KeyMend.Layouts;

/// <summary>Чистая логика выбора раскладки (SPEC F2.1): какой из установленных описателей включить. Без WinAPI.</summary>
internal static class LayoutPairSelector
{
    /// <summary>Идентификатор языка раскладки — младшее слово HKL (например, 0x0419 для русской).</summary>
    /// <param name="hkl">Описатель раскладки.</param>
    /// <returns>LANGID.</returns>
    public static int LanguageOf(nint hkl) => (int)(hkl & 0xFFFF);

    /// <summary>
    /// Выбирает цель для режима «между двумя выбранными»: текущая A — включаем B, текущая B — включаем A, любая другая —
    /// включаем A (а если A не установлена, то B).
    /// </summary>
    /// <param name="installed">Установленные раскладки.</param>
    /// <param name="current">Текущая раскладка окна.</param>
    /// <param name="languageA">Язык A пары.</param>
    /// <param name="languageB">Язык B пары.</param>
    /// <returns>HKL цели или 0, если включать нечего.</returns>
    public static nint PickPair(IReadOnlyList<nint> installed, nint current, int languageA, int languageB)
    {
        var currentLanguage = LanguageOf(current);
        if (currentLanguage == languageA)
        {
            return FirstOf(installed, languageB);
        }

        if (currentLanguage == languageB)
        {
            return FirstOf(installed, languageA);
        }

        var a = FirstOf(installed, languageA);
        return a != 0 ? a : FirstOf(installed, languageB);
    }

    /// <summary>Выбирает раскладку для режима «включить конкретную».</summary>
    /// <param name="installed">Установленные раскладки.</param>
    /// <param name="language">Нужный язык.</param>
    /// <returns>HKL первой установленной раскладки этого языка или 0, если такой нет.</returns>
    public static nint PickSpecific(IReadOnlyList<nint> installed, int language) => FirstOf(installed, language);

    /// <summary>Первая установленная раскладка языка.</summary>
    /// <param name="installed">Установленные раскладки.</param>
    /// <param name="language">Язык.</param>
    /// <returns>HKL или 0.</returns>
    private static nint FirstOf(IReadOnlyList<nint> installed, int language)
    {
        foreach (var hkl in installed)
        {
            if (LanguageOf(hkl) == language)
            {
                return hkl;
            }
        }

        return 0;
    }
}
