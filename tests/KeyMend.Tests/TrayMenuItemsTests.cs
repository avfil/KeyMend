using KeyMend.UI;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TrayMenuItems"/>: состав и порядок пунктов меню трея (SPEC F10).</summary>
public sealed class TrayMenuItemsTests
{
    /// <summary>Без таймера: порядок макета, разделители между группами.</summary>
    [Fact]
    public void Build_NoTimer_FollowsMockupOrder()
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(true, false, false, null));

        Assert.Equal(
            new TrayCommand?[]
            {
                TrayCommand.ToggleEnabled, TrayCommand.OpenSettings, null,
                TrayCommand.ToggleAutostart, TrayCommand.ToggleAdminAutostart, null,
                TrayCommand.OpenDataFolder, TrayCommand.About, null,
                TrayCommand.Exit,
            },
            entries.Select(e => e.Command));
    }

    /// <summary>С таймером: «Отменить таймер питания» третьим пунктом, вторая строка — текст таймера.</summary>
    [Fact]
    public void Build_WithTimer_AddsCancelItemWithSecondLine()
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(true, false, false, "Сон в 23:40 (через 25 мин)"));

        var cancel = entries[2];
        Assert.Equal(TrayCommand.CancelPowerTimer, cancel.Command);
        Assert.Equal("Сон в 23:40 (через 25 мин)", cancel.SecondLine);
        Assert.Equal(11, entries.Count);
    }

    /// <summary>Без таймера второй строки ни у кого нет.</summary>
    [Fact]
    public void Build_NoTimer_NoSecondLines()
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(true, false, false, null));

        Assert.All(entries, e => Assert.Null(e.SecondLine));
    }

    /// <summary>Только «Настройки…» выделен полужирным (пункт по умолчанию).</summary>
    [Fact]
    public void Build_Always_OnlySettingsIsDefault()
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(true, true, false, "Сон в 23:40 (через 25 мин)"));

        var defaults = entries.Where(e => e.IsDefault).ToList();
        Assert.Equal(TrayCommand.OpenSettings, Assert.Single(defaults).Command);
    }

    /// <summary>Галочки повторяют состояние.</summary>
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public void Build_State_SetsChecks(bool enabled, bool autostart, bool admin)
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(enabled, autostart, admin, null));

        Assert.Equal(enabled, Find(entries, TrayCommand.ToggleEnabled).IsChecked);
        Assert.Equal(autostart, Find(entries, TrayCommand.ToggleAutostart).IsChecked);
        Assert.Equal(admin, Find(entries, TrayCommand.ToggleAdminAutostart).IsChecked);
    }

    /// <summary>Обычный автозапуск недоступен, пока включён автозапуск от администратора.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Build_AdminAutostart_DisablesPlainAutostart(bool admin, bool expectedEnabled)
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(true, false, admin, null));

        Assert.Equal(expectedEnabled, Find(entries, TrayCommand.ToggleAutostart).IsEnabled);
        Assert.True(Find(entries, TrayCommand.ToggleAdminAutostart).IsEnabled);
    }

    /// <summary>Разделители не выбираются и не отмечены.</summary>
    [Fact]
    public void Build_Separators_AreNotSelectable()
    {
        var entries = TrayMenuItems.Build(new TrayMenuState(true, false, false, null));

        Assert.All(entries.Where(e => e.IsSeparator), e => Assert.False(TrayMenuNavigator.IsSelectable(e)));
    }

    /// <summary>Находит пункт по команде.</summary>
    private static TrayMenuEntry Find(IReadOnlyList<TrayMenuEntry> entries, TrayCommand command) =>
        entries.Single(e => e.Command == command);
}
