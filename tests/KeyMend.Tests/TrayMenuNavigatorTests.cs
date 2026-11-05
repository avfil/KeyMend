using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TrayMenuNavigator"/>: стрелки, Home и End в меню трея.</summary>
public sealed class TrayMenuNavigatorTests
{
    /// <summary>Меню: 0 пункт, 1 разделитель, 2 недоступный, 3 пункт, 4 пункт.</summary>
    private static readonly TrayMenuEntry[] _entries =
    [
        new(TrayCommand.ToggleEnabled, "a"),
        TrayMenuEntry.Separator,
        new(TrayCommand.ToggleAutostart, "b", IsEnabled: false),
        new(TrayCommand.About, "c"),
        new(TrayCommand.Exit, "d"),
    ];

    /// <summary>Из «ничего не выбрано» вниз — первый доступный пункт.</summary>
    [Fact]
    public void Next_FromNoneForward_ReturnsFirst() =>
        Assert.Equal(0, TrayMenuNavigator.Next(_entries, TrayMenuNavigator.None, forward: true));

    /// <summary>Из «ничего не выбрано» вверх — последний доступный пункт.</summary>
    [Fact]
    public void Next_FromNoneBackward_ReturnsLast() =>
        Assert.Equal(4, TrayMenuNavigator.Next(_entries, TrayMenuNavigator.None, forward: false));

    /// <summary>Разделитель и недоступный пункт пропускаются при движении вниз.</summary>
    [Fact]
    public void Next_Forward_SkipsSeparatorAndDisabled() =>
        Assert.Equal(3, TrayMenuNavigator.Next(_entries, 0, forward: true));

    /// <summary>Разделитель и недоступный пункт пропускаются при движении вверх.</summary>
    [Fact]
    public void Next_Backward_SkipsSeparatorAndDisabled() =>
        Assert.Equal(0, TrayMenuNavigator.Next(_entries, 3, forward: false));

    /// <summary>После последнего пункта выделение возвращается к первому.</summary>
    [Fact]
    public void Next_PastLast_WrapsToFirst() =>
        Assert.Equal(0, TrayMenuNavigator.Next(_entries, 4, forward: true));

    /// <summary>Перед первым пунктом выделение уходит к последнему.</summary>
    [Fact]
    public void Next_BeforeFirst_WrapsToLast() =>
        Assert.Equal(4, TrayMenuNavigator.Next(_entries, 0, forward: false));

    /// <summary>Если выбирать нечего, результат — «ничего».</summary>
    [Fact]
    public void Next_NothingSelectable_ReturnsNone()
    {
        TrayMenuEntry[] entries = [TrayMenuEntry.Separator, new(TrayCommand.Exit, "x", IsEnabled: false)];

        Assert.Equal(TrayMenuNavigator.None, TrayMenuNavigator.Next(entries, 0, forward: true));
        Assert.Equal(TrayMenuNavigator.None, TrayMenuNavigator.First(entries));
        Assert.Equal(TrayMenuNavigator.None, TrayMenuNavigator.Last(entries));
    }

    /// <summary>Пустое меню не падает.</summary>
    [Fact]
    public void Next_Empty_ReturnsNone() =>
        Assert.Equal(TrayMenuNavigator.None, TrayMenuNavigator.Next([], TrayMenuNavigator.None, forward: true));

    /// <summary>Home и End.</summary>
    [Fact]
    public void FirstAndLast_Menu_ReturnEdges()
    {
        Assert.Equal(0, TrayMenuNavigator.First(_entries));
        Assert.Equal(4, TrayMenuNavigator.Last(_entries));
    }
}
