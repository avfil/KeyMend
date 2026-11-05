namespace KeyMend.UI.Settings;

/// <summary>Кусок текста подсказки «?»: обычный, полужирный или перенос строки.</summary>
/// <param name="Text">Текст куска; у переноса строки пустой.</param>
/// <param name="Bold">Выделен ли кусок полужирным.</param>
/// <param name="LineBreak">Кусок — перенос строки внутри абзаца.</param>
internal sealed record HelpRun(string Text, bool Bold = false, bool LineBreak = false);

/// <summary>Абзац подсказки «?».</summary>
/// <param name="Runs">Куски абзаца по порядку.</param>
internal sealed record HelpParagraph(IReadOnlyList<HelpRun> Runs);

/// <summary>
/// Разбор текста подсказки из ресурсов (SPEC F9). Пустая строка начинает новый абзац, одиночный перевод строки — перенос
/// внутри абзаца, <c>**текст**</c> — полужирный. Так тексты остаются простыми строками в <c>.resx</c>.
/// </summary>
internal static class HelpText
{
    /// <summary>Маркер полужирного.</summary>
    private const string _boldMark = "**";

    /// <summary>Разбирает текст подсказки.</summary>
    /// <param name="text">Текст из ресурсов.</param>
    /// <returns>Абзацы; пустой текст даёт пустой список.</returns>
    public static IReadOnlyList<HelpParagraph> Parse(string text)
    {
        var paragraphs = new List<HelpParagraph>();
        var lines = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                Flush(paragraphs, lines);
            }
            else
            {
                lines.Add(line);
            }
        }

        Flush(paragraphs, lines);
        return paragraphs;
    }

    /// <summary>Убирает разметку: текст для читалок экрана.</summary>
    /// <param name="text">Текст из ресурсов.</param>
    /// <returns>Абзацы через пробел, без <c>**</c>.</returns>
    public static string Plain(string text) =>
        string.Join(' ', Parse(text).Select(p => string.Concat(p.Runs.Select(r => r.LineBreak ? " " : r.Text))));

    /// <summary>Превращает накопленные строки в абзац.</summary>
    private static void Flush(List<HelpParagraph> paragraphs, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var runs = new List<HelpRun>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                runs.Add(new HelpRun(string.Empty, LineBreak: true));
            }

            AddLine(runs, lines[i]);
        }

        paragraphs.Add(new HelpParagraph(runs));
        lines.Clear();
    }

    /// <summary>Режет строку по парам <c>**</c>; непарный маркер остаётся обычным текстом.</summary>
    private static void AddLine(List<HelpRun> runs, string line)
    {
        var pos = 0;
        while (pos < line.Length)
        {
            var open = line.IndexOf(_boldMark, pos, StringComparison.Ordinal);
            var close = open < 0 ? -1 : line.IndexOf(_boldMark, open + _boldMark.Length, StringComparison.Ordinal);
            if (close < 0)
            {
                runs.Add(new HelpRun(line[pos..]));
                return;
            }

            if (open > pos)
            {
                runs.Add(new HelpRun(line[pos..open]));
            }

            runs.Add(new HelpRun(line[(open + _boldMark.Length)..close], Bold: true));
            pos = close + _boldMark.Length;
        }
    }
}
