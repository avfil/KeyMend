using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «Перенос» (SPEC 5, F8): сохранить настройки в файл, загрузить из файла, резервные копии и папка с настройками.
/// Пока идёт операция, страница недоступна. Пароль живёт только в поле ввода и в вызове; нигде не запоминается.
/// </summary>
internal sealed class TransferPage : UserControl
{
    /// <summary>Выбор «Оставить текущее» в списке конфликтов.</summary>
    private const int _keepIndex = 0;

    /// <summary>Текущие настройки: от них зависит, есть ли секреты для экспорта.</summary>
    private readonly SettingsService _settings;

    /// <summary>Экспорт, загрузка и восстановление.</summary>
    private readonly SettingsTransfer _transfer;

    /// <summary>Резервные копии для списка.</summary>
    private readonly BackupStore _backups;

    /// <summary>Диалоги выбора файла.</summary>
    private readonly IFilePicker _picker;

    /// <summary>Вопросы с кнопками.</summary>
    private readonly IDialogs _dialogs;

    /// <summary>Открытие папки.</summary>
    private readonly IAppControl _app;

    /// <summary>Папка настроек.</summary>
    private readonly string _dataDir;

    /// <summary>Имя группы радиокнопок: своё у каждого экземпляра, иначе две страницы мешали бы друг другу.</summary>
    private readonly string _modeGroup = "TransferMode" + Guid.NewGuid().ToString("N");

    /// <summary>Содержимое страницы; отключается на время операции.</summary>
    private readonly StackPanel _root = new();

