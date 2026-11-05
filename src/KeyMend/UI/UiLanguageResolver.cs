using System.Globalization;
using KeyMend.Storage;

namespace KeyMend.UI;

/// <summary>Свёртка настройки языка интерфейса в конкретную культуру: русскую или английскую (SPEC F9).</summary>
internal static class UiLanguageResolver
{
    /// <summary>Русская культура интерфейса.</summary>
    private static readonly CultureInfo _russian = CultureInfo.GetCultureInfo("ru");

    /// <summary>Английская культура интерфейса.</summary>
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    /// <summary>Определяет культуру строк интерфейса.</summary>
    /// <param name="setting">Настройка пользователя.</param>
    /// <param name="windowsUi">Язык интерфейса Windows.</param>
    /// <returns>Русская культура, если выбран русский или «Как в Windows» при русской Windows; иначе английская.</returns>
    public static CultureInfo Resolve(AppLanguage setting, CultureInfo windowsUi) => setting switch
    {
        AppLanguage.Russian => _russian,
        AppLanguage.English => _english,
        _ => windowsUi.TwoLetterISOLanguageName == "ru" ? _russian : _english,
    };
}
