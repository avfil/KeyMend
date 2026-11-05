using System.Globalization;

namespace KeyMend.Text;

/// <summary>Выбор формы существительного после числа по правилам языка интерфейса (SPEC F9).</summary>
internal static class UiPlural
{
    /// <summary>Выбирает форму: у русского три формы, у английского две (1 — единственное, остальное — множественное).</summary>
    /// <param name="culture">Язык интерфейса.</param>
    /// <param name="count">Число.</param>
    /// <param name="one">Форма для 1 («программа», «program»).</param>
    /// <param name="few">Форма для 2–4 по русским правилам; в английском не используется.</param>
    /// <param name="many">Форма для остальных чисел («программ», «programs»).</param>
    /// <returns>Подходящая форма.</returns>
    public static string Pick(CultureInfo culture, int count, string one, string few, string many) =>
        culture.TwoLetterISOLanguageName == "ru"
            ? RussianPlural.Pick(count, one, few, many)
            : Math.Abs(count) == 1 ? one : many;
}
