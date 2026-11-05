using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «О программе» (SPEC 5): версия, режим администратора, приватность, репозиторий и папки приложения с кнопкой «Открыть».
/// </summary>
internal sealed class AboutPage : UserControl
{
    /// <summary>Шрифт путей.</summary>
    private static readonly System.Windows.Media.FontFamily _mono = new("Cascadia Mono, Consolas");

    /// <summary>Текущие настройки: от них зависит приписка у журнала.</summary>
    private readonly SettingsService _settings;

    /// <summary>Открытие папок.</summary>
    private readonly IAppControl _app;

    /// <summary>Сведения о программе.</summary>
    private readonly AboutInfo _info;

    /// <summary>Путь журнала с припиской, когда журнал выключен.</summary>
    private readonly TextBlock _logPath = new() { FontFamily = _mono, FontSize = 12, TextWrapping = TextWrapping.Wrap };

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки.</param>
    /// <param name="app">Открытие папок.</param>
    /// <param name="info">Сведения о программе.</param>
    public AboutPage(SettingsService settings, IAppControl app, AboutInfo info)
    {
        _settings = settings;
        _app = app;
        _info = info;
        Content = BuildLayout();
        RefreshLogPath();
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            RefreshLogPath();
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Журнал могли включить или выключить в «Общих» или в файле.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => RefreshLogPath();

    /// <summary>Собирает страницу.</summary>
    private StackPanel BuildLayout()
    {
        var panel = new StackPanel();
        panel.Children.Add(HeaderCard());
        panel.Children.Add(SettingsCards.GroupTitle(Strings.AboutGroupInfo));
        panel.Children.Add(SettingsCards.Card(Strings.AboutPrivacyTitle, Strings.AboutPrivacyText, null));
        panel.Children.Add(RepositoryCard());
        panel.Children.Add(SettingsCards.GroupTitle(Strings.AboutGroupFolders));
        panel.Children.Add(FolderCard(Strings.AboutFolderProgram, _info.ProgramDir, null));
        panel.Children.Add(FolderCard(Strings.AboutFolderData, _info.DataDir, null));
        panel.Children.Add(FolderCard(Strings.AboutFolderScripts, _info.ScriptsDir, null));
        panel.Children.Add(FolderCard(Strings.AboutFolderLog, _info.LogDir, _logPath));
        panel.Children.Add(SettingsCards.GroupTitle(Strings.AboutGroupRegistry));
        panel.Children.Add(RegistryCard());
        return panel;
    }

    /// <summary>Верхняя карточка: логотип, название, версия и плашка режима администратора.</summary>
    private Border HeaderCard()
    {
        var logo = new LogoMark { Width = 72, Height = 72, Shield = _info.Elevated, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = Strings.AppName, FontSize = 20, FontWeight = FontWeights.SemiBold };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text");
        var texts = new StackPanel { Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(name);
        texts.Children.Add(KeyChipsBuilder.Label(RuleSummary.Fill(Strings.AboutVersion, _info.Version), "Km.Text2", 13, margin: false));
        if (_info.Elevated)
        {
            var badge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            badge.SetResourceReference(Border.BackgroundProperty, "Km.Admin");
            var text = KeyChipsBuilder.Label(Strings.AboutAdminBadge, "Km.AdminText", 12, margin: false);
            badge.Child = text;
            texts.Children.Add(badge);
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(logo);
        row.Children.Add(texts);
        var card = new Border { Padding = new Thickness(24), Child = row };
        card.SetResourceReference(StyleProperty, "Km.CardBorder");
        return card;
    }

    /// <summary>Карточка репозитория: адрес и кнопка «Копировать ссылку» (браузер не запускается, программа к сети не обращается).</summary>
    private Border RepositoryCard()
    {
        var url = new TextBlock { Text = AboutInfo.RepositoryUrl, FontFamily = _mono, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        url.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        var detail = new StackPanel();
        detail.Children.Add(url);
        detail.Children.Add(KeyChipsBuilder.Label(Strings.AboutRepoHint, "Km.Text2", 12, margin: false));

        var copy = new Button { Content = Strings.AboutCopyLink, MinWidth = 90, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(copy, Strings.AboutRepoTitle + ": " + Strings.AboutCopyLink);
        copy.Click += (_, _) => _app.CopyText(AboutInfo.RepositoryUrl);
        return SettingsCards.Card(Strings.AboutRepoTitle, detail, copy);
    }

    /// <summary>Карточка папки: название, путь и кнопка «Открыть».</summary>
    /// <param name="title">Название папки.</param>
    /// <param name="path">Полный путь.</param>
    /// <param name="pathBlock">Готовое поле пути (для журнала) или <c>null</c> — создаётся простое.</param>
    private Border FolderCard(string title, string path, TextBlock? pathBlock)
    {
        var block = pathBlock ?? new TextBlock { Text = path, FontFamily = _mono, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        var open = new Button { Content = Strings.AboutOpen, MinWidth = 90, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(open, title + ": " + Strings.AboutOpen);
        open.IsEnabled = Directory.Exists(path);
        open.Click += (_, _) => _app.OpenFolder(path);

        return SettingsCards.Card(title, block, open);
    }

    /// <summary>Карточка реестра: раздел, в который пишется автозапуск, имя значения и «записано сейчас или нет».</summary>
    private Border RegistryCard()
    {
        var path = new TextBlock { Text = _info.RegistryKey, FontFamily = _mono, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        path.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        var value = KeyChipsBuilder.Label(RuleSummary.Fill(Strings.AboutRegistryValue, _info.RegistryValue), "Km.Text2", 12, margin: false);
        value.TextWrapping = TextWrapping.Wrap;
        var detail = new StackPanel();
        detail.Children.Add(path);
        detail.Children.Add(value);

        var copy = new Button { Content = Strings.AboutCopyPath, MinWidth = 90, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(copy, Strings.AboutRegistryTitle + ": " + Strings.AboutCopyPath);
        copy.Click += (_, _) => _app.CopyText(_info.RegistryKey);
        return SettingsCards.Card(Strings.AboutRegistryTitle, detail, copy);
    }

    /// <summary>Показывает путь журнала; у выключенного журнала добавляет, что папка пуста.</summary>
    private void RefreshLogPath() =>
        _logPath.Text = _settings.Current.LogEnabled ? _info.LogDir : _info.LogDir + " · " + Strings.AboutLogOff;
}
