using KeyMend.Resources;

namespace KeyMend.UI;

/// <summary>Подсказка значка в трее (SPEC F10): первая строка — имя и режим, дальше пауза и таймер питания. Чистая функция.</summary>
internal static class TrayTip
{
    /// <summary>Собирает текст подсказки.</summary>
    /// <param name="paused">KeyMend на паузе.</param>
    /// <param name="elevated">Программа работает от администратора.</param>
    /// <param name="powerTimer">Описание активного таймера питания или <c>null</c>.</param>
    /// <returns>Строки через перевод строки.</returns>
    public static string Build(bool paused, bool elevated, string? powerTimer)
    {
        var lines = new List<string> { elevated ? Strings.TrayTipAdmin : Strings.AppName };
        if (paused)
        {
            lines.Add(Strings.TrayTipPaused);
        }

        if (powerTimer is not null)
        {
            lines.Add(powerTimer);
        }

        return string.Join('\n', lines);
    }
}
