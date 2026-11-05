using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>Раздел окна настроек (SPEC 5).</summary>
internal enum SettingsSection
{
    /// <summary>Тема, язык, пауза, журнал, автозапуск.</summary>
    General,

    /// <summary>Таблица правил.</summary>
    Rules,

    /// <summary>Сокращения автозамены.</summary>
    Snippets,

    /// <summary>Подсказка раскладки.</summary>
    Hint,

    /// <summary>Исключения программ.</summary>
    Exclusions,

    /// <summary>Экспорт и импорт настроек.</summary>
    Transfer,

    /// <summary>О программе.</summary>
    About,
}

/// <summary>Пункт меню разделов.</summary>
/// <param name="Section">Раздел.</param>
/// <param name="Title">Название в меню и заголовок страницы.</param>
/// <param name="IconPath">Контур значка в виде геометрии на сетке 16×16.</param>
/// <param name="IsBottom">Пункт стоит в нижней группе меню, под распоркой.</param>
internal sealed record SectionItem(SettingsSection Section, string Title, string IconPath, bool IsBottom);

/// <summary>Порядок и состав меню разделов окна настроек (SPEC 5): шесть основных пунктов, внизу — «О программе».</summary>
internal static class SettingsSections
{
    /// <summary>Все пункты меню сверху вниз.</summary>
    /// <returns>Пункты в порядке показа.</returns>
    public static IReadOnlyList<SectionItem> All() =>
    [
        new(SettingsSection.General, Strings.SectionGeneral,
            "M8,5.5 A2.5,2.5 0 1 0 8.01,5.5 M8,1.5 V3.5 M8,12.5 V14.5 M1.5,8 H3.5 M12.5,8 H14.5 M3.4,3.4 L4.8,4.8 M11.2,11.2 L12.6,12.6 M3.4,12.6 L4.8,11.2 M11.2,4.8 L12.6,3.4", false),
        new(SettingsSection.Rules, Strings.SectionRules, "M9,1.5 L3.5,9 H8 L7,14.5 L12.5,7 H8 Z", false),
        new(SettingsSection.Snippets, Strings.SectionSnippets, "M2,3.5 H10 M6,3.5 V12.5 M9.5,10.5 H14 M12,8.5 L14,10.5 L12,12.5", false),
        new(SettingsSection.Hint, Strings.SectionHint, "M3.5,2.5 H12.5 A2,2 0 0 1 14.5,4.5 V9 A2,2 0 0 1 12.5,11 H3.5 A2,2 0 0 1 1.5,9 V4.5 A2,2 0 0 1 3.5,2.5 Z M6,11 L8,13.5 L10,11 M5,6.8 H11", false),
        new(SettingsSection.Exclusions, Strings.SectionExclusions, "M8,2 A6,6 0 1 0 8.01,2 M3.8,3.8 L12.2,12.2", false),
        new(SettingsSection.Transfer, Strings.SectionTransfer, "M8,2 V10 M5,7 L8,10 L11,7 M2.5,11 V13.5 H13.5 V11", false),
        new(SettingsSection.About, Strings.SectionAbout, "M8,2 A6,6 0 1 0 8.01,2 M8,7.2 V11.2 M8,4.8 V5", true),
    ];
}
