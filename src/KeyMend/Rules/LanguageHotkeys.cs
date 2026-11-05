namespace KeyMend.Rules;

/// <summary>
/// Какие из сочетаний Alt+Shift и Ctrl+Shift Windows сейчас использует для смены языка или раскладки. Нужен предупреждению
/// о триггерах, которые заодно переключат язык (SPEC 5, «Поле клавиш и проверки»).
/// </summary>
/// <param name="AltShift">Левый Alt+Shift меняет язык или раскладку.</param>
/// <param name="CtrlShift">Ctrl+Shift меняет язык или раскладку.</param>
internal readonly record struct LanguageHotkeys(bool AltShift, bool CtrlShift)
{
    /// <summary>Значение реестра для смены языка по умолчанию: левый Alt+Shift.</summary>
    internal const string DefaultLanguageCode = "1";

    /// <summary>Значение реестра для смены раскладки по умолчанию: Ctrl+Shift.</summary>
    internal const string DefaultLayoutCode = "2";

    /// <summary>Код «левый Alt+Shift» в <c>HKCU\Keyboard Layout\Toggle</c>.</summary>
    internal const string AltShiftCode = "1";

    /// <summary>Код «Ctrl+Shift» в <c>HKCU\Keyboard Layout\Toggle</c>.</summary>
    internal const string CtrlShiftCode = "2";

    /// <summary>Ничего не назначено: предупреждать не о чем.</summary>
    public static LanguageHotkeys None => default;

    /// <summary>Разбирает значения реестра.</summary>
    /// <param name="language">Значение <c>Language Hotkey</c> (смена языка ввода); <c>null</c> — Windows берёт значение по умолчанию.</param>
    /// <param name="layout">Значение <c>Layout Hotkey</c> (смена раскладки внутри языка); <c>null</c> — по умолчанию.</param>
    /// <param name="severalLayoutsInOneLanguage">
    /// У какого-то языка две раскладки или больше. Сочетание смены раскладки тогда реально срабатывает; без этого
    /// оно ничего не меняет, и предупреждать о нём не нужно.
    /// </param>
    /// <returns>Какие сочетания заняты.</returns>
    public static LanguageHotkeys From(string? language, string? layout, bool severalLayoutsInOneLanguage)
    {
        var languageCode = language ?? DefaultLanguageCode;
        var layoutCode = severalLayoutsInOneLanguage ? layout ?? DefaultLayoutCode : null;
        return new LanguageHotkeys(
            languageCode == AltShiftCode || layoutCode == AltShiftCode,
            languageCode == CtrlShiftCode || layoutCode == CtrlShiftCode);
    }
}
