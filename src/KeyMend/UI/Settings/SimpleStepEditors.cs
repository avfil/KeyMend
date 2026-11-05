using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Редактор шага «Пауза» (SPEC 3.2): число миллисекунд.</summary>
internal sealed class DelayStepEditor : StepEditor
{
    /// <summary>Исходный шаг.</summary>
    private readonly DelayStep _step;

    /// <summary>Поле миллисекунд.</summary>
    private readonly NumberBox _ms = new() { Width = 110 };

    /// <summary>Миллисекунды, принятые последними.</summary>
    private int _value;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    public DelayStepEditor(DelayStep step)
    {
        _step = step;
        _value = step.Ms;
        _ms.SetValue(_value);
        AutomationProperties.SetName(_ms, Strings.DelayMs);
        _ms.Committed += OnCommitted;

        var unit = KeyChipsBuilder.Label(Strings.DelayUnit, "Km.Text2", 13, margin: false);
        unit.Margin = new Thickness(8, 0, 0, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_ms);
        row.Children.Add(unit);
        var form = CreateForm();
        AddRow(form, Strings.DelayMs, row, help: Strings.HelpDelay);
        Content = form;
    }

    /// <inheritdoc/>
    public override Step Current => _step with { Ms = _value };

    /// <summary>Применяет паузу в границах 0–60 000; нечисловое значение возвращает прежнее.</summary>
    private void OnCommitted()
    {
        var changed = false;
        if (_ms.Value is { } value)
        {
            var clamped = Math.Clamp(value, 0, DelayStep.MaxMs);
            changed = clamped != _value;
            _value = clamped;
        }

        _ms.SetValue(_value);
        if (changed)
        {
            RaiseChanged();
        }
    }
}

/// <summary>Редактор шага «Вставить текст» (SPEC F5.3): текст, способ вставки, перенос строки.</summary>
internal sealed class InsertTextStepEditor : StepEditor
{
    /// <summary>Исходный шаг.</summary>
    private readonly InsertTextStep _step;

    /// <summary>Текст.</summary>
    private readonly TextBox _text = new()
    {
        MinHeight = 64,
        MinWidth = 320,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalContentAlignment = VerticalAlignment.Top,
    };

    /// <summary>Способ вставки.</summary>
    private readonly SegmentPicker<InsertMode> _mode;

    /// <summary>Перенос строки.</summary>
    private readonly SegmentPicker<NewlineMode> _newline;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    public InsertTextStepEditor(InsertTextStep step)
    {
        _step = step;
        var form = CreateForm();
        if (step.SecretRef is not null)
        {
            // Секретный текст в правилах не бывает, но шаг из файла не должен ломаться: показываем его без полей.
            _mode = new SegmentPicker<InsertMode>(Strings.InsertHow, [(InsertMode.Clipboard, Strings.InsertClipboard)], InsertMode.Clipboard);
            _newline = new SegmentPicker<NewlineMode>(Strings.InsertNewline, [(NewlineMode.ShiftEnter, Strings.NewlineShiftEnter)], NewlineMode.ShiftEnter);
            AddRow(form, Strings.InsertText, KeyChipsBuilder.Label(Strings.InsertSecret, "Km.Text2", 13, margin: false));
            Content = form;
            return;
        }

        _text.Text = step.Text ?? string.Empty;
        AutomationProperties.SetName(_text, Strings.InsertText);
        _mode = new SegmentPicker<InsertMode>(
            Strings.InsertHow, [(InsertMode.Type, Strings.InsertType), (InsertMode.Clipboard, Strings.InsertClipboard)], step.Insert);
        _newline = new SegmentPicker<NewlineMode>(
            Strings.InsertNewline, [(NewlineMode.ShiftEnter, Strings.NewlineShiftEnter), (NewlineMode.Enter, Strings.NewlineEnter)], step.Newline);
        _text.TextChanged += (_, _) => RaiseChanged();
        _mode.Changed += RaiseChanged;
        _newline.Changed += RaiseChanged;

        AddRow(form, Strings.InsertText, _text, top: true, stretch: true);
        AddRow(form, Strings.InsertHow, _mode, help: Strings.HelpInsertHow);
        AddRow(form, Strings.InsertNewline, _newline, help: Strings.HelpInsertNewline);
        Content = form;
    }

    /// <inheritdoc/>
    public override Step Current => _step.SecretRef is not null
        ? _step
        : _step with { Text = _text.Text, Insert = _mode.Value, Newline = _newline.Value };
}

/// <summary>Редактор шага «Служебное действие»: пауза KeyMend, открыть настройки, отменить таймер питания.</summary>
internal sealed class ServiceStepEditor : StepEditor
{
    /// <summary>Исходный шаг.</summary>
    private readonly ServiceStep _step;

    /// <summary>Действие.</summary>
    private readonly ChoiceBox<ServiceAction> _action;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    public ServiceStepEditor(ServiceStep step)
    {
        _step = step;
        _action = new ChoiceBox<ServiceAction>(
            Strings.ServiceAction,
            [
                (ServiceAction.TogglePause, Strings.SummaryServicePause),
                (ServiceAction.OpenSettings, Strings.SummaryServiceOpen),
                (ServiceAction.CancelPowerTimer, Strings.SummaryServiceCancelTimer),
            ],
            step.Action);
        _action.Changed += RaiseChanged;
        var form = CreateForm();
        AddRow(form, Strings.ServiceAction, _action, help: Strings.HelpServiceAction);
        Content = form;
    }

    /// <inheritdoc/>
    public override Step Current => _step with { Action = _action.Value };
}
