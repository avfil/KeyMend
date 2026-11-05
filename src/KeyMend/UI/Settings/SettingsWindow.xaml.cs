using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.UI.Themes;

namespace KeyMend.UI.Settings;

/// <summary>
/// Окно настроек (SPEC 5): слева состояние программы и меню разделов, справа страница раздела. Окно одно на программу;
/// размер у всех разделов одинаковый, содержимое прокручивается.
/// </summary>
internal sealed partial class SettingsWindow : Window, IDialogs, IFilePicker
{
    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Пути данных программы.</summary>
    private readonly AppPaths _paths;

    /// <summary>Запись клавиш для поля «Клавиши».</summary>
    private readonly ITriggerRecording _recording;

    /// <summary>Сочетания смены языка в Windows.</summary>
    private readonly ILanguageHotkeysSource _hotkeys;

    /// <summary>Тема: диалогам ставится светлая или тёмная рамка.</summary>
    private readonly ThemeManager _themes;

    /// <summary>Пункты меню разделов.</summary>
    private IReadOnlyList<SectionItem> _items = SettingsSections.All();

    /// <summary>KeyMend включён (не на паузе): нужно, чтобы заново подписать строку состояния при смене языка.</summary>
    private bool _enabledState;

    /// <summary>Работает от администратора.</summary>
    private readonly bool _elevated;

    /// <summary>Установленные языки для списков выбора языка.</summary>
    private readonly IInstalledLanguages _languages;

    /// <summary>Папка скриптов: диалог выбора файла открывается в ней.</summary>
    private readonly string _scriptsDir;

    /// <summary>Включена ли гибернация в Windows.</summary>
    private readonly bool _hibernateEnabled;

    /// <summary>Запись сокращений, секреты и таблицы раскладок для раздела «Автозамена».</summary>
    private readonly SnippetServices _snippets;

    /// <summary>Действия самой программы: пример подсказки раскладки.</summary>
    private readonly IAppControl _app;

    /// <summary>Запущенные программы и описания файлов.</summary>
    private readonly IRunningApps _runningApps;

    /// <summary>Экспорт, загрузка и копии для раздела «Перенос».</summary>
    private readonly TransferServices _transfer;

    /// <summary>Отступы прокрутки страницы по умолчанию.</summary>
    private static readonly Thickness _pagePadding = new(24, 0, 40, 40);

    /// <summary>Раздел, показанный сейчас.</summary>
    private SettingsSection _section = SettingsSection.General;

    /// <summary>Меню перестраивается из кода: обработчик выбора в этот момент ничего не делает.</summary>
    private bool _syncing;

    /// <summary>Создаёт окно и показывает раздел «Общие».</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="themes">Тема: окну ставится светлая или тёмная рамка.</param>
    /// <param name="elevated">Работает от администратора.</param>
    /// <param name="enabled">KeyMend включён (не на паузе).</param>
    /// <param name="recording">Запись клавиш для поля «Клавиши».</param>
    /// <param name="languages">Установленные языки.</param>
    /// <param name="hotkeys">Сочетания смены языка в Windows.</param>
    /// <param name="paths">Пути данных: папка скриптов для выбора файлов, папка настроек для ссылок.</param>
    /// <param name="hibernateEnabled">Включена ли гибернация в Windows.</param>
    /// <param name="snippets">Запись сокращений, секреты и таблицы раскладок.</param>
    /// <param name="app">Действия самой программы.</param>
    /// <param name="runningApps">Запущенные программы для раздела «Исключения».</param>
    /// <param name="transfer">Экспорт, загрузка и копии для раздела «Перенос».</param>
    public SettingsWindow(
        SettingsService settings, ThemeManager themes, bool elevated, bool enabled,
        ITriggerRecording recording, ILanguageHotkeysSource hotkeys, IInstalledLanguages languages, AppPaths paths, bool hibernateEnabled,
        SnippetServices snippets, IAppControl app, IRunningApps runningApps, TransferServices transfer)
    {
        _transfer = transfer;
        _runningApps = runningApps;
        _app = app;
        _snippets = snippets;
        _languages = languages;
        _paths = paths;
        _scriptsDir = paths.ScriptsDir;
        _hibernateEnabled = hibernateEnabled;
        _settings = settings;
        _themes = themes;
        _elevated = elevated;
        _recording = recording;
        _hotkeys = hotkeys;
        InitializeComponent();
        FitToWorkArea();
        SetTexts();
        themes.Attach(this);
        SetEnabled(enabled);
        Select(SettingsSection.General);
    }

