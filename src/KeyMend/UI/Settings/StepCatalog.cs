using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Вид шага в меню «Добавить шаг».</summary>
internal enum StepKind
{
    /// <summary>Переключить язык.</summary>
    SwitchLayout,

    /// <summary>Исправить раскладку текста.</summary>
    Correct,

    /// <summary>Запустить программу.</summary>
    Launch,

    /// <summary>Питание.</summary>
    Power,

    /// <summary>Нажать клавиши.</summary>
    Press,

    /// <summary>Пауза.</summary>
    Delay,

    /// <summary>Вставить текст.</summary>
    InsertText,

    /// <summary>Служебное действие KeyMend.</summary>
    Service,
}

/// <summary>Виды шагов, которые можно добавить в окне, и их значения по умолчанию.</summary>
internal static class StepCatalog
{
    /// <summary>Пауза нового шага «Пауза» в миллисекундах.</summary>
    internal const int DefaultDelayMs = 200;

    /// <summary>Все виды в порядке меню.</summary>
    public static IReadOnlyList<StepKind> All { get; } =
        [StepKind.SwitchLayout, StepKind.Correct, StepKind.Launch, StepKind.Power, StepKind.Press, StepKind.Delay, StepKind.InsertText, StepKind.Service];

    /// <summary>Создаёт шаг вида со значениями по умолчанию.</summary>
    /// <param name="kind">Вид шага.</param>
    /// <returns>Новый шаг; у «Нажать клавиши» клавиша не задана и шаг не пройдёт проверку, пока её не укажут.</returns>
    public static Step Create(StepKind kind) => kind switch
    {
        StepKind.SwitchLayout => new SwitchLayoutStep(),
        StepKind.Correct => new CorrectStep { Scope = CorrectScope.Selection },
        StepKind.Launch => new LaunchStep { File = string.Empty },
        StepKind.Power => new PowerStep { Action = PowerAction.Sleep },
        StepKind.Press => new PressStep { Keys = default },
        StepKind.Delay => new DelayStep { Ms = DefaultDelayMs },
        StepKind.InsertText => new InsertTextStep { Text = string.Empty },
        _ => new ServiceStep { Action = ServiceAction.TogglePause },
    };

    /// <summary>Первый шаг нового правила выбранной группы.</summary>
    /// <param name="group">Группа из «Добавить правило».</param>
    /// <returns>Шаг по умолчанию для группы.</returns>
    public static Step FirstStep(RuleGroup group) => group switch
    {
        RuleGroup.Correction => Create(StepKind.Correct),
        RuleGroup.Launch => Create(StepKind.Launch),
        RuleGroup.Power => Create(StepKind.Power),
        _ => Create(StepKind.SwitchLayout),
    };

    /// <summary>Название вида в меню.</summary>
    /// <param name="kind">Вид.</param>
    /// <returns>Название.</returns>
    public static string Title(StepKind kind) => kind switch
    {
        StepKind.SwitchLayout => Strings.StepSwitchLayout,
        StepKind.Correct => Strings.StepCorrect,
        StepKind.Launch => Strings.StepLaunch,
        StepKind.Power => Strings.StepPower,
        StepKind.Press => Strings.StepPress,
        StepKind.Delay => Strings.StepDelay,
        StepKind.InsertText => Strings.StepInsert,
        _ => Strings.StepService,
    };

    /// <summary>Вид шага.</summary>
    /// <param name="step">Шаг.</param>
    /// <returns>Вид.</returns>
    public static StepKind KindOf(Step step) => step switch
    {
        SwitchLayoutStep => StepKind.SwitchLayout,
        CorrectStep => StepKind.Correct,
        LaunchStep => StepKind.Launch,
        PowerStep => StepKind.Power,
        PressStep => StepKind.Press,
        DelayStep => StepKind.Delay,
        InsertTextStep => StepKind.InsertText,
        _ => StepKind.Service,
    };

    /// <summary>Название шага в заголовке карточки.</summary>
    /// <param name="step">Шаг.</param>
    /// <returns>Название.</returns>
    public static string Title(Step step) => Title(KindOf(step));
}
