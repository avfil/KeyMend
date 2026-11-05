using System.Globalization;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.Actions;

/// <summary>Текст про активный таймер питания для меню и подсказки значка (SPEC F10).</summary>
internal static class PowerTimerText
{
    /// <summary>Формирует текст вида «Сон в 23:40 (через 25 мин)».</summary>
    /// <param name="action">Действие таймера.</param>
    /// <param name="due">Момент срабатывания.</param>
    /// <param name="now">Текущий момент.</param>
    /// <param name="zone">Часовой пояс, в котором показывается время.</param>
    /// <returns>Строка с заглавной первой буквой; остаток меньше минуты — «менее чем через минуту».</returns>
    public static string Format(PowerAction action, DateTimeOffset due, DateTimeOffset now, TimeZoneInfo zone)
    {
        var name = PowerService.Name(action);
        var title = PowerService.Capitalize(name);
        var at = TimeZoneInfo.ConvertTime(due, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
        var minutes = (int)Math.Ceiling((due - now).TotalMinutes);
        return minutes <= 1
            ? Strings.Fill(Strings.PowerTimerSoon, title, at)
            : Strings.Fill(Strings.PowerTimerAt, title, at, minutes.ToString(CultureInfo.InvariantCulture));
    }
}
