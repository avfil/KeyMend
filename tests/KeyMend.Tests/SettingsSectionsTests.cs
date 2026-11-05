using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Меню разделов окна настроек (SPEC 5) и строка состояния над ним.</summary>
public sealed class SettingsSectionsTests
{
    /// <summary>Порядок меню: шесть основных пунктов, внизу — «О программе».</summary>
    [Fact]
    public void All_Default_HasSpecOrderWithAboutAtBottom()
    {
        var items = SettingsSections.All();

        Assert.Equal(
            ["General", "Rules", "Snippets", "Hint", "Exclusions", "Transfer", "About"],
            items.Select(i => i.Section.ToString()).ToArray());
        Assert.Equal(["About"], items.Where(i => i.IsBottom).Select(i => i.Section.ToString()).ToArray());
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Title)));
    }

    /// <summary>Обычная работа: голубая точка, логотип обычный.</summary>
    [Fact]
    public void From_EnabledNotElevated_IsRunning()
    {
        var view = AppStatusView.From(enabled: true, elevated: false);

        Assert.Equal(StatusDot.Running, view.Dot);
        Assert.False(view.Greyed);
        Assert.False(view.Shield);
    }

    /// <summary>Администратор: красная точка и щит.</summary>
    [Fact]
    public void From_EnabledElevated_IsAdminWithShield()
    {
        var view = AppStatusView.From(enabled: true, elevated: true);

        Assert.Equal(StatusDot.Admin, view.Dot);
        Assert.True(view.Shield);
    }

    /// <summary>Пауза важнее администратора в тексте и точке, щит остаётся.</summary>
    [Fact]
    public void From_PausedElevated_IsPausedButKeepsShield()
    {
        var view = AppStatusView.From(enabled: false, elevated: true);

        Assert.Equal(StatusDot.Paused, view.Dot);
        Assert.True(view.Greyed);
        Assert.True(view.Shield);
        Assert.Equal(AppStatusView.From(enabled: false, elevated: false).Text, view.Text);
    }
}