    /// <summary>
    /// Применяет сменившийся язык интерфейса без закрытия окна: заново подписывает заголовок, меню и строку состояния и пересоздаёт
    /// страницу. Открытый редактор не трогается (он откроется заново на новом языке), чтобы не потерять несохранённые правки.
    /// </summary>
    public void ApplyLanguage()
    {
        SetTexts();
        SetEnabled(_enabledState);
        if (PageHost.Content is IEditorPage)
        {
            _ = SyncMenu(_section);
            return;
        }

        Select(_section);
        if (_section == SettingsSection.General && PageHost.Content is GeneralPage page)
        {
            page.FocusLanguage();
        }
    }

    /// <summary>Подписывает заголовок окна, название программы, поиск и меню разделов на языке интерфейса.</summary>
    private void SetTexts()
    {
        Title = Strings.WindowTitle + (_elevated ? " " + Strings.WindowTitleAdmin : string.Empty);
        AppNameText.Text = Strings.AppName;
        SearchBox.Text = Strings.SearchPlaceholder;
        AutomationProperties.SetName(SearchBox, Strings.SearchPlaceholder);
        AutomationProperties.SetName(BottomMenu, Strings.SectionAbout);
        AutomationProperties.SetName(TopMenu, Strings.AppName);
        _items = SettingsSections.All();
        TopMenu.ItemsSource = _items.Where(i => !i.IsBottom).ToList();
        BottomMenu.ItemsSource = _items.Where(i => i.IsBottom).ToList();
    }

