using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Редактор шага «Исправить раскладку текста» (SPEC 5): что исправлять, языки, переключение языка, предел длины.</summary>
internal sealed class CorrectStepEditor : StepEditor
{
    /// <summary>Исходный шаг: из него берётся <c>caretAfter</c>, которого в окне нет.</summary>
    private readonly CorrectStep _step;

    /// <summary>Исправлять выделенный текст.</summary>
    private readonly RadioButton _selection = new() { Content = Strings.CorrectSelection };

    /// <summary>Исправлять до начала строки.</summary>
    private readonly RadioButton _line = new() { Content = Strings.CorrectLine };

    /// <summary>Пара языков.</summary>
    private readonly LanguagePairPicker _pair = new();

    /// <summary>Переключать язык после исправления.</summary>
    private readonly CheckBox _switch = new();

    /// <summary>Предел длины текста.</summary>
    private readonly NumberBox _max = new() { Width = 110 };

    /// <summary>Предел длины, принятый последним.</summary>
    private int _maxChars;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="installed">Теги установленных языков.</param>
    public CorrectStepEditor(CorrectStep step, IReadOnlyList<string> installed)
    {
        _step = step;
        _selection.GroupName = _line.GroupName = Guid.NewGuid().ToString("N");
        _maxChars = step.MaxChars;
        var first = step.Pair.Count > 0 ? step.Pair[0] : "ru-RU";
        var second = step.Pair.Count > 1 ? step.Pair[1] : "en-US";

        _selection.Style = (Style)FindResource("Km.Segment");
        _line.Style = (Style)FindResource("Km.Segment");
        _line.Margin = new Thickness(4, 0, 0, 0);
        _selection.IsChecked = step.Scope == CorrectScope.Selection;
        _line.IsChecked = step.Scope == CorrectScope.Line;
        _pair.Load(LanguageChoices.Build(installed, [first, second]), first, second, Strings.CorrectLanguages, Strings.CorrectLanguages);
        _switch.Style = (Style)FindResource("Km.Switch");
        _switch.IsChecked = step.SwitchLayout;
        AutomationProperties.SetName(_switch, Strings.CorrectSwitchAfter);
        _max.SetValue(_maxChars);
        AutomationProperties.SetName(_max, Strings.CorrectMax);

        _selection.Checked += OnFieldChanged;
        _line.Checked += OnFieldChanged;
        _pair.Changed += RaiseChanged;
        _switch.Click += OnFieldChanged;
        _max.Committed += OnMaxCommitted;

        var scope = new StackPanel { Orientation = Orientation.Horizontal };
        scope.Children.Add(_selection);
        scope.Children.Add(_line);
        var unit = KeyChipsBuilder.Label(Strings.CorrectChars, "Km.Text2", 13, margin: false);
        unit.Margin = new Thickness(8, 0, 0, 0);
        var limit = new StackPanel { Orientation = Orientation.Horizontal };
        limit.Children.Add(_max);
        limit.Children.Add(unit);

        var form = CreateForm();
        AddRow(form, Strings.CorrectWhat, scope, help: Strings.HelpCorrectWhat);
        AddRow(form, Strings.CorrectLanguages, _pair, help: Strings.HelpCorrectLanguages);
        AddRow(form, Strings.CorrectSwitchAfter, _switch, help: Strings.HelpCorrectSwitchAfter);
        AddRow(form, Strings.CorrectMax, limit, help: Strings.HelpCorrectMax);
        Content = form;
    }

    /// <inheritdoc/>
    public override Step Current
    {
        get
        {
            var (a, b) = _pair.Selected;
            return _step with
            {
                Scope = _line.IsChecked == true ? CorrectScope.Line : CorrectScope.Selection,
                Pair = [a ?? _step.Pair.ElementAtOrDefault(0) ?? "ru-RU", b ?? _step.Pair.ElementAtOrDefault(1) ?? "en-US"],
                SwitchLayout = _switch.IsChecked == true,
                MaxChars = _maxChars,
            };
        }
    }

    /// <summary>Поменяли сегмент или переключатель.</summary>
    private void OnFieldChanged(object sender, RoutedEventArgs e)
    {
        RaiseChanged();
    }

    /// <summary>Применяет предел длины в допустимых границах; нечисловое значение возвращает прежнее.</summary>
    private void OnMaxCommitted()
    {
        var changed = false;
        if (_max.Value is { } value)
        {
            var clamped = Math.Clamp(value, CorrectStep.MinChars, CorrectStep.MaxCharsLimit);
            changed = clamped != _maxChars;
            _maxChars = clamped;
        }

        _max.SetValue(_maxChars);
        if (changed)
        {
            RaiseChanged();
        }
    }
}
