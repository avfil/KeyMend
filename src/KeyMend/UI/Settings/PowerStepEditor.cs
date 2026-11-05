using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Редактор шага «Питание» (SPEC F4.2): действие, сейчас или через N минут, отсчёт перед выключением.</summary>
internal sealed class PowerStepEditor : StepEditor
{
    /// <summary>Способ задать время.</summary>
    private enum When
    {
        /// <summary>Сразу.</summary>
        Now,

        /// <summary>Через заданное число минут.</summary>
        After,
    }

    /// <summary>Исходный шаг.</summary>
    private readonly PowerStep _step;

    /// <summary>Включена ли гибернация в Windows.</summary>
    private readonly bool _hibernateEnabled;

    /// <summary>Действие.</summary>
    private readonly ChoiceBox<PowerAction> _action;

    /// <summary>Сейчас или через N минут.</summary>
    private readonly SegmentPicker<When> _when;

    /// <summary>Минуты до срабатывания.</summary>
    private readonly NumberBox _minutes = new() { Width = 90 };

    /// <summary>Строка «мин» рядом с полем минут.</summary>
    private readonly TextBlock _unit = KeyChipsBuilder.Label(Strings.PowerMinutes, "Km.Text2", 13, margin: false);

    /// <summary>Отсчёт с кнопкой «Отменить».</summary>
    private readonly CheckBox _countdown = new();

    /// <summary>Подпись и переключатель отсчёта: видны, только если отсчёт применим.</summary>
    private readonly IReadOnlyList<FrameworkElement> _countdownCells;

    /// <summary>Сведение про выключенную гибернацию.</summary>
    private readonly TextBlock _hibernateNote = KeyChipsBuilder.Label(Strings.PowerHibernateOff, "Km.Warning", 12, margin: false);

    /// <summary>Минуты, принятые последними.</summary>
    private int _afterMinutes;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="hibernateEnabled">Включена ли гибернация в Windows.</param>
    public PowerStepEditor(PowerStep step, bool hibernateEnabled)
    {
        _step = step;
        _hibernateEnabled = hibernateEnabled;
        _afterMinutes = step.AfterMinutes > 0 ? step.AfterMinutes : 30;
        _action = new ChoiceBox<PowerAction>(
            Strings.PowerAction,
            [
                (PowerAction.Sleep, Strings.PowerSleep),
                (PowerAction.Hibernate, Strings.PowerHibernate),
                (PowerAction.Shutdown, Strings.PowerShutdown),
                (PowerAction.Restart, Strings.PowerRestart),
                (PowerAction.Lock, Strings.PowerLock),
                (PowerAction.SignOut, Strings.PowerSignOut),
            ],
            step.Action);
        _when = new SegmentPicker<When>(Strings.PowerWhen, [(When.Now, Strings.PowerNow), (When.After, Strings.PowerAfter)], step.AfterMinutes > 0 ? When.After : When.Now);
        _minutes.SetValue(_afterMinutes);
        AutomationProperties.SetName(_minutes, Strings.PowerMinutes);
        _countdown.SetResourceReference(StyleProperty, "Km.Switch");
        _countdown.IsChecked = step.Countdown;
        AutomationProperties.SetName(_countdown, Strings.PowerCountdown);
        _hibernateNote.TextWrapping = TextWrapping.Wrap;

        _action.Changed += OnFieldChanged;
        _when.Changed += OnFieldChanged;
        _minutes.Committed += OnMinutesCommitted;
        _countdown.Click += (_, _) => RaiseChanged();

        var after = new StackPanel { Orientation = Orientation.Horizontal };
        after.Children.Add(_when);
        _minutes.Margin = new Thickness(12, 0, 8, 0);
        after.Children.Add(_minutes);
        after.Children.Add(_unit);

        var actionBox = new StackPanel();
        actionBox.Children.Add(_action);
        _hibernateNote.Margin = new Thickness(0, 6, 0, 0);
        actionBox.Children.Add(_hibernateNote);

        var form = CreateForm();
        AddRow(form, Strings.PowerAction, actionBox, help: Strings.HelpPowerAction);
        AddRow(form, Strings.PowerWhen, after, help: Strings.HelpPowerWhen);
        AddRow(form, Strings.PowerCountdown, _countdown, help: Strings.HelpPowerCountdown);
        var lastRow = form.RowDefinitions.Count - 1;
        _countdownCells = [.. form.Children.OfType<FrameworkElement>().Where(c => Grid.GetRow(c) == lastRow)];
        Content = form;
        Refresh();
    }

    /// <inheritdoc/>
    public override Step Current => _step with
    {
        Action = _action.Value,
        AfterMinutes = _when.Value == When.After ? _afterMinutes : 0,
        Countdown = _countdown.IsChecked == true,
    };

    /// <summary>Поменяли действие или «Сейчас / Через».</summary>
    private void OnFieldChanged()
    {
        Refresh();
        RaiseChanged();
    }

    /// <summary>Применяет минуты в границах 1–1440; нечисловое значение возвращает прежнее.</summary>
    private void OnMinutesCommitted()
    {
        var changed = false;
        if (_minutes.Value is { } value)
        {
            var clamped = Math.Clamp(value, 1, PowerStep.MaxAfterMinutes);
            changed = clamped != _afterMinutes;
            _afterMinutes = clamped;
        }

        _minutes.SetValue(_afterMinutes);
        if (changed)
        {
            RaiseChanged();
        }
    }

    /// <summary>Показывает поле минут при «Через», отсчёт — при выключении и перезагрузке «сейчас», сведение — при выключенной гибернации.</summary>
    private void Refresh()
    {
        var after = _when.Value == When.After;
        _minutes.Visibility = after ? Visibility.Visible : Visibility.Collapsed;
        _unit.Visibility = _minutes.Visibility;
        var countdownApplies = !after && _action.Value is PowerAction.Shutdown or PowerAction.Restart;
        foreach (var cell in _countdownCells)
        {
            cell.Visibility = countdownApplies ? Visibility.Visible : Visibility.Collapsed;
        }

        _hibernateNote.Visibility = _action.Value == PowerAction.Hibernate && !_hibernateEnabled ? Visibility.Visible : Visibility.Collapsed;
    }
}
