using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using KeyMend.Resources;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «Общие» (SPEC 5): тема, язык интерфейса, общая пауза, журнал работы, автозапуск и ссылки на файлы настроек. Правки применяются сразу
/// (SPEC F11).
/// </summary>
internal sealed class GeneralPage : UserControl
{
    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Пауза, автозапуск, открытие файлов.</summary>
    private readonly IAppControl _app;

    /// <summary>Папка настроек для ссылки «Открыть папку с настройками».</summary>
    private readonly string _dataDir;

    /// <summary>Список тем; порядок пунктов совпадает с <see cref="AppTheme"/>.</summary>
    private readonly ComboBox _theme = new() { MinWidth = 200, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Список языков интерфейса; порядок пунктов совпадает с <see cref="AppLanguage"/>.</summary>
    private readonly ComboBox _language = new() { MinWidth = 200, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Переключатель «KeyMend включён».</summary>
    private readonly LabeledSwitch _enabled = new(Strings.GeneralEnabledTitle);

    /// <summary>Переключатель «Вести журнал работы».</summary>
    private readonly LabeledSwitch _log = new(Strings.GeneralLogTitle);

    /// <summary>Переключатель «Запускать вместе с Windows».</summary>
    private readonly LabeledSwitch _autostart = new(Strings.GeneralAutostartTitle);

    /// <summary>Карточка обычного автозапуска: серая, пока включён автозапуск от администратора.</summary>
    private readonly Border _autostartCard;

    /// <summary>Кнопка «Включить…» / «Выключить…» у автозапуска от администратора.</summary>
    private readonly Button _adminButton = new() { MinWidth = 120, MinHeight = 32, VerticalAlignment = VerticalAlignment.Top };

    /// <summary>Значение выставляется из кода, а не пользователем: запись в этот момент не нужна.</summary>
    private bool _syncing;

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="app">Пауза, автозапуск, открытие файлов.</param>
    /// <param name="dataDir">Папка настроек.</param>
    public GeneralPage(SettingsService settings, IAppControl app, string dataDir)
    {
        _settings = settings;
        _app = app;
        _dataDir = dataDir;
        _autostartCard = SettingsCards.Card(Strings.GeneralAutostartTitle, Strings.GeneralAutostartHint, _autostart, Strings.HelpAutostart);
        Content = BuildLayout();
        Refresh();
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            Refresh();
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Перечитывает состояние программы: пауза и автозапуск могли поменять в меню трея.</summary>
    public void Refresh()
    {
        _syncing = true;
        _theme.SelectedIndex = (int)_settings.Current.Theme;
        _language.SelectedIndex = (int)_settings.Current.UiLanguage;
        _log.IsOn = _settings.Current.LogEnabled;
        _enabled.IsOn = _app.IsEnabled;
        _autostart.IsOn = _app.IsAutostartOn;
        var admin = _app.IsAdminAutostartOn;
        _adminButton.Content = admin ? Strings.GeneralAdminDisable : Strings.GeneralAdminEnable;
        _adminButton.IsEnabled = true;
        _autostartCard.IsEnabled = !admin;
        _autostartCard.Opacity = admin ? 0.5 : 1;
        _syncing = false;
    }

    /// <summary>Настройки изменили из файла или другой страницы.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => Refresh();

    /// <summary>Собирает страницу.</summary>
    private StackPanel BuildLayout()
    {
        _ = _theme.Items.Add(Strings.ThemeSystem);
        _ = _theme.Items.Add(Strings.ThemeLight);
        _ = _theme.Items.Add(Strings.ThemeDark);
        AutomationProperties.SetName(_theme, Strings.ThemeTitle);
        _theme.SelectionChanged += (_, _) => OnThemeSelected();
        _ = _language.Items.Add(Strings.LanguageSystem);
        _ = _language.Items.Add(Strings.LanguageRussian);
        _ = _language.Items.Add(Strings.LanguageEnglish);
        AutomationProperties.SetName(_language, Strings.LanguageTitle);
        _language.SelectionChanged += (_, _) => OnLanguageSelected();
        _enabled.Toggled += () => _app.SetEnabled(_enabled.IsOn);
        _log.Toggled += () => SaveOrRevert(s => s with { LogEnabled = _log.IsOn });
        _autostart.Toggled += () =>
        {
            if (!_app.TrySetAutostart(_autostart.IsOn))
            {
                Refresh();
            }
        };
        _adminButton.Click += (_, _) => _ = ToggleAdminAsync();

        var panel = new StackPanel();
        panel.Children.Add(SettingsCards.GroupTitle(Strings.GroupAppearance));
        panel.Children.Add(SettingsCards.Card(Strings.ThemeTitle, Strings.ThemeDescription, _theme, Strings.HelpTheme));
        panel.Children.Add(SettingsCards.Card(Strings.LanguageTitle, Strings.LanguageDescription, _language, Strings.HelpLanguage));
        panel.Children.Add(SettingsCards.GroupTitle(Strings.GeneralGroupWork));
        panel.Children.Add(SettingsCards.Card(Strings.GeneralEnabledTitle, Strings.GeneralEnabledHint, _enabled, Strings.HelpEnabled));
        panel.Children.Add(SettingsCards.Card(Strings.GeneralLogTitle, Strings.GeneralLogHint, _log, Strings.HelpLog));
        panel.Children.Add(SettingsCards.GroupTitle(Strings.GeneralGroupAutostart));
        panel.Children.Add(_autostartCard);
        panel.Children.Add(SettingsCards.Card(Strings.GeneralAdminTitle, Strings.GeneralAdminHint, _adminButton, Strings.HelpAdmin));
        panel.Children.Add(SettingsCards.GroupTitle(Strings.GroupLinks));
        panel.Children.Add(Link(Strings.GeneralOpenFolder, () => _app.OpenFolder(_dataDir)));
        panel.Children.Add(Link(Strings.OpenSettingsFile, _app.OpenSettingsFile));
        return panel;
    }

    /// <summary>Ссылка в нижнем блоке страницы.</summary>
    private static TextBlock Link(string text, Action click)
    {
        var link = new Hyperlink(new Run(text));
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
        link.Click += (_, _) => click();
        return new TextBlock(link) { Margin = new Thickness(0, 0, 0, 6) };
    }

    /// <summary>Пользователь выбрал тему: она применяется и записывается сразу (SPEC F11).</summary>
    private void OnThemeSelected()
    {
        if (_syncing || _theme.SelectedIndex < 0)
        {
            return;
        }

        var theme = (AppTheme)_theme.SelectedIndex;
        SaveOrRevert(s => s with { Theme = theme });
    }

    /// <summary>Пользователь выбрал язык интерфейса: записывается сразу, окно пересоберёт программа (<c>SettingsWindow.ApplyLanguage</c>).</summary>
    private void OnLanguageSelected()
    {
        if (_syncing || _language.SelectedIndex < 0)
        {
            return;
        }

        var language = (AppLanguage)_language.SelectedIndex;
        SaveOrRevert(s => s with { UiLanguage = language });
    }

    /// <summary>Ставит фокус на список языка: после смены языка окно пересоздаёт страницу.</summary>
    public void FocusLanguage()
    {
        if (_language.IsLoaded)
        {
            _ = _language.Focus();
            return;
        }

        // Страница только что создана и ещё не в дереве: фокус возможен после загрузки списка.
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            _language.Loaded -= OnLoaded;
            _ = _language.Focus();
        }

        _language.Loaded += OnLoaded;
    }

    /// <summary>Записывает правку; при отказе записи страница возвращает прежние значения.</summary>
    /// <param name="change">Правка настроек.</param>
    private void SaveOrRevert(Func<AppSettings, AppSettings> change)
    {
        if (!_settings.Update(change))
        {
            Refresh();
        }
    }

    /// <summary>Включает или выключает автозапуск от администратора и обновляет страницу, когда окна и UAC закончились.</summary>
    private async Task ToggleAdminAsync()
    {
        _adminButton.IsEnabled = false;
        await _app.ToggleAdminAutostartAsync();
        Refresh();
    }
}
