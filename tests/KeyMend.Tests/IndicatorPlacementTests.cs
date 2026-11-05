using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="IndicatorPlacement"/>: под кареткой, над ней у нижнего края, за панелью задач, у краёв экрана.</summary>
public sealed class IndicatorPlacementTests
{
    /// <summary>Ширина подсказки в тестах.</summary>
    internal const int W = 60;

    /// <summary>Высота подсказки в тестах.</summary>
    internal const int H = 38;

    /// <summary>Рабочая область 1920×1040: панель задач (40 px) занимает низ экрана 1080.</summary>
    private static readonly WorkArea _area = new(0, 0, 1920, 1040);

    /// <summary>Создаёт привязку к каретке.</summary>
    private static Anchor Caret(int x, int top, int bottom) => new(x, top, bottom, AnchorSource.Caret);

    /// <summary>Каретка с запасом места снизу: подсказка под кареткой.</summary>
    [Fact]
    public void Calculate_CaretWithRoomBelow_GoesBelowCaret()
    {
        var (x, y) = IndicatorPlacement.Calculate(Caret(500, 400, 423), W, H, _area);

        Assert.Equal(500, x);
        Assert.Equal(423 + IndicatorPlacement.CaretGap, y);
    }

    /// <summary>Каретка у панели задач: подсказка над кареткой, чтобы не закрыть текст.</summary>
    [Fact]
    public void Calculate_CaretNearBottom_GoesAboveCaret()
    {
        var (_, y) = IndicatorPlacement.Calculate(Caret(500, 1000, 1023), W, H, _area);

        Assert.Equal(1000 - IndicatorPlacement.CaretGap - H, y);
    }

    /// <summary>Нижняя граница подсказки с отступом ровно на краю: остаётся снизу.</summary>
    [Fact]
    public void Calculate_CaretExactlyFitsBelow_StaysBelow()
    {
        var bottom = _area.Bottom - IndicatorPlacement.ScreenMargin - H - IndicatorPlacement.CaretGap;
        var (_, y) = IndicatorPlacement.Calculate(Caret(500, bottom - 23, bottom), W, H, _area);

        Assert.Equal(bottom + IndicatorPlacement.CaretGap, y);
    }

    /// <summary>Окно утащили вниз, каретка за панелью задач: подсказка остаётся над панелью.</summary>
    [Fact]
    public void Calculate_CaretHiddenBehindTaskbar_StaysAboveTaskbar()
    {
        var (_, y) = IndicatorPlacement.Calculate(Caret(500, 1055, 1078), W, H, _area);

        Assert.Equal(_area.Bottom - H - IndicatorPlacement.ScreenMargin, y);
    }

    /// <summary>Нет места ни снизу, ни сверху: подсказка зажимается внутрь области.</summary>
    [Fact]
    public void Calculate_NoRoomAboveEither_IsClampedInsideArea()
    {
        var tiny = new WorkArea(0, 0, 800, 50);
        var (_, y) = IndicatorPlacement.Calculate(Caret(100, 20, 40), W, H, tiny);

        Assert.InRange(y, tiny.Top, tiny.Bottom - IndicatorPlacement.ScreenMargin);
    }

    /// <summary>Каретка у правого края: подсказка сдвигается влево.</summary>
    [Fact]
    public void Calculate_CaretAtRightEdge_IsShiftedLeft()
    {
        var (x, _) = IndicatorPlacement.Calculate(Caret(1915, 400, 423), W, H, _area);

        Assert.Equal(_area.Right - W - IndicatorPlacement.ScreenMargin, x);
    }

    /// <summary>Каретка левее области: подсказка остаётся внутри.</summary>
    [Fact]
    public void Calculate_CaretAtLeftEdge_IsKeptInsideArea()
    {
        var (x, _) = IndicatorPlacement.Calculate(Caret(-30, 400, 423), W, H, _area);

        Assert.Equal(_area.Left, x);
    }

    /// <summary>Мышь: подсказка смещена вправо и вниз, чтобы не закрыть курсор.</summary>
    [Fact]
    public void Calculate_Mouse_IsOffsetRightAndDown()
    {
        var mouse = new Anchor(300, 300, 300, AnchorSource.Mouse);
        var (x, y) = IndicatorPlacement.Calculate(mouse, W, H, _area);

        Assert.Equal(300 + IndicatorPlacement.MouseOffset, x);
        Assert.Equal(300 + IndicatorPlacement.MouseOffset, y);
    }

    /// <summary>Мышь у нижнего края: подсказка над указателем.</summary>
    [Fact]
    public void Calculate_MouseNearBottom_GoesAbovePointer()
    {
        var mouse = new Anchor(300, 1030, 1030, AnchorSource.Mouse);
        var (_, y) = IndicatorPlacement.Calculate(mouse, W, H, _area);

        Assert.Equal(1030 - IndicatorPlacement.CaretGap - H, y);
    }

    /// <summary>Окно оболочки («Пуск», поиск) закрывает подсказку: она ставится над его верхним краем.</summary>
    [Fact]
    public void Calculate_ShellOverlay_PutsHintAboveItsTopEdge()
    {
        var search = new WorkArea(0, 440, 640, 1040);
        var anchor = new Anchor(100, 980, 1003, AnchorSource.Uia, search);
        var (x, y) = IndicatorPlacement.Calculate(anchor, W, H, _area);

        Assert.Equal(100, x);
        Assert.Equal(search.Top - IndicatorPlacement.CaretGap - H, y);
    }

    /// <summary>Сверху от окна оболочки нет места: подсказка под его нижним краем.</summary>
    [Fact]
    public void Calculate_ShellOverlayNoRoomAbove_GoesBelowItsBottomEdge()
    {
        var panel = new WorkArea(0, 10, 640, 700);
        var anchor = new Anchor(100, 300, 323, AnchorSource.Uia, panel);
        var (_, y) = IndicatorPlacement.Calculate(anchor, W, H, _area);

        Assert.Equal(panel.Bottom + IndicatorPlacement.CaretGap, y);
    }

    /// <summary>Привязка вне окна оболочки: по горизонтали остаёмся в его ширине.</summary>
    [Fact]
    public void Calculate_ShellOverlayCaretOutsidePanel_XIsKeptInsidePanelWidth()
    {
        var panel = new WorkArea(100, 440, 740, 1040);
        var anchor = new Anchor(1500, 400, 423, AnchorSource.Mouse, panel);
        var (x, _) = IndicatorPlacement.Calculate(anchor, W, H, _area);

        Assert.Equal(panel.Right - W, x);
    }

    /// <summary>Второй монитор с отрицательными координатами.</summary>
    [Fact]
    public void Calculate_SecondMonitorWithNegativeCoordinates_Works()
    {
        var left = new WorkArea(-1920, 0, 0, 1040);
        var (x, y) = IndicatorPlacement.Calculate(Caret(-500, 400, 423), W, H, left);

        Assert.Equal(-500, x);
        Assert.Equal(427, y);
    }
}
