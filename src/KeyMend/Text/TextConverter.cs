using System.Text;

namespace KeyMend.Text;

/// <summary>Результат исправления готового текста.</summary>
/// <param name="Text">Исправленный текст.</param>
/// <param name="FromFirst">Исходный текст набран в первой раскладке пары, результат — во второй.</param>
internal readonly record struct TextConversion(string Text, bool FromFirst);

/// <summary>
/// Исправляет готовый (выделенный) текст, набранный не в той раскладке (SPEC F3.1в): каждый символ переводится в
/// клавишу исходной раскладки и печатается в целевой. Без WinAPI.
/// </summary>
internal static class TextConverter
{
    /// <summary>
    /// Конвертирует текст между двумя раскладками. Исходной считается та раскладка, в которой больше букв текста.
    /// Символы без клавиши (переводы строк, табуляция, эмодзи, чужие буквы) остаются как есть.
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="first">Первая раскладка пары.</param>
    /// <param name="second">Вторая раскладка пары.</param>
    /// <param name="tieFromFirst">
    /// Кто исходная раскладка, если букв поровну (но они есть): <c>true</c> — первая, <c>false</c> — вторая;
    /// обычно это текущая раскладка окна. <c>null</c> — при равенстве исправления нет.
    /// </param>
    /// <returns>
    /// Результат или <c>null</c>: нет букв, либо букв поровну и подсказки нет, либо клавиша символа в целевой раскладке
    /// не даёт символа (мёртвая клавиша) и исправление исказило бы текст.
    /// </returns>
    public static TextConversion? Convert(string text, LayoutTable first, LayoutTable second, bool? tieFromFirst = null)
    {
        var inFirst = CountLetters(text, first);
        var inSecond = CountLetters(text, second);
        bool fromFirst;
        if (inFirst != inSecond)
        {
            fromFirst = inFirst > inSecond;
        }
        else if (inFirst > 0 && tieFromFirst is { } hint)
        {
            fromFirst = hint;
        }
        else
        {
            return null;
        }

        var source = fromFirst ? first : second;
        var target = fromFirst ? second : first;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!source.TryFind(c, out var key))
            {
                builder.Append(c);
                continue;
            }

            var translation = target.Get(key);
            if (translation.Kind != TranslationKind.Char)
            {
                return null;
            }

            builder.Append(translation.Char);
        }

        return new TextConversion(builder.ToString(), fromFirst);
    }

    /// <summary>Считает буквы текста, которые есть в таблице раскладки.</summary>
    private static int CountLetters(string text, LayoutTable table)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (char.IsLetter(c) && table.TryFind(c, out _))
            {
                count++;
            }
        }

        return count;
    }
}
