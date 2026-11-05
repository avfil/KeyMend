using System.Globalization;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Форма существительного после числа по языку интерфейса.</summary>
public sealed class UiPluralTests
{
    /// <summary>Английский: только 1 — единственное число, остальные, включая 0 и 21, — множественное.</summary>
    /// <param name="count">Число.</param>
    /// <param name="expected">Ожидаемая форма.</param>
    [Theory]
    [InlineData(0, "programs")]
    [InlineData(1, "program")]
    [InlineData(2, "programs")]
    [InlineData(5, "programs")]
    [InlineData(21, "programs")]
    public void Pick_English_UsesTwoForms(int count, string expected) =>
        Assert.Equal(expected, UiPlural.Pick(CultureInfo.GetCultureInfo("en"), count, "program", "programs", "programs"));

    /// <summary>Русский: три формы, 21 — как 1, 11 — третья.</summary>
    /// <param name="count">Число.</param>
    /// <param name="expected">Ожидаемая форма.</param>
    [Theory]
    [InlineData(1, "программа")]
    [InlineData(2, "программы")]
    [InlineData(5, "программ")]
    [InlineData(11, "программ")]
    [InlineData(21, "программа")]
    public void Pick_Russian_UsesThreeForms(int count, string expected) =>
        Assert.Equal(expected, UiPlural.Pick(CultureInfo.GetCultureInfo("ru"), count, "программа", "программы", "программ"));
}
