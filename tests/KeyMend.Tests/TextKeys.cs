using KeyMend.Input;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Помощник тестов: набирает строку «физическими клавишами» по таблице раскладки (обратный поиск символа).</summary>
internal static class TextKeys
{
    /// <summary>
    /// Клавиши, которые дают строку <paramref name="text"/> в раскладке <paramref name="table"/>.
    /// Для символа выбирается состояние без CapsLock (сначала без Shift, затем с Shift).
    /// </summary>
    /// <param name="text">Строка.</param>
    /// <param name="table">Раскладка, в которой «набирается» строка.</param>
    /// <returns>Клавиши по порядку.</returns>
    /// <exception cref="ArgumentException">В таблице нет символа строки.</exception>
    public static KeyStroke[] Type(string text, LayoutTable table) =>
        [.. text.Select(c => Find(c, table))];

    /// <summary>Находит клавишу и состояние Shift, дающие символ <paramref name="c"/>.</summary>
    private static KeyStroke Find(char c, LayoutTable table) =>
        table.TryFind(c, out var stroke)
            ? stroke
            : throw new ArgumentException($"В таблице нет символа '{c}'");
}
