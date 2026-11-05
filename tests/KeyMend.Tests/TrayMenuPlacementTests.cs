using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TrayMenuPlacement"/>: где появляется меню трея относительно точки щелчка.</summary>
public sealed class TrayMenuPlacementTests
{
    /// <summary>Монитор 1920×1080 с панелью задач снизу (рабочая область до 1040).</summary>
    private static readonly WorkArea _bottomBar = new(0, 0, 1920, 1040);

    /// <summary>Панель снизу: нижний левый угол меню у точки.</summary>
    [Fact]
    public void Calculate_BottomBar_PlacesAboveAndRightOfPoint()
    {
        var (x, y) = TrayMenuPlacement.Calculate(1500, 1040, 288, 300, _bottomBar);

        Assert.Equal((1500, 740), (x, y));
    }

    /// <summary>Значок у правого края: меню уходит влево от точки.</summary>
    [Fact]
    public void Calculate_NearRightEdge_PlacesLeftOfPoint()
    {
        var (x, y) = TrayMenuPlacement.Calculate(1900, 1040, 288, 300, _bottomBar);

        Assert.Equal((1612, 740), (x, y));
    }

    /// <summary>Панель сверху: меню не помещается над точкой и открывается под ней.</summary>
    [Fact]
    public void Calculate_TopBar_PlacesBelowPoint()
    {
        var area = new WorkArea(0, 40, 1920, 1080);

        var (x, y) = TrayMenuPlacement.Calculate(1500, 40, 288, 300, area);

        Assert.Equal((1500, 40), (x, y));
    }

    /// <summary>Панель слева: меню прижато к рабочей области, а не к краю экрана.</summary>
    [Fact]
    public void Calculate_LeftBar_ClampsToWorkArea()
    {
        var area = new WorkArea(60, 0, 1920, 1080);

        var (x, y) = TrayMenuPlacement.Calculate(30, 1000, 288, 300, area);

        Assert.Equal(60, x);
        Assert.Equal(700, y);
    }

    /// <summary>Меню выше рабочей области прижимается к её верху.</summary>
    [Fact]
    public void Calculate_TallerThanArea_ClampsToTop()
    {
        var (_, y) = TrayMenuPlacement.Calculate(1500, 1040, 288, 2000, _bottomBar);

        Assert.Equal(0, y);
    }

    /// <summary>Второй монитор слева (отрицательные координаты) считается так же.</summary>
    [Fact]
    public void Calculate_NegativeMonitor_UsesItsWorkArea()
    {
        var area = new WorkArea(-1920, 0, 0, 1040);

        var (x, y) = TrayMenuPlacement.Calculate(-100, 1040, 288, 300, area);

        Assert.Equal((-388, 740), (x, y));
    }
}
