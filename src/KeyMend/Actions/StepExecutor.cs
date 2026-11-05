using KeyMend.Infrastructure;
using KeyMend.Layouts;
using KeyMend.Rules;

namespace KeyMend.Actions;

/// <summary>
/// Выполняет шаги цепочек (SPEC 3.2). Сейчас: «Переключить язык», исправление выделенного и до начала строки, «Вставить текст», «Нажать», «Пауза», «Запустить программу», «Питание», служебные
/// шаги; остальные виды добавляются на своих этапах. В лог идут только коды языков и имена правил, не ввод.
/// </summary>
/// <param name="layouts">Сервис раскладок.</param>
/// <param name="notifier">Получает сообщение о своей смене раскладки (подсказка).</param>
/// <param name="commands">Действия приложения для служебных шагов.</param>
/// <param name="corrector">Исправление выделенного текста и текста до начала строки.</param>
/// <param name="inputSteps">Шаги, вводящие текст и клавиши: «Вставить текст» и «Нажать».</param>
/// <param name="launcher">Шаг «Запустить программу».</param>
/// <param name="power">Шаг «Питание» и таймер питания.</param>
/// <param name="time">Источник времени для шага «Пауза».</param>
/// <param name="log">Журнал.</param>
internal sealed class StepExecutor(
    ILayoutService layouts,
    ILayoutNotifier notifier,
    IAppCommands commands,
    ITextCorrector corrector,
    IInputStepRunner inputSteps,
    ILaunchStepRunner launcher,
    IPowerService power,
    TimeProvider time,
    ILog log) : IStepExecutor
{
    /// <inheritdoc />
    public async Task ExecuteAsync(Rule rule, Step step, CancellationToken cancellationToken)
    {
        switch (step)
        {
            case SwitchLayoutStep switchLayout:
                await SwitchLayoutAsync(rule, switchLayout, cancellationToken);
                break;
            case CorrectStep correct:
                await corrector.CorrectAsync(correct, cancellationToken);
                break;
            case InsertTextStep insert:
                await inputSteps.InsertAsync(rule, insert, cancellationToken);
                break;
            case PressStep press:
                await inputSteps.PressAsync(rule, press, cancellationToken);
                break;
            case LaunchStep launch:
                await launcher.LaunchAsync(rule, launch, cancellationToken);
                break;
            case PowerStep powerStep:
                await power.RunAsync(rule, powerStep, cancellationToken);
                break;
            case DelayStep delay:
                await Task.Delay(TimeSpan.FromMilliseconds(delay.Ms), time, cancellationToken);
                break;
            case ServiceStep { Action: ServiceAction.TogglePause }:
                commands.TogglePause();
                break;
            case ServiceStep { Action: ServiceAction.CancelPowerTimer }:
                _ = power.CancelTimer();
                break;
            case ServiceStep { Action: ServiceAction.OpenSettings }:
                commands.OpenSettings();
                break;
            default:
                log.Error($"rule '{rule.Name}': step {step.GetType().Name} is not supported yet");
                break;
        }
    }

    /// <summary>Переключает раскладку окна с фокусом по режиму шага; неверные теги языков пишутся в лог.</summary>
    /// <param name="rule">Правило (для сообщений).</param>
    /// <param name="step">Шаг.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task SwitchLayoutAsync(Rule rule, SwitchLayoutStep step, CancellationToken cancellationToken)
    {
        var before = layouts.GetCurrent();
        LayoutInfo? after;
        if (step.Mode == SwitchMode.Specific)
        {
            if (!LayoutTags.TryGetLanguageId(step.Layout, out var language))
            {
                log.Error($"rule '{rule.Name}': layout tag is invalid");
                return;
            }

            after = layouts.SwitchTo(language);
        }
        else
        {
            if (step.Pair.Count != 2
                || !LayoutTags.TryGetLanguageId(step.Pair[0], out var a)
                || !LayoutTags.TryGetLanguageId(step.Pair[1], out var b))
            {
                log.Error($"rule '{rule.Name}': layout pair is invalid");
                return;
            }

            after = layouts.SwitchPair(a, b);
        }

        if (after is null)
        {
            log.Info("layout switch skipped");
            return;
        }

        log.Info($"layout switched {before?.Code ?? "?"} -> {after.Code}");
        await notifier.OnOwnSwitchAsync(after, cancellationToken);
    }
}