    /// <summary>
    /// Не даёт окну быть больше рабочей области экрана: при масштабе 200 % на мониторе 1440p высота 760 даёт 1520 физических
    /// пикселей, и нижняя панель редактора уходит за экран.
    /// </summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width);
        Height = Math.Min(Height, area.Height);
        MinWidth = Math.Min(MinWidth, Width);
        MinHeight = Math.Min(MinHeight, Height);
    }

    /// <summary>Обновляет логотип и строку состояния: пауза, обычный режим, администратор.</summary>
    /// <param name="enabled">KeyMend включён (не на паузе).</param>
    public void SetEnabled(bool enabled)
    {
        _enabledState = enabled;
        var view = AppStatusView.From(enabled, _elevated);
        StatusText.Text = view.Text;
        Logo.Greyed = view.Greyed;
        Logo.Shield = view.Shield;
        Icon = AppIcons.Window(!enabled, _elevated);
        var key = view.Dot switch
        {
            StatusDot.Paused => "Km.Text2",
            StatusDot.Admin => "Km.Admin",
            _ => "Km.Accent",
        };
        StatusDotShape.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, key);
        RefreshAppState();
    }

    /// <summary>Пауза или автозапуск изменились вне страницы (меню трея): открытая страница «Общие» показывает актуальное состояние.</summary>
    public void RefreshAppState() => (PageHost.Content as GeneralPage)?.Refresh();

    /// <summary>Окно закрывают вместе с программой: вопрос о несохранённых изменениях не задаётся.</summary>
    public bool Quitting { get; set; }

    /// <summary>Показывает раздел и отмечает его в меню. Несохранённые изменения не проверяются: для ухода с вопросом есть <see cref="Navigate"/>.</summary>
    /// <param name="section">Раздел.</param>
    /// <param name="ruleGroup">Для раздела «Правила»: какую группу выбрать; <c>null</c> — «Все».</param>
    public void Select(SettingsSection section, RuleGroup? ruleGroup = null)
    {
        var item = SyncMenu(section);
        PageTitle.Text = item.Title;
        BarHost.Content = null;
        PageScroll.Padding = _pagePadding;
        PageHost.Content = CreatePage(section, ruleGroup);
    }

    /// <summary>Переходит в раздел, спросив о несохранённых изменениях в редакторе.</summary>
    /// <param name="section">Раздел.</param>
    /// <param name="ruleGroup">Для раздела «Правила»: какую группу выбрать; <c>null</c> — «Все».</param>
    /// <returns><c>true</c>, если раздел открыт; <c>false</c>, если владелец остался в редакторе.</returns>
    public bool Navigate(SettingsSection section, RuleGroup? ruleGroup = null)
    {
        if (!ConfirmLeave())
        {
            return false;
        }

        Select(section, ruleGroup);
        return true;
    }

    /// <summary>Открывает редактор правила; из другого редактора с изменениями сначала спрашивает.</summary>
    /// <param name="rule">Правило.</param>
    public void ShowRuleEditor(Rule rule)
    {
        if (ConfirmLeave())
        {
            ShowEditor(rule, null);
        }
    }

    /// <summary>Открывает редактор нового правила в выбранной группе.</summary>
    /// <param name="group">Группа: определяет первый шаг.</param>
    public void ShowNewRule(RuleGroup group)
    {
        if (ConfirmLeave())
        {
            ShowEditor(null, StepCatalog.FirstStep(group));
        }
    }

    /// <summary>Открывает редактор сокращения; из другого редактора с изменениями сначала спрашивает.</summary>
    /// <param name="snippet">Сокращение.</param>
    public void ShowSnippetEditor(Snippet snippet)
    {
        if (ConfirmLeave())
        {
            ShowSnippetPage(snippet);
        }
    }

    /// <summary>Открывает редактор нового сокращения.</summary>
    public void ShowNewSnippet()
    {
        if (ConfirmLeave())
        {
            ShowSnippetPage(null);
        }
    }

    /// <summary>Отмечает раздел в меню, не трогая страницу.</summary>
    private SectionItem SyncMenu(SettingsSection section)
    {
        var item = _items.First(i => i.Section == section);
        _section = section;
        _syncing = true;
        TopMenu.SelectedItem = item.IsBottom ? null : item;
        BottomMenu.SelectedItem = item.IsBottom ? item : null;
        _syncing = false;
        return item;
    }

    /// <summary>Показывает редактор правила внутри раздела «Правила».</summary>
    private void ShowEditor(Rule? original, Step? newStep)
    {
        _ = SyncMenu(SettingsSection.Rules);
        var context = new StepContext(_languages.Tags(), _scriptsDir, this, _hibernateEnabled, () => Navigate(SettingsSection.Hint));
        var page = new RuleEditorPage(
            _settings, _recording, _hotkeys, context, this, original, newStep, () => Select(SettingsSection.Rules), ShowRuleEditor);
        ShowEditorPage(page, SettingsSection.Rules);
    }

    /// <summary>Показывает редактор сокращения внутри раздела «Автозамена».</summary>
    private void ShowSnippetPage(Snippet? original)
    {
        _ = SyncMenu(SettingsSection.Snippets);
        var context = new StepContext(_languages.Tags(), _scriptsDir, this, _hibernateEnabled, () => Navigate(SettingsSection.Hint));
        var page = new SnippetEditorPage(
            _settings, _snippets, context, this, original, () => Select(SettingsSection.Snippets), ShowSnippetEditor);
        ShowEditorPage(page, SettingsSection.Snippets);
    }

    /// <summary>Ставит страницу-редактор в окно: заголовок-путь, отступ под нижнюю панель и сама панель.</summary>
    private void ShowEditorPage(IEditorPage page, SettingsSection section)
    {
        page.TitleChanged += () => SetCrumb(section, page.TitleText);
        SetCrumb(section, page.TitleText);
        PageScroll.Padding = new Thickness(24, 0, 40, 110);
        PageHost.Content = page;
        BarHost.Content = page.ActionBar;
    }

    /// <summary>Заголовок редактора: ссылка на раздел, стрелка и название правила или сокращения.</summary>
    private void SetCrumb(SettingsSection section, string name)
    {
        PageTitle.Inlines.Clear();
        var link = new Hyperlink(new Run(_items.First(i => i.Section == section).Title)) { TextDecorations = null };
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
        link.Click += (_, _) => Navigate(section);
        PageTitle.Inlines.Add(link);
        PageTitle.Inlines.Add(new Run("  ›  "));
        PageTitle.Inlines.Add(new Run(name));
    }

    /// <summary>Можно ли уйти со страницы: в редакторе с изменениями спрашивает «Сохранить / Не сохранять / Отмена».</summary>
    /// <returns><c>true</c>, если страницу можно менять.</returns>
    private bool ConfirmLeave()
    {
        if (PageHost.Content is not IEditorPage { HasChanges: true } page)
        {
            return true;
        }

        var answer = Ask(
            Strings.UnsavedTitle, RuleSummary.Fill(page.UnsavedTemplate, page.TitleText),
            Strings.UnsavedSave, Strings.UnsavedDiscard, Strings.DialogCancel);
        return answer switch
        {
            0 => page.TrySave(),
            1 => true,
            _ => false,
        };
    }

    /// <summary>Закрытие окна: в редакторе с изменениями сначала вопрос, кроме выхода из программы.</summary>
    /// <param name="e">Данные закрытия.</param>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!Quitting && !ConfirmLeave())
        {
            e.Cancel = true;
        }
    }

    /// <summary>Создаёт страницу раздела.</summary>
    /// <param name="section">Раздел.</param>
    /// <param name="ruleGroup">Группа правил, выбранная при открытии «Правил».</param>
    /// <returns>Страница.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Раздела нет в меню.</exception>
    private UIElement CreatePage(SettingsSection section, RuleGroup? ruleGroup) => section switch
    {
        SettingsSection.General => new GeneralPage(_settings, _app, _paths.DataDir),
        SettingsSection.Rules => new RulesPage(_settings, _hotkeys, this, ShowRuleEditor, ShowNewRule, ruleGroup),
        SettingsSection.Snippets => new SnippetsPage(_settings, _snippets.Tables, ShowSnippetEditor, ShowNewSnippet),
        SettingsSection.Exclusions => new ExclusionsPage(_settings, _runningApps),
        SettingsSection.About => new AboutPage(_settings, _app, AboutInfo.Create(_paths, _elevated)),
        SettingsSection.Hint => new HintPage(_settings, _app, () => Navigate(SettingsSection.Rules, RuleGroup.Language)),
        SettingsSection.Transfer => new TransferPage(_settings, _transfer.Transfer, _transfer.Backups, this, this, _app, _paths.DataDir),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };

    /// <inheritdoc/>
    public int Ask(string title, string text, params string[] buttons) =>
        new ConfirmDialog(this, _themes, title, text, buttons).ShowAndGetResult();

    /// <inheritdoc/>
    public string? PickFile(string initialDirectory)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.LaunchFile,
            Filter = Strings.LaunchFileFilter,
            InitialDirectory = System.IO.Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? PickFolder(string initialDirectory)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.LaunchWorkDir,
            InitialDirectory = System.IO.Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
        };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    /// <inheritdoc/>
    public string? PickSaveFile(string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.TransferSaveDialogTitle,
            Filter = Strings.TransferSaveFilter,
            FileName = suggestedName,
            DefaultExt = ".keymend",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? PickTransferFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.TransferOpenDialogTitle,
            Filter = Strings.TransferOpenFilter,
            CheckFileExists = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    /// <summary>Щелчок мимо поля ввода убирает курсор из текстового поля: значение применилось, и это видно.</summary>
    /// <param name="sender">Окно.</param>
    /// <param name="e">Данные щелчка.</param>
    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.OriginalSource is not TextBox && !IsInsideTextBox(e.OriginalSource as DependencyObject))
        {
            Keyboard.ClearFocus();
            PageHost.Focus();
        }
    }

    /// <summary>Источник щелчка лежит внутри текстового поля (его шаблон состоит из других элементов).</summary>
    private static bool IsInsideTextBox(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is TextBox)
            {
                return true;
            }

            element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    /// <summary>Стрелки вверх и вниз ходят по обоим меню как по одному списку: из «Перенос» — в «Справка» и обратно.</summary>
    /// <param name="sender">Список с фокусом.</param>
    /// <param name="e">Данные нажатия.</param>
    private void OnMenuKeyDown(object sender, KeyEventArgs e)
    {
        var step = e.Key switch
        {
            Key.Down => 1,
            Key.Up => -1,
            _ => 0,
        };
        if (step == 0)
        {
            return;
        }

        e.Handled = true;
        var current = _items.ToList().FindIndex(i => i.Section == (((ListBox)sender).SelectedItem as SectionItem)?.Section);
        var next = Math.Clamp((current < 0 ? (step > 0 ? -1 : _items.Count) : current) + step, 0, _items.Count - 1);
        var target = Navigate(_items[next].Section) ? _items[next] : _items.First(i => i.Section == _section);
        FocusMenuItem(target);
    }

    /// <summary>Переводит фокус на пункт меню, чтобы стрелки продолжали с него.</summary>
    /// <param name="item">Пункт.</param>
    private void FocusMenuItem(SectionItem item)
    {
        var list = item.IsBottom ? BottomMenu : TopMenu;
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
        {
            _ = container.Focus();
        }
    }

    /// <summary>Выбор в одном из двух меню: второе сбрасывается, показывается выбранный раздел.</summary>
    /// <param name="sender">Список, в котором выбрали пункт.</param>
    /// <param name="e">Данные выбора.</param>
    private void OnMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ((ListBox)sender).SelectedItem is not SectionItem item)
        {
            return;
        }

        if (!Navigate(item.Section))
        {
            _ = SyncMenu(_section);
            return;
        }

        FocusMenuItem(item);
    }
}
