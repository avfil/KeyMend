using System.Globalization;
using KeyMend.Storage;
using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Свёртка настройки языка интерфейса в русский или английский (SPEC F9).</summary>
public sealed class UiLanguageResolverTests
{
    /// <summary>«Как в Windows»: русский только при русской Windows, остальные языки дают английский; явный выбор от Windows не зависит.</summary>
    /// <param name="setting">Имя значения <see cref="AppLanguage"/>.</param>
    /// <param name="windows">Язык интерфейса Windows.</param>
    /// <param name="expected">Ожидаемый двухбуквенный код.</param>
    [Theory]
    [InlineData("System", "ru-RU", "ru")]
    [InlineData("System", "en-US", "en")]
    [InlineData("System", "uk-UA", "en")]
    [InlineData("System", "de-DE", "en")]
    [InlineData("Russian", "en-US", "ru")]
    [InlineData("Russian", "de-DE", "ru")]
    [InlineData("English", "ru-RU", "en")]
    [InlineData("English", "en-US", "en")]
    public void Resolve_SettingAndWindowsLanguage_MatchesExpectation(string setting, string windows, string expected) =>
        Assert.Equal(
            expected,
            UiLanguageResolver.Resolve(Enum.Parse<AppLanguage>(setting), CultureInfo.GetCultureInfo(windows)).TwoLetterISOLanguageName);
}
