using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Группа правил в таблице «Правила» (SPEC 5). Определяется первым шагом цепочки.</summary>
internal enum RuleGroup
{
    /// <summary>Переключение языка.</summary>
    Language,

    /// <summary>Исправление ввода.</summary>
    Correction,

    /// <summary>Запуск программ.</summary>
    Launch,

    /// <summary>Питание.</summary>
    Power,

    /// <summary>Всё остальное: нажатие клавиш, паузы, служебные шаги, пустая цепочка.</summary>
    Other,
}

/// <summary>Порядок и названия групп таблицы правил.</summary>
internal static class RuleGroups
{
    /// <summary>Все группы в порядке показа.</summary>
    public static IReadOnlyList<RuleGroup> All { get; } = [RuleGroup.Language, RuleGroup.Correction, RuleGroup.Launch, RuleGroup.Power, RuleGroup.Other];

    /// <summary>Определяет группу правила по первому шагу. Не то же самое, что <see cref="RuleFilter.CategoryOf"/>: там для исключений смотрят все шаги.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Группа правила.</returns>
    public static RuleGroup Of(Rule rule) => rule.Steps.Count == 0
        ? RuleGroup.Other
        : rule.Steps[0] switch
        {
            SwitchLayoutStep => RuleGroup.Language,
            CorrectStep => RuleGroup.Correction,
            LaunchStep => RuleGroup.Launch,
            PowerStep => RuleGroup.Power,
            _ => RuleGroup.Other,
        };

    /// <summary>Название группы для чипа и заголовка.</summary>
    /// <param name="group">Группа.</param>
    /// <returns>Название.</returns>
    public static string Title(RuleGroup group) => group switch
    {
        RuleGroup.Language => Strings.RuleGroupLanguage,
        RuleGroup.Correction => Strings.RuleGroupCorrection,
        RuleGroup.Launch => Strings.RuleGroupLaunch,
        RuleGroup.Power => Strings.RuleGroupPower,
        _ => Strings.RuleGroupOther,
    };
}
