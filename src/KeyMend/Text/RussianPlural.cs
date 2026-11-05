namespace KeyMend.Text;

/// <summary>Выбор формы существительного после числа по правилам русского языка.</summary>
internal static class RussianPlural
{
    /// <summary>Выбирает форму: 1 программа, 2 программы, 5 программ; 11–14 — всегда третья форма.</summary>
    /// <param name="count">Число.</param>
    /// <param name="one">Форма для 1, 21, 31… («программа»).</param>
    /// <param name="few">Форма для 2–4, 22–24… («программы»).</param>
    /// <param name="many">Форма для 0, 5–20, 25–30… («программ»).</param>
    /// <returns>Подходящая форма.</returns>
    public static string Pick(int count, string one, string few, string many)
    {
        var n = Math.Abs(count);
        var lastTwo = n % 100;
        if (lastTwo is >= 11 and <= 14)
        {
            return many;
        }

        return (n % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }
}
