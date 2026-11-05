using KeyMend.Storage;
using KeyMend.UI.Themes;

namespace KeyMend.Tests;

/// <summary>Свёртка настройки темы в светлую или тёмную (SPEC F1).</summary>
public sealed class ThemeResolverTests
{
    /// <summary>«Как в Windows» следует системе, «Светлая» и «Тёмная» от неё не зависят.</summary>
    /// <param name="theme">Имя значения <see cref="AppTheme"/>.</param>
    /// <param name="systemIsLight">Windows светлая.</param>
    /// <param name="expected">Ожидается тёмная тема.</param>
    [Theory]
    [InlineData("System", true, false)]
    [InlineData("System", false, true)]
    [InlineData("Light", false, false)]
    [InlineData("Dark", true, true)]
    public void IsDark_ThemeAndSystem_MatchesExpectation(string theme, bool systemIsLight, bool expected) =>
        Assert.Equal(expected, ThemeResolver.IsDark(Enum.Parse<AppTheme>(theme), systemIsLight));
}
