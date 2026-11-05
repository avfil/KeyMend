using KeyMend.Resources;

namespace KeyMend.UI;

/// <summary>Строка меню трея: пункт или разделитель.</summary>
/// <param name="Command">Команда пункта; у разделителя <c>null</c>.</param>
/// <param name="Text">Текст пункта; у разделителя пустой.</param>
/// <param name="SecondLine">Вторая строка мелким серым шрифтом или <c>null</c>.</param>
/// <param name="IsChecked">Пункт отмечен галочкой.</param>
/// <param name="IsEnabled">Пункт можно выбрать.</param>
/// <param name="IsDefault">Пункт по умолчанию (двойной клик по значку): рисуется полужирным.</param>
internal sealed record TrayMenuEntry(
    TrayCommand? Command,
    string Text,
    string? SecondLine = null,
    bool IsChecked = false,
    bool IsEnabled = true,
    bool IsDefault = false)
{
    /// <summary>Строка — разделитель, а не пункт.</summary>
    public bool IsSeparator => Command is null;

    /// <summary>Разделитель групп пунктов.</summary>
    public static TrayMenuEntry Separator { get; } = new(null, string.Empty, IsEnabled: false);
}

/// <summary>Состав меню трея по текущему состоянию программы (SPEC F10). Чистая функция без WPF.</summary>
internal static class TrayMenuItems
{
    /// <summary>Собирает пункты меню в порядке макета.</summary>
    /// <param name="state">Состояние переключателей и таймера.</param>
    /// <returns>Пункты и разделители сверху вниз.</returns>
    public static IReadOnlyList<TrayMenuEntry> Build(TrayMenuState state)
    {
        var entries = new List<TrayMenuEntry>
        {
            new(TrayCommand.ToggleEnabled, Strings.TrayEnabled, IsChecked: state.Enabled),
            new(TrayCommand.OpenSettings, Strings.TraySettings, IsDefault: true),
        };

        if (state.PowerTimer is not null)
        {
            entries.Add(new TrayMenuEntry(TrayCommand.CancelPowerTimer, Strings.TrayCancelTimer, SecondLine: state.PowerTimer));
        }

        entries.Add(TrayMenuEntry.Separator);
        entries.Add(new TrayMenuEntry(
            TrayCommand.ToggleAutostart, Strings.TrayAutostart, IsChecked: state.AutostartOn, IsEnabled: !state.AdminAutostartOn));
        entries.Add(new TrayMenuEntry(TrayCommand.ToggleAdminAutostart, Strings.TrayAdminAutostart, IsChecked: state.AdminAutostartOn));
        entries.Add(TrayMenuEntry.Separator);
        entries.Add(new TrayMenuEntry(TrayCommand.OpenDataFolder, Strings.TrayOpenDataFolder));
        entries.Add(new TrayMenuEntry(TrayCommand.About, Strings.TrayAbout));
        entries.Add(TrayMenuEntry.Separator);
        entries.Add(new TrayMenuEntry(TrayCommand.Exit, Strings.TrayExit));
        return entries;
    }
}
