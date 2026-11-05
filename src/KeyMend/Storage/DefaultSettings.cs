using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.Storage;

/// <summary>Настройки первого запуска: правила языка, исправление выделенного и до начала строки (SPEC F3), исключение для RDP (SPEC F5.5, F7).</summary>
internal static class DefaultSettings
{
    /// <summary>Сколько мс даётся на сборку жеста исправления: две клавиши нажимаются как попало (SPEC 3.1, п. 7).</summary>
    private const int _correctionHoldMs = 300;

    /// <summary>Создаёт настройки по умолчанию.</summary>
    /// <returns>Новый набор настроек.</returns>
    public static AppSettings Create() => new()
    {
        Rules =
        [
            new Rule
            {
                Id = "lang",
                Name = Strings.DefaultRuleLang,
                Trigger = new TapTrigger { Keys = ModifierSet.RCtrl },
                Steps = [new SwitchLayoutStep()],
            },
            new Rule
            {
                Id = "lang-work",
                Name = Strings.DefaultRuleLangWork,
                Enabled = false,
                Trigger = new TapTrigger { Keys = ModifierSet.RCtrl, DoubleMs = 200 },
                Steps = [new SwitchLayoutStep()],
            },
            new Rule
            {
                Id = "fix-selection",
                Name = Strings.DefaultRuleFixSelection,
                Trigger = new TapTrigger { Keys = ModifierSet.RShift, MaxHoldMs = _correctionHoldMs },
                Steps = [new CorrectStep { Scope = CorrectScope.Selection }],
            },
            new Rule
            {
                Id = "fix-line",
                Name = Strings.DefaultRuleFixLine,
                Trigger = new TapTrigger { Keys = ModifierSet.RShift, Hold = ModifierSet.RCtrl, MaxHoldMs = _correctionHoldMs },
                Steps = [new CorrectStep { Scope = CorrectScope.Line }],
            },
        ],
        Exclusions = [new ExclusionEntry { Process = "mstsc.exe", LayoutSwitch = false, Correction = false }],
    };
}
