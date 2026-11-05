using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>Цвет точки состояния в меню окна.</summary>
internal enum StatusDot
{
    /// <summary>Работает в обычном режиме: голубая.</summary>
    Running,

    /// <summary>На паузе: серая.</summary>
    Paused,

    /// <summary>Работает от администратора: красная.</summary>
    Admin,
}

/// <summary>Состояние программы, как оно показывается над меню разделов и на логотипе.</summary>
/// <param name="Text">Строка состояния.</param>
/// <param name="Dot">Цвет точки.</param>
/// <param name="Greyed">Логотип серый (программа выключена).</param>
/// <param name="Shield">На логотипе красный щит (режим администратора, SPEC F10).</param>
internal sealed record AppStatusView(string Text, StatusDot Dot, bool Greyed, bool Shield)
{
    /// <summary>Строит вид по состоянию программы.</summary>
    /// <param name="enabled">KeyMend включён (не на паузе).</param>
    /// <param name="elevated">Работает от администратора.</param>
    /// <returns>Вид состояния. Пауза важнее администратора в тексте и точке, но щит остаётся: режим от паузы не меняется.</returns>
    public static AppStatusView From(bool enabled, bool elevated) => !enabled
        ? new AppStatusView(Strings.StatusPaused, StatusDot.Paused, Greyed: true, Shield: elevated)
        : elevated
            ? new AppStatusView(Strings.StatusWorkingAdmin, StatusDot.Admin, Greyed: false, Shield: true)
            : new AppStatusView(Strings.StatusWorking, StatusDot.Running, Greyed: false, Shield: false);
}
