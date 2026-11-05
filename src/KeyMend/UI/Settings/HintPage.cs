using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeyMend.Resources;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «Подсказка раскладки» (SPEC 5, F2.2): включение, место, длительность, «только когда язык сменил KeyMend»
/// и пример. Правки применяются и записываются сразу (SPEC F11).
/// </summary>
internal sealed class HintPage : UserControl
{
    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Показывает пример подсказки под кнопкой.</summary>
    private readonly IAppControl _app;

    /// <summary>Переключатель «Показывать подсказку раскладки».</summary>
    private readonly LabeledSwitch _enabled = new(Strings.HintEnabledTitle);

    /// <summary>Список «Где показывать»; порядок пунктов совпадает с <see cref="IndicatorPosition"/>.</summary>
    private readonly ComboBox _position = new() { MinWidth = 240 };

    /// <summary>Поле «Сколько показывать».</summary>
    private readonly NumberBox _duration = new() { Width = 80, MinHeight = 32 };

    /// <summary>Переключатель «Только когда язык сменил KeyMend».</summary>
    private readonly LabeledSwitch _onlyOwn = new(Strings.HintOnlyOwnTitle);

    /// <summary>Карточки, которые серые, пока подсказка выключена.</summary>
    private readonly List<Border> _dependent = [];

    /// <summary>Настройка выставляется из кода, а не пользователем: запись в этот момент не нужна.</summary>
    private bool _syncing;

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="app">Показ примера подсказки.</param>
    /// <param name="openLanguageRules">Открывает «Правила» с группой «Переключение языка».</param>
    public HintPage(SettingsService settings, IAppControl app, Action openLanguageRules)
    {
        _settings = settings;
        _app = app;
        Content = BuildLayout(openLanguageRules);
        Show(settings.Current.Indicator);
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            Show(_settings.Current.Indicator);
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Настройки изменили из файла или другой страницы: поля показывают актуальные значения.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => Show(settings.Indicator);

    /// <summary>Собирает страницу.</summary>
    private StackPanel BuildLayout(Action openLanguageRules)
    {
        _enabled.Toggled += () => Save(s => s with { Enabled = _enabled.IsOn });
        _onlyOwn.Toggled += () => Save(s => s with { OnlyOwnSwitch = _onlyOwn.IsOn });
        _ = _position.Items.Add(Strings.HintPositionCaret);
        _ = _position.Items.Add(Strings.HintPositionMouse);
        AutomationProperties.SetName(_position, Strings.HintPositionTitle);
        _position.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _position.SelectedIndex >= 0)
            {
                var position = (IndicatorPosition)_position.SelectedIndex;
                Save(s => s with { Position = position });
            }
        };
        AutomationProperties.SetName(_duration, Strings.HintDurationTitle);
        _duration.Committed += OnDurationCommitted;

        var duration = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        duration.Children.Add(_duration);
        var unit = KeyChipsBuilder.Label(Strings.HintDurationUnit, "Km.Text2", 13, margin: false);
        unit.Margin = new Thickness(8, 0, 0, 0);
        duration.Children.Add(unit);

        var panel = new StackPanel();
        panel.Children.Add(SettingsCards.Card(Strings.HintEnabledTitle, Strings.HintEnabledHint, _enabled, Strings.HelpHintEnabled));
        panel.Children.Add(Dependent(SettingsCards.Card(Strings.HintPositionTitle, Strings.HintPositionHint, _position, Strings.HelpHintPosition)));
        panel.Children.Add(Dependent(SettingsCards.Card(Strings.HintDurationTitle, Strings.HintDurationHint, duration, Strings.HelpHintDuration)));
        panel.Children.Add(Dependent(SettingsCards.Card(Strings.HintOnlyOwnTitle, Strings.HintOnlyOwnHint, _onlyOwn, Strings.HelpHintOnlyOwn)));
        panel.Children.Add(SampleCard());
        panel.Children.Add(SettingsCards.GroupTitle(Strings.GroupLinks));
        panel.Children.Add(Link(Strings.HintLanguageRulesLink, openLanguageRules));
        return panel;
    }

    /// <summary>Запоминает карточку, которая зависит от переключателя «Показывать».</summary>
    private Border Dependent(Border card)
    {
        _dependent.Add(card);
        return card;
    }

    /// <summary>Карточка «Как выглядит»: плашка с примером и кнопка «Показать».</summary>
    private Border SampleCard()
    {
        var plate = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x20, 0x20)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "EN", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold },
        };
        var show = new Button { Content = Strings.HintShow, Margin = new Thickness(12, 0, 0, 0), MinHeight = 32, MinWidth = 100 };
        show.Click += (_, _) => OnShowClicked(show);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(plate);
        right.Children.Add(show);
        return SettingsCards.Card(Strings.HintSampleTitle, Strings.HintSampleHint, right);
    }

    /// <summary>Просит программу показать подсказку под кнопкой.</summary>
    /// <param name="button">Кнопка «Показать».</param>
    private void OnShowClicked(Button button)
    {
        var scale = VisualTreeHelper.GetDpi(button);
        var origin = button.PointToScreen(new Point(0, 0));
        _app.ShowIndicatorSample(new Rect(origin.X, origin.Y, button.ActualWidth * scale.DpiScaleX, button.ActualHeight * scale.DpiScaleY));
    }

    /// <summary>Ссылка в нижнем блоке страницы.</summary>
    private static TextBlock Link(string text, Action click)
    {
        var link = new Hyperlink(new Run(text));
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
        link.Click += (_, _) => click();
        return new TextBlock(link);
    }

    /// <summary>Число принято: границы 200–5000 мс (SPEC 7.3), запись сразу; нечисло возвращает прежнее значение.</summary>
    private void OnDurationCommitted()
    {
        var current = _settings.Current.Indicator.DurationMs;
        var value = Math.Clamp(_duration.Value ?? current, IndicatorSettings.MinDurationMs, IndicatorSettings.MaxDurationMs);
        _duration.SetValue(value);
        if (value != current)
        {
            Save(s => s with { DurationMs = value });
        }
    }

    /// <summary>Записывает правку подсказки; при отказе записи страница возвращает прежние значения.</summary>
    /// <param name="change">Правка настроек подсказки.</param>
    private void Save(Func<IndicatorSettings, IndicatorSettings> change)
    {
        if (!_settings.Update(s => s with { Indicator = change(s.Indicator) }))
        {
            Show(_settings.Current.Indicator);
        }
    }

    /// <summary>Показывает настройки в полях, не вызывая записи.</summary>
    /// <param name="indicator">Настройки подсказки.</param>
    private void Show(IndicatorSettings indicator)
    {
        _syncing = true;
        _enabled.IsOn = indicator.Enabled;
        _onlyOwn.IsOn = indicator.OnlyOwnSwitch;
        _position.SelectedIndex = (int)indicator.Position;
        _duration.SetValue(indicator.DurationMs);
        foreach (var card in _dependent)
        {
            card.IsEnabled = indicator.Enabled;
            card.Opacity = indicator.Enabled ? 1 : 0.5;
        }

        _syncing = false;
    }
}
