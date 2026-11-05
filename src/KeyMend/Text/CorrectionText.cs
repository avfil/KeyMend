using System.Globalization;

namespace KeyMend.Text;

/// <summary>
/// Чистые правила обработки текста, который исправление забрало из окна (SPEC F3): хвост до начала строки, пустой текст,
/// «скопирована строка целиком», число шагов каретки. Без WinAPI; содержимое никуда не пишет.
/// </summary>
internal static class CorrectionText
{
    /// <summary>Символы, которые завершают строку: LF, CR, вертикальная табуляция, FF, NEL, разделители строки и абзаца.</summary>
    private static readonly char[] _lineBreaks = ['\n', '\r', '\v', '\f', (char)0x0085, (char)0x2028, (char)0x2029];

    /// <summary>Есть ли в тексте перевод строки.</summary>
    /// <param name="text">Текст.</param>
    /// <returns><c>true</c>, если найден любой перевод строки.</returns>
    public static bool HasLineBreak(string text) => text.AsSpan().IndexOfAny(_lineBreaks) >= 0;

    /// <summary>Часть текста после последнего перевода строки: то, что находится на строке с кареткой.</summary>
    /// <param name="text">Текст от начала абзаца (или видимой строки) до каретки.</param>
    /// <returns>Хвост после последнего перевода строки; весь текст, если переводов нет.</returns>
    public static string LineTail(string text)
    {
        var index = text.AsSpan().LastIndexOfAny(_lineBreaks);
        return index < 0 ? text : text[(index + 1)..];
    }

    /// <summary>Пустой ли текст или состоит из одних пробельных символов: такое исправлять незачем.</summary>
    /// <param name="text">Текст.</param>
    /// <returns><c>true</c>, если текст пуст или из одних пробелов.</returns>
    public static bool IsBlank(string text) => string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Похоже ли, что приложение по Ctrl+C без выделения скопировало всю строку (Sublime Text, VS Code): ровно один
    /// перевод строки, и он стоит в конце. Настоящее выделение так заканчивается редко, а дубль строки при вставке
    /// испортил бы текст, поэтому в запасном пути такое исправление отменяется.
    /// </summary>
    /// <param name="text">Скопированный текст.</param>
    /// <returns><c>true</c>, если текст выглядит как скопированная строка целиком.</returns>
    public static bool LooksLikeCopiedLine(string text)
    {
        var body = text.EndsWith("\r\n", StringComparison.Ordinal) ? text[..^2]
            : text.EndsWith('\n') || text.EndsWith('\r') ? text[..^1]
            : null;
        return body is not null && !HasLineBreak(body);
    }

    /// <summary>
    /// Сколько нажатий Shift+Влево нужно, чтобы выделить текст справа налево: перевод строки CRLF, суррогатная пара и
    /// символ с комбинируемыми знаками дают один шаг.
    /// </summary>
    /// <param name="text">Текст.</param>
    /// <returns>Число шагов каретки.</returns>
    public static int CaretSteps(string text) => new StringInfo(text).LengthInTextElements;
}
