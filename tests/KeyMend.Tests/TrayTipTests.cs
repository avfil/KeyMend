using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TrayTip"/>: подсказка значка в трее в несколько строк.</summary>
public sealed class TrayTipTests
{
    /// <summary>Обычная работа — одна строка с именем.</summary>
    [Fact]
    public void Build_Normal_IsNameOnly() =>
        Assert.Equal("KeyMend", TrayTip.Build(paused: false, elevated: false, powerTimer: null));

    /// <summary>Администратор меняет первую строку.</summary>
    [Fact]
    public void Build_Elevated_ChangesFirstLine() =>
        Assert.Equal("KeyMend · администратор", TrayTip.Build(paused: false, elevated: true, powerTimer: null));

    /// <summary>Пауза — вторая строка.</summary>
    [Fact]
    public void Build_Paused_AddsSecondLine() =>
        Assert.Equal("KeyMend\nПауза", TrayTip.Build(paused: true, elevated: false, powerTimer: null));

    /// <summary>Таймер питания — отдельная строка.</summary>
    [Fact]
    public void Build_Timer_AddsLine() =>
        Assert.Equal("KeyMend\nСон в 23:40 (через 25 мин)", TrayTip.Build(false, false, "Сон в 23:40 (через 25 мин)"));

    /// <summary>Всё сразу: имя с режимом, пауза, таймер.</summary>
    [Fact]
    public void Build_Everything_KeepsOrder() =>
        Assert.Equal(
            "KeyMend · администратор\nПауза\nСон в 23:40 (через 25 мин)",
            TrayTip.Build(true, true, "Сон в 23:40 (через 25 мин)"));
}
