using KeyMend.Actions;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="PowerTimerText"/>: вторая строка пункта меню и строка подсказки значка (SPEC F10).</summary>
public sealed class PowerTimerTextTests
{
    /// <summary>Начало отсчёта для всех тестов.</summary>
    private static readonly DateTimeOffset _now = new(2026, 10, 24, 23, 15, 0, TimeSpan.Zero);

    /// <summary>Остаток в минутах: время срабатывания и «через N мин».</summary>
    [Fact]
    public void Format_Minutes_ShowsTimeAndRemainder()
    {
        var text = PowerTimerText.Format(PowerAction.Sleep, _now.AddMinutes(25), _now, TimeZoneInfo.Utc);

        Assert.Equal("Сон в 23:40 (через 25 мин)", text);
    }

    /// <summary>Неполная минута округляется вверх.</summary>
    [Fact]
    public void Format_PartialMinute_RoundsUp()
    {
        var text = PowerTimerText.Format(PowerAction.Lock, _now.AddSeconds(150), _now, TimeZoneInfo.Utc);

        Assert.Equal("Блокировка в 23:17 (через 3 мин)", text);
    }

    /// <summary>Меньше минуты — отдельная формулировка.</summary>
    [Theory]
    [InlineData(60)]
    [InlineData(20)]
    [InlineData(0)]
    public void Format_LessThanMinute_SaysLessThanMinute(int seconds)
    {
        var text = PowerTimerText.Format(PowerAction.Restart, _now.AddSeconds(seconds), _now, TimeZoneInfo.Utc);

        Assert.EndsWith("(менее чем через минуту)", text, StringComparison.Ordinal);
    }

    /// <summary>Время показывается в заданном часовом поясе, а остаток от него не зависит.</summary>
    [Fact]
    public void Format_OtherZone_ConvertsClockTime()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(3), "test", "test");

        var text = PowerTimerText.Format(PowerAction.Shutdown, _now.AddMinutes(45), _now, zone);

        Assert.Equal("Выключение в 03:00 (через 45 мин)", text);
    }

    /// <summary>Первая буква названия действия всегда заглавная.</summary>
    [Theory]
    [InlineData("Sleep", "Сон")]
    [InlineData("Hibernate", "Гибернация")]
    [InlineData("SignOut", "Выход из системы")]
    public void Format_AnyAction_StartsWithCapital(string action, string title)
    {
        var text = PowerTimerText.Format(Enum.Parse<PowerAction>(action), _now.AddMinutes(10), _now, TimeZoneInfo.Utc);

        Assert.StartsWith(title + " в ", text, StringComparison.Ordinal);
    }
}
