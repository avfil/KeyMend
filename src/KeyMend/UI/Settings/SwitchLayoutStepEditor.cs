using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Редактор шага «Переключить язык» (SPEC 5): по очереди между двумя языками или всегда один.</summary>
internal sealed class SwitchLayoutStepEditor : StepEditor
{
    /// <summary>Исходный шаг: из него берутся поля, которых в окне нет.</summary>
    private readonly SwitchLayoutStep _step;

    /// <summary>Режим «по очереди».</summary>
    private readonly RadioButton _pairMode = new() { Content = Strings.SwitchPair };

    /// <summary>Режим «всегда один».</summary>
    private readonly RadioButton _oneMode = new() { Content = Strings.SwitchOne };

    /// <summary>Пара языков режима «по очереди».</summary>
    private readonly LanguagePairPicker _pair = new() { Margin = new Thickness(28, 6, 0, 8) };

    /// <summary>Язык режима «всегда один».</summary>
    private readonly ComboBox _one = new() { MinWidth = 180, MinHeight = 32, Margin = new Thickness(28, 6, 0, 0), DisplayMemberPath = nameof(LanguageChoice.Name), SelectedValuePath = nameof(LanguageChoice.Tag) };

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="installed">Теги установленных языков.</param>
    /// <param name="openHint">Открывает раздел «Подсказка раскладки».</param>
    public SwitchLayoutStepEditor(SwitchLayoutStep step, IReadOnlyList<string> installed, Action openHint)
    {
        _step = step;
        _pairMode.GroupName = _oneMode.GroupName = Guid.NewGuid().ToString("N");
        var first = step.Pair.Count > 0 ? step.Pair[0] : "ru-RU";
        var second = step.Pair.Count > 1 ? step.Pair[1] : "en-US";
        var one = step.Layout ?? first;
        var choices = LanguageChoices.Build(installed, [first, second, one]);

        _pair.Load(choices, first, second, Strings.CorrectLanguages, Strings.CorrectLanguages);
        _one.ItemsSource = choices;
        _one.SelectedItem = choices.FirstOrDefault(c => string.Equals(c.Tag, one, StringComparison.OrdinalIgnoreCase));
        _pairMode.IsChecked = step.Mode == SwitchMode.Pair;
        _oneMode.IsChecked = step.Mode == SwitchMode.Specific;
        ShowMode();

        _pairMode.Checked += OnModeChecked;
        _oneMode.Checked += OnModeChecked;
        _pair.Changed += OnFieldChanged;
        _one.SelectionChanged += (_, _) => OnFieldChanged();

        var how = new StackPanel();
        how.Children.Add(_pairMode);
        how.Children.Add(_pair);
        how.Children.Add(_oneMode);
        how.Children.Add(_one);

        var hint = new StackPanel { Orientation = Orientation.Horizontal };
        hint.Children.Add(KeyChipsBuilder.Label(Strings.SwitchHintText, "Km.Text2", 13));
        hint.Children.Add(KeyChipsBuilder.Label("·", "Km.Text2", 13));
        var link = new Hyperlink(new Run(Strings.SwitchHintLink));
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
        link.Click += (_, _) => openHint();
        hint.Children.Add(new TextBlock(link) { VerticalAlignment = VerticalAlignment.Center });

        var form = CreateForm();
        AddRow(form, Strings.SwitchHow, how, top: true, help: Strings.HelpSwitchHow);
        AddRow(form, Strings.SwitchHintLabel, hint);
        Content = form;
    }

    /// <inheritdoc/>
    public override Step Current
    {
        get
        {
            var (a, b) = _pair.Selected;
            var specific = _oneMode.IsChecked == true;
            return _step with
            {
                Mode = specific ? SwitchMode.Specific : SwitchMode.Pair,
                Pair = [a ?? _step.Pair.ElementAtOrDefault(0) ?? "ru-RU", b ?? _step.Pair.ElementAtOrDefault(1) ?? "en-US"],
                Layout = specific ? (_one.SelectedValue as string ?? _step.Layout) : _step.Layout,
            };
        }
    }

    /// <summary>Показывает список того режима, который выбран.</summary>
    private void ShowMode()
    {
        var specific = _oneMode.IsChecked == true;
        _pair.Visibility = specific ? Visibility.Collapsed : Visibility.Visible;
        _one.Visibility = specific ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Смена режима.</summary>
    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        ShowMode();
        RaiseChanged();
    }

    /// <summary>Выбран другой язык.</summary>
    private void OnFieldChanged()
    {
        RaiseChanged();
    }
}