    /// <summary>Кнопка «Сохранить…».</summary>
    private readonly Button _saveButton = new() { MinWidth = 120, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Переключатель «Добавить секреты».</summary>
    private readonly LabeledSwitch _secretsSwitch = new(Strings.TransferSecretsTitle);

    /// <summary>Карточка «Добавить секреты»: видна, только если в автозамене есть секреты.</summary>
    private readonly Border _secretsCard;

    /// <summary>Поля пароля экспорта.</summary>
    private readonly Border _passwordPanel = new() { Margin = new Thickness(0, 0, 0, 4) };

    /// <summary>Пароль экспорта.</summary>
    private readonly PasswordBox _password = new() { Width = 220 };

    /// <summary>Повтор пароля экспорта.</summary>
    private readonly PasswordBox _repeat = new() { Width = 220 };

    /// <summary>Ошибка пароля экспорта.</summary>
    private readonly TextBlock _passwordError = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

    /// <summary>Итог сохранения.</summary>
    private readonly TextBlock _saveStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>Кнопка «Выбрать файл…».</summary>
    private readonly Button _openButton = new() { MinWidth = 120, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Панель выбранного файла: режим, пароль, конфликты, кнопки.</summary>
    private readonly Border _loadPanel = new() { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };

    /// <summary>Итог открытия и загрузки.</summary>
    private readonly TextBlock _loadStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>Список резервных копий.</summary>
    private readonly StackPanel _backupsPanel = new();

    /// <summary>Выбранный файл, пока панель открыта.</summary>
    private ImportPreview? _preview;

    /// <summary>Радиокнопка «Заменить текущие».</summary>
    private RadioButton? _replace;

    /// <summary>Радиокнопка «Добавить к текущим».</summary>
    private RadioButton? _merge;

    /// <summary>Поле пароля от секретов файла.</summary>
    private PasswordBox? _filePassword;

    /// <summary>Блок конфликтов, виден при «Добавить».</summary>
    private StackPanel? _conflictsPanel;

    /// <summary>Выбор по каждому конфликту.</summary>
    private readonly List<(ConflictKey Key, ComboBox Choice)> _choices = [];

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки.</param>
    /// <param name="transfer">Экспорт, загрузка, восстановление.</param>
    /// <param name="backups">Резервные копии.</param>
    /// <param name="picker">Диалоги выбора файла.</param>
    /// <param name="dialogs">Вопросы с кнопками.</param>
    /// <param name="app">Открытие папки.</param>
    /// <param name="dataDir">Папка настроек.</param>
    public TransferPage(
        SettingsService settings, SettingsTransfer transfer, BackupStore backups, IFilePicker picker, IDialogs dialogs, IAppControl app, string dataDir)
    {
        _settings = settings;
        _transfer = transfer;
        _backups = backups;
        _picker = picker;
        _dialogs = dialogs;
        _app = app;
        _dataDir = dataDir;
        _secretsCard = SettingsCards.Card(Strings.TransferSecretsTitle, Strings.TransferSecretsHint, _secretsSwitch, Strings.HelpTransferSecrets);
        Content = BuildLayout();
        RefreshSecretsCard();
        RefreshBackups();
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            RefreshSecretsCard();
            RefreshBackups();
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Настройки изменились: секретные сокращения могли появиться или исчезнуть.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => RefreshSecretsCard();

    /// <summary>Собирает страницу.</summary>
    private StackPanel BuildLayout()
    {
        _saveButton.Content = Strings.TransferSaveButton;
        _saveButton.SetResourceReference(StyleProperty, "Km.AccentButton");
        _saveButton.Click += (_, _) => _ = SaveAsync();
        _openButton.Content = Strings.TransferLoadButton;
        _openButton.Click += (_, _) => _ = OpenAsync();
        AutomationProperties.SetName(_password, Strings.TransferPasswordLabel);
        AutomationProperties.SetName(_repeat, Strings.TransferPasswordRepeat);
        _secretsSwitch.Toggled += () => _passwordPanel.Visibility = _secretsSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        _passwordError.SetResourceReference(StyleProperty, "Km.CardHint");
        _passwordError.SetResourceReference(TextBlock.ForegroundProperty, "Km.Error");
        BuildPasswordPanel();

        _root.Children.Add(SettingsCards.GroupTitle(Strings.TransferGroupSave));
        _root.Children.Add(SettingsCards.Card(Strings.TransferSaveTitle, Strings.TransferSaveHint, _saveButton, Strings.HelpTransferSave));
        _root.Children.Add(_secretsCard);
        _root.Children.Add(_passwordPanel);
        _root.Children.Add(_saveStatus);
        _root.Children.Add(SettingsCards.GroupTitle(Strings.TransferGroupLoad));
        _root.Children.Add(SettingsCards.Card(Strings.TransferLoadTitle, Strings.TransferLoadHint, _openButton, Strings.HelpTransferLoad));
        _root.Children.Add(_loadPanel);
        _root.Children.Add(_loadStatus);
        _root.Children.Add(SettingsCards.GroupTitle(Strings.TransferGroupBackups));
        _root.Children.Add(_backupsPanel);
        _root.Children.Add(SettingsCards.GroupTitle(Strings.TransferGroupFolder));
        var open = new Button { Content = Strings.TransferFolderOpen, MinWidth = 120, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
        open.Click += (_, _) => _app.OpenFolder(_dataDir);
        _root.Children.Add(SettingsCards.Card(_dataDir, Strings.TransferFolderHint, open));
        return _root;
    }

    /// <summary>Собирает поля пароля экспорта.</summary>
    private void BuildPasswordPanel()
    {
        var fields = new StackPanel { Orientation = Orientation.Horizontal };
        _password.Margin = new Thickness(0, 0, 12, 0);
        fields.Children.Add(Labeled(Strings.TransferPasswordLabel, _password));
        fields.Children.Add(Labeled(Strings.TransferPasswordRepeat, _repeat));
        var stack = new StackPanel();
        stack.Children.Add(fields);
        stack.Children.Add(_passwordError);
        _passwordPanel.Child = stack;
        _passwordPanel.Visibility = Visibility.Collapsed;
        _passwordPanel.SetResourceReference(StyleProperty, "Km.CardBorder");
    }

    /// <summary>Подпись над полем.</summary>
    private static StackPanel Labeled(string caption, Control field)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        var label = new TextBlock { Text = caption, Margin = new Thickness(0, 0, 0, 4) };
        label.SetResourceReference(StyleProperty, "Km.CardHint");
        panel.Children.Add(label);
        panel.Children.Add(field);
        return panel;
    }

    /// <summary>Показывает карточку секретов, только когда в автозамене есть секретные сокращения.</summary>
    private void RefreshSecretsCard()
    {
        var any = _settings.Current.Snippets.Any(s => s.Secret);
        _secretsCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        _passwordPanel.Visibility = any && _secretsSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Включает или отключает страницу на время операции.</summary>
    /// <param name="busy">Идёт операция.</param>
    private void SetBusy(bool busy) => _root.IsEnabled = !busy;

    /// <summary>Показывает сообщение под блоком.</summary>
    /// <param name="target">Куда писать.</param>
    /// <param name="lines">Строки.</param>
    /// <param name="error">Это ошибка: красный цвет.</param>
    private static void Show(TextBlock target, IEnumerable<string> lines, bool error)
    {
        target.Text = string.Join(Environment.NewLine, lines);
        target.SetResourceReference(StyleProperty, "Km.CardHint");
        target.SetResourceReference(TextBlock.ForegroundProperty, error ? "Km.Error" : "Km.Text");
    }

    /// <summary>Проверяет пароль, спрашивает файл и сохраняет настройки.</summary>
    private async Task SaveAsync()
    {
        string? password = null;
        if (_secretsCard.Visibility == Visibility.Visible && _secretsSwitch.IsOn)
        {
            var error = CheckPassword();
            _passwordError.Text = error ?? string.Empty;
            _passwordError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
            if (error is not null)
            {
                return;
            }

            password = _password.Password;
        }

        var name = "KeyMend-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".keymend";
        if (_picker.PickSaveFile(name) is not { } path)
        {
            return;
        }

        SetBusy(true);
        Show(_saveStatus, [Strings.TransferBusy], error: false);
        try
        {
            var result = await _transfer.ExportAsync(path, password, CancellationToken.None);
            Show(_saveStatus, [TransferMessages.ForExport(result)], result != ExportResult.Saved);
            if (result == ExportResult.Saved)
            {
                _password.Clear();
                _repeat.Clear();
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Проверяет пароль и повтор.</summary>
    /// <returns>Текст ошибки или <c>null</c>.</returns>
    private string? CheckPassword()
    {
        if (_password.Password.Length < SettingsTransfer.MinPasswordLength)
        {
            return Strings.Fill(Strings.TransferPasswordShort, SettingsTransfer.MinPasswordLength.ToString(CultureInfo.InvariantCulture));
        }

        return _password.Password == _repeat.Password ? null : Strings.TransferPasswordMismatch;
    }

    /// <summary>Спрашивает файл, проверяет его целиком и открывает панель загрузки.</summary>
    private async Task OpenAsync()
    {
        if (_picker.PickTransferFile() is not { } path)
        {
            return;
        }

        SetBusy(true);
        Show(_loadStatus, [Strings.TransferBusy], error: false);
        try
        {
            var result = await _transfer.OpenAsync(path, CancellationToken.None);
            if (result.Preview is null)
            {
                _loadPanel.Visibility = Visibility.Collapsed;
                Show(_loadStatus, [TransferMessages.ForError(result.Error)], error: true);
                return;
            }

            _loadStatus.Text = string.Empty;
            ShowPreview(result.Preview);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Строит панель загрузки для проверенного файла.</summary>
    /// <param name="preview">Файл.</param>
    private void ShowPreview(ImportPreview preview)
    {
        _preview = preview;
        _choices.Clear();
        var stack = new StackPanel();

        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        summary.Inlines.Add(new System.Windows.Documents.Run(preview.FileName + ": ") { FontFamily = new System.Windows.Media.FontFamily("Consolas") });
        summary.Inlines.Add(new System.Windows.Documents.Run(TransferMessages.FileSummary(preview)));
        stack.Children.Add(summary);

        _replace = new RadioButton { Content = Strings.TransferModeReplace, GroupName = _modeGroup, Margin = new Thickness(0, 0, 24, 0) };
        _merge = new RadioButton { Content = Strings.TransferModeMerge, GroupName = _modeGroup, IsChecked = true };
        _merge.Checked += (_, _) => UpdateConflictsVisibility();
        _replace.Checked += (_, _) => UpdateConflictsVisibility();
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        modes.Children.Add(_replace);
        modes.Children.Add(_merge);
        modes.Children.Add(new HelpButton(Strings.TransferLoadTitle, Strings.HelpTransferMode));
        stack.Children.Add(modes);

        _filePassword = new PasswordBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(_filePassword, Strings.TransferSecretsPassword);
        _filePassword.LostFocus += (_, _) => _ = RecheckConflictsAsync();
        _filePassword.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _ = RecheckConflictsAsync();
            }
        };
        if (preview.HasSecrets)
        {
            var field = Labeled(Strings.TransferSecretsPassword + " · " + Strings.TransferSecretsPasswordHint, _filePassword);
            field.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(field);
        }

        _conflictsPanel = BuildConflicts(preview);
        stack.Children.Add(_conflictsPanel);

        var cancel = new Button { Content = Strings.TransferCancel, MinWidth = 96, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => CloseLoadPanel();
        var apply = new Button { Content = Strings.TransferApply, MinWidth = 96, Height = 32 };
        apply.SetResourceReference(StyleProperty, "Km.AccentButton");
        apply.Click += (_, _) => _ = ApplyAsync();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        stack.Children.Add(buttons);

        _loadPanel.Child = stack;
        _loadPanel.SetResourceReference(StyleProperty, "Km.CardBorder");
        _loadPanel.Visibility = Visibility.Visible;
        UpdateConflictsVisibility();
    }

    /// <summary>Строит список конфликтов: у каждого выбор «Оставить текущее» / «Взять из файла».</summary>
    /// <param name="preview">Файл.</param>
    /// <returns>Панель; пустая, если конфликтов нет.</returns>
    private StackPanel BuildConflicts(ImportPreview preview)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        if (preview.Conflicts.Count == 0)
        {
            return panel;
        }

        panel.Children.Add(SettingsCards.GroupTitle(Strings.TransferConflictsTitle));
        foreach (var conflict in preview.Conflicts)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new TextBlock
            {
                Text = TransferMessages.ForConflict(conflict),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            var choice = new ComboBox { MinWidth = 170, VerticalAlignment = VerticalAlignment.Center };
            _ = choice.Items.Add(Strings.TransferChoiceKeep);
            _ = choice.Items.Add(Strings.TransferChoiceTake);
            choice.SelectedIndex = _keepIndex;
            AutomationProperties.SetName(choice, TransferMessages.ForConflict(conflict));
            choice.SetValue(Grid.ColumnProperty, 1);
            row.Children.Add(text);
            row.Children.Add(choice);
            panel.Children.Add(row);
            _choices.Add((conflict.Key, choice));
        }

        return panel;
    }

    /// <summary>
    /// После ввода пароля пересчитывает конфликты: секретные сокращения с теми же текстами, что у вас, из списка уходят, выбор у остальных
    /// сохраняется. Неверный пароль список не меняет (об этом скажет сама загрузка). Окончательно решает загрузка, а не этот список.
    /// </summary>
    private async Task RecheckConflictsAsync()
    {
        if (_preview is not { HasSecrets: true } preview || _filePassword is not { Password.Length: > 0 } box
            || _conflictsPanel?.Parent is not StackPanel parent)
        {
            return;
        }

        var fresh = await _transfer.RecheckConflictsAsync(preview, box.Password, CancellationToken.None);
        if (fresh is null || !ReferenceEquals(_preview, preview) || _conflictsPanel?.Parent != parent)
        {
            return;
        }

        var chosen = _choices.Where(c => c.Choice.SelectedIndex != _keepIndex).Select(c => c.Key).ToHashSet();
        var updated = preview with { Conflicts = fresh };
        _preview = updated;
        _choices.Clear();
        var index = parent.Children.IndexOf(_conflictsPanel);
        parent.Children.RemoveAt(index);
        _conflictsPanel = BuildConflicts(updated);
        parent.Children.Insert(index, _conflictsPanel);
        foreach (var entry in _choices.Where(c => chosen.Contains(c.Key)))
        {
            entry.Choice.SelectedIndex = 1 - _keepIndex;
        }

        UpdateConflictsVisibility();
    }

    /// <summary>Конфликты спрашиваются только при «Добавить к текущим»: «Заменить» берёт файл целиком.</summary>
    private void UpdateConflictsVisibility()
    {
        if (_conflictsPanel is not null && _merge is not null)
        {
            _conflictsPanel.Visibility = _merge.IsChecked == true && _choices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Закрывает панель загрузки, пароль забывается.</summary>
    private void CloseLoadPanel()
    {
        _loadPanel.Visibility = Visibility.Collapsed;
        _loadPanel.Child = null;
        _preview = null;
        _filePassword = null;
    }

    /// <summary>Загружает выбранный файл.</summary>
    private async Task ApplyAsync()
    {
        if (_preview is not { } preview || _replace is null)
        {
            return;
        }

        var mode = _replace.IsChecked == true ? ImportMode.Replace : ImportMode.Merge;
        var take = mode == ImportMode.Merge
            ? _choices.Where(c => c.Choice.SelectedIndex != _keepIndex).Select(c => c.Key).ToHashSet()
            : [];
        var password = preview.HasSecrets && _filePassword is { Password.Length: > 0 } ? _filePassword.Password : null;

        SetBusy(true);
        Show(_loadStatus, [Strings.TransferBusy], error: false);
        try
        {
            var result = await _transfer.ImportAsync(preview, mode, take, password, CancellationToken.None);
            if (result.Failure == ImportFailure.None)
            {
                CloseLoadPanel();
                Show(_loadStatus, TransferMessages.Summary(result, mode), error: false);
            }
            else
            {
                Show(_loadStatus, [TransferMessages.ForFailure(result.Failure)], error: true);
            }
        }
        finally
        {
            SetBusy(false);
            RefreshBackups();
        }
    }

    /// <summary>Перестраивает список резервных копий.</summary>
    private void RefreshBackups()
    {
        _backupsPanel.Children.Clear();
        var title = new TextBlock { Text = Strings.TransferBackupsTitle };
        title.SetResourceReference(StyleProperty, "Km.CardTitle");
        var hint = new TextBlock { Text = Strings.Fill(Strings.TransferBackupsHint, BackupStore.MaxCopies.ToString(CultureInfo.InvariantCulture)) };
        hint.SetResourceReference(StyleProperty, "Km.CardHint");
        var header = new StackPanel();
        header.Children.Add(title);
        header.Children.Add(hint);
        _backupsPanel.Children.Add(CardOf(header));

        var list = _backups.List();
        if (list.Count == 0)
        {
            var none = new TextBlock { Text = Strings.TransferBackupsNone, Margin = new Thickness(16, 4, 0, 8) };
            none.SetResourceReference(StyleProperty, "Km.CardHint");
            _backupsPanel.Children.Add(none);
            return;
        }

        foreach (var backup in list)
        {
            _backupsPanel.Children.Add(BackupRow(backup));
        }
    }

    /// <summary>Строка одной копии с кнопкой «Восстановить».</summary>
    /// <param name="backup">Копия.</param>
    /// <returns>Карточка.</returns>
    private Border BackupRow(BackupInfo backup)
    {
        var when = backup.Created.ToString("g", CultureInfo.CurrentCulture);
        var text = new TextBlock
        {
            Text = backup.SourceName.Length > 0 ? Strings.Fill(Strings.TransferBackupRow, when, backup.SourceName) : Strings.Fill(Strings.TransferBackupRowPlain, when),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var restore = new Button { Content = Strings.TransferRestoreButton, MinWidth = 120, MinHeight = 32 };
        AutomationProperties.SetName(restore, Strings.TransferRestoreButton + " " + when);
        restore.Click += (_, _) => _ = RestoreAsync(backup, when);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        restore.SetValue(Grid.ColumnProperty, 1);
        grid.Children.Add(text);
        grid.Children.Add(restore);
        return CardOf(grid);
    }

    /// <summary>Оборачивает содержимое в карточку темы.</summary>
    /// <param name="child">Содержимое.</param>
    /// <returns>Карточка.</returns>
    private static Border CardOf(UIElement child)
    {
        var border = new Border { Margin = new Thickness(0, 0, 0, 4), Child = child };
        border.SetResourceReference(StyleProperty, "Km.CardBorder");
        return border;
    }

    /// <summary>Спрашивает подтверждение и возвращает настройки из копии.</summary>
    /// <param name="backup">Копия.</param>
    /// <param name="when">Дата копии для текста вопроса.</param>
    private async Task RestoreAsync(BackupInfo backup, string when)
    {
        if (_dialogs.Ask(Strings.TransferRestoreAskTitle, Strings.Fill(Strings.TransferRestoreAsk, when), Strings.TransferRestoreButton, Strings.TransferCancel) != 0)
        {
            return;
        }

        SetBusy(true);
        Show(_loadStatus, [Strings.TransferBusy], error: false);
        try
        {
            var result = await _transfer.RestoreAsync(backup, CancellationToken.None);
            CloseLoadPanel();
            if (result.Failure == ImportFailure.None)
            {
                Show(_loadStatus, TransferMessages.Summary(result, ImportMode.Replace), error: false);
            }
            else
            {
                Show(_loadStatus, [TransferMessages.ForFailure(result.Failure)], error: true);
            }
        }
        finally
        {
            SetBusy(false);
            RefreshBackups();
        }
    }
}
