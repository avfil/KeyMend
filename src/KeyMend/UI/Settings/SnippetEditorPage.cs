using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Text;

namespace KeyMend.UI.Settings;

/// <summary>
/// Редактор сокращения автозамены (SPEC 5, F5): сокращение с видом в другой раскладке, «включено», секрет, текст, способ вставки,
/// перенос строки и шаги «После вставки». «Сохранить» проверяет сокращение и пишет его через <see cref="SnippetSaver"/>: секрет
/// уходит в зашифрованное хранилище, в <c>settings.json</c> остаётся только ключ. Тексты в журнал не попадают.
/// </summary>
internal sealed class SnippetEditorPage : UserControl, IEditorPage
{
    /// <summary>Шрифт сокращения.</summary>
    private static readonly FontFamily _mono = new("Cascadia Mono, Consolas");

    /// <summary>Текущие настройки.</summary>
    private readonly SettingsService _settings;

    /// <summary>Запись сокращений и секретов.</summary>
    private readonly SnippetSaver _saver;

    /// <summary>Таблицы установленных раскладок: для проверок и «тех же клавиш».</summary>
    private readonly IReadOnlyList<LayoutTable> _tables;

    /// <summary>Вопросы с кнопками.</summary>
    private readonly IDialogs _dialogs;

    /// <summary>Возврат к таблице без вопросов.</summary>
    private readonly Action _close;

    /// <summary>Открывает редактор другого сокращения (ссылка «Открыть это сокращение»).</summary>
    private readonly Action<Snippet> _openSnippet;

    /// <summary>Сокращение, которое правят; <c>null</c> — новое.</summary>
    private readonly Snippet? _original;

    /// <summary>Папка скриптов: от неё проверяются файлы шагов запуска.</summary>
    private readonly string _scriptsDir;

    /// <summary>Поле сокращения.</summary>
    private readonly TextBox _keys = new() { Width = 240, MinHeight = 32, FontFamily = _mono, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Те же клавиши в другой раскладке.</summary>
    private readonly TextBlock _otherLayout = new() { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };

    /// <summary>Ошибка сокращения.</summary>
    private readonly TextBlock _keysError = ErrorText();

    /// <summary>Предупреждения о перекрытии с другими сокращениями; сохранить можно.</summary>
    private readonly TextBlock _overlaps = new() { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

    /// <summary>Переключатель «Включено».</summary>
    private readonly CheckBox _enabled = new() { VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Подпись «Вкл./Откл.».</summary>
    private readonly TextBlock _enabledLabel = new() { Width = 44, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Переключатель «Секрет».</summary>
    private readonly CheckBox _secret = new() { VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Подпись «Вкл./Откл.» секрета.</summary>
    private readonly TextBlock _secretLabel = new() { Width = 44, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Текст замены (и открытый секрет, пока он показан).</summary>
    private readonly TextBox _text = new()
    {
        MinHeight = 64,
        MinWidth = 320,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalContentAlignment = VerticalAlignment.Top,
    };

    /// <summary>Скрытый секрет.</summary>
    private readonly PasswordBox _password = new() { MinHeight = 32, MinWidth = 320, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Кнопка «Показать» / «Скрыть».</summary>
    private readonly Button _reveal = new() { MinHeight = 32, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };

    /// <summary>Ошибка текста.</summary>
    private readonly TextBlock _textError = ErrorText();

    /// <summary>Сообщение, что текст секрета не найден в хранилище.</summary>
    private readonly TextBlock _secretMissing = ErrorText();

    /// <summary>Способ вставки обычного сокращения.</summary>
    private readonly SegmentPicker<InsertMode> _insert;

    /// <summary>Пояснение вместо выбора способа: секрет вставляется только через буфер.</summary>
    private readonly TextBlock _insertSecret = new() { FontSize = 13, Visibility = Visibility.Collapsed };

    /// <summary>Перенос строки.</summary>
    private readonly SegmentPicker<NewlineMode> _newline;

    /// <summary>Шаги «После вставки».</summary>
    private readonly StepListEditor _after;

    /// <summary>Кнопка «Удалить сокращение».</summary>
    private readonly Button _delete = new() { Content = Strings.SnipDelete, MinHeight = 32, Padding = new Thickness(14, 0, 14, 0) };

    /// <summary>Полоса ошибки записи.</summary>
    private readonly Border _saveError = new() { Visibility = Visibility.Collapsed };

    /// <summary>Текст полосы ошибки записи.</summary>
    private readonly TextBlock _saveErrorText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    /// <summary>Текст замены или открытый секрет: единый источник для поля и скрытого поля.</summary>
    private string _value;

    /// <summary>Секрет показан открытым текстом.</summary>
    private bool _revealed;

    /// <summary>Идёт программная смена полей текста: обработчики ничего не делают.</summary>
    private bool _syncing;

    /// <summary>Снимок сокращения, с которым сравнивается текущее состояние.</summary>
    private string _baseline = string.Empty;

    /// <summary>Пользователь уже пробовал сохранить: ошибки полей показываются сразу.</summary>
    private bool _submitted;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="settings">Текущие настройки.</param>
    /// <param name="services">Запись сокращений, хранилище секретов и таблицы раскладок.</param>
    /// <param name="context">Окружение редакторов шагов.</param>
    /// <param name="dialogs">Вопросы с кнопками.</param>
    /// <param name="original">Сокращение для правки или <c>null</c> для нового.</param>
    /// <param name="close">Возврат к таблице.</param>
    /// <param name="openSnippet">Открывает редактор другого сокращения.</param>
    public SnippetEditorPage(
        SettingsService settings, SnippetServices services, StepContext context, IDialogs dialogs,
        Snippet? original, Action close, Action<Snippet> openSnippet)
    {
        _settings = settings;
        _saver = services.Saver;
        _tables = services.Tables.Tables();
        _dialogs = dialogs;
        _scriptsDir = context.ScriptsDir;
        _original = original;
        _close = close;
        _openSnippet = openSnippet;
        Focusable = true;
        IsTabStop = false;
        FocusVisualStyle = null;

        string? secretText = null;
        var missing = original is { Secret: true, SecretRef: { } key } && !services.Secrets.TryGet(key, out secretText);
        var draft = original is null ? new SnippetDraft() : SnippetDraft.From(original, secretText);
        _value = draft.Text;

        _keys.Text = draft.Keys;
        AutomationProperties.SetName(_keys, Strings.SnipKeys);
        _enabled.Style = (Style)FindResource("Km.Switch");
        _enabled.IsChecked = draft.Enabled;
        AutomationProperties.SetName(_enabled, Strings.EditorEnabled);
        _secret.Style = (Style)FindResource("Km.Switch");
        _secret.IsChecked = draft.Secret;
        AutomationProperties.SetName(_secret, Strings.SnipSecret);
        AutomationProperties.SetName(_text, Strings.SnipText);
        AutomationProperties.SetName(_password, Strings.SnipTextSecretName);
        _insert = new SegmentPicker<InsertMode>(
            Strings.InsertHow, [(InsertMode.Type, Strings.InsertType), (InsertMode.Clipboard, Strings.InsertClipboard)],
            original is { Secret: false } ? original.Insert : InsertMode.Type);
        _newline = new SegmentPicker<NewlineMode>(
            Strings.InsertNewline, [(NewlineMode.ShiftEnter, Strings.NewlineShiftEnter), (NewlineMode.Enter, Strings.NewlineEnter)], draft.Newline);
        _after = new StepListEditor(draft.After, context);
        _secretMissing.Text = Strings.SnipSecretMissing;
        _secretMissing.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        _insertSecret.Text = Strings.SnipSecretOnlyClipboard;
        _insertSecret.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _otherLayout.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _overlaps.SetResourceReference(TextBlock.ForegroundProperty, "Km.Warning");

        Content = BuildLayout();
        ActionBar = BuildActionBar();
        _delete.Visibility = original is null ? Visibility.Collapsed : Visibility.Visible;
        RefreshEnabledLabels();
        ApplyTextMode();
        _baseline = Snapshot(BuildDraft());

        _keys.TextChanged += (_, _) =>
        {
            TitleChanged?.Invoke();
            Revalidate();
        };
        _enabled.Click += (_, _) => OnEdited(refreshLabels: true);
        _secret.Click += (_, _) =>
        {
            _revealed = false;
            ApplyTextMode();
            OnEdited(refreshLabels: true);
        };
        _text.TextChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _value = _text.Text;
                OnEdited();
            }
        };
        _password.PasswordChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _value = _password.Password;
                OnEdited();
            }
        };
        _reveal.Click += (_, _) =>
        {
            _revealed = !_revealed;
            ApplyTextMode();
        };
        _insert.Changed += () => OnEdited();
        _newline.Changed += () => OnEdited();
        _after.Changed += () => OnEdited();

        Loaded += (_, _) =>
        {
            Revalidate();
            if (original is null)
            {
                _ = _keys.Focus();
            }
        };
    }

    /// <inheritdoc/>
    public event Action? TitleChanged;

    /// <inheritdoc/>
    public string TitleText => _keys.Text.Length == 0 ? Strings.SnipEditorNew : SnippetTable.DisplayKeys(_keys.Text);

    /// <inheritdoc/>
    public string UnsavedTemplate => Strings.UnsavedSnipText;

    /// <inheritdoc/>
    public UIElement ActionBar { get; }

    /// <inheritdoc/>
    public bool HasChanges => Snapshot(BuildDraft()) != _baseline;

    /// <inheritdoc/>
    public bool TrySave()
    {
        _submitted = true;
        var draft = BuildDraft();
        if (!Revalidate().CanSave)
        {
            return false;
        }

        _saveError.Visibility = Visibility.Collapsed;
        var result = _saver.Save(_original, draft);
        if (result != SnippetSaveResult.Saved)
        {
            ShowSaveError(result);
            return false;
        }

        _baseline = Snapshot(draft);
        return true;
    }

    /// <summary>Создаёт текст ошибки под полем.</summary>
    private static TextBlock ErrorText()
    {
        var text = new TextBlock { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Km.Error");
        return text;
    }

    /// <summary>Собирает страницу: карточка с полями, затем «После вставки».</summary>
    private StackPanel BuildLayout()
    {
        var card = new Border { Style = (Style)FindResource("Km.CardBorder"), Padding = new Thickness(20, 18, 20, 18) };
        var form = StepEditor.CreateForm();
        form.Margin = new Thickness(0);

        var keys = new StackPanel();
        keys.Children.Add(_keys);
        keys.Children.Add(_otherLayout);
        keys.Children.Add(_keysError);
        keys.Children.Add(_overlaps);
        StepEditor.AddRow(form, Strings.SnipKeys, keys, top: true, help: Strings.HelpSnipKeys);

        var enabled = new StackPanel { Orientation = Orientation.Horizontal };
        enabled.Children.Add(_enabled);
        enabled.Children.Add(_enabledLabel);
        StepEditor.AddRow(form, Strings.EditorEnabled, enabled);

        var secret = new StackPanel { Orientation = Orientation.Horizontal };
        secret.Children.Add(_secret);
        secret.Children.Add(_secretLabel);
        secret.Children.Add(KeyChipsBuilder.Label(Strings.SnipSecretHint, "Km.Text2", 12, margin: false));
        StepEditor.AddRow(form, Strings.SnipSecret, secret, help: Strings.HelpSnipSecret);

        StepEditor.AddRow(form, Strings.SnipText, BuildTextBlock(), top: true, stretch: true);

        var how = new StackPanel { Orientation = Orientation.Horizontal };
        how.Children.Add(_insert);
        how.Children.Add(_insertSecret);
        StepEditor.AddRow(form, Strings.InsertHow, how, help: Strings.HelpInsertHow);

        var newline = new StackPanel { Orientation = Orientation.Horizontal };
        newline.Children.Add(_newline);
        var newlineHint = KeyChipsBuilder.Label(Strings.SnipNewlineHint, "Km.Text2", 12, margin: false);
        newlineHint.Margin = new Thickness(12, 0, 0, 0);
        newline.Children.Add(newlineHint);
        StepEditor.AddRow(form, Strings.InsertNewline, newline, help: Strings.HelpInsertNewline);
        card.Child = form;

        var title = new TextBlock { Style = (Style)FindResource("Km.GroupTitle") };
        title.Inlines.Add(new Run(Strings.SnipAfter));
        var hint = new Run("  " + Strings.SnipAfterHint) { FontWeight = FontWeights.Normal };
        hint.SetResourceReference(TextElement.ForegroundProperty, "Km.Text2");
        title.Inlines.Add(hint);
        SettingsCards.AddHelp(title, Strings.SnipAfter, Strings.HelpSnipAfter);

        _saveError.Style = (Style)FindResource("Km.InfoBar");
        _saveError.Margin = new Thickness(0, 0, 0, 12);
        _saveError.Child = _saveErrorText;
        _saveError.SetResourceReference(Border.BorderBrushProperty, "Km.Error");

        var page = new StackPanel();
        page.Children.Add(_saveError);
        page.Children.Add(card);
        page.Children.Add(title);
        page.Children.Add(_after);
        return page;
    }

    /// <summary>Блок текста: поле, скрытое поле секрета, кнопка «Показать» и ошибки.</summary>
    private StackPanel BuildTextBlock()
    {
        var fields = new Grid();
        fields.Children.Add(_text);
        fields.Children.Add(_password);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_reveal, 1);
        row.Children.Add(fields);
        row.Children.Add(_reveal);

        var block = new StackPanel();
        block.Children.Add(row);
        block.Children.Add(_textError);
        block.Children.Add(_secretMissing);
        return block;
    }

    /// <summary>Нижняя панель: «Удалить сокращение» слева, «Отмена» и «Сохранить» справа.</summary>
    private Border BuildActionBar()
    {
        _delete.Click += (_, _) => OnDelete();
        return EditorActionBar.Create(
            this, _delete, _close,
            () =>
            {
                if (TrySave())
                {
                    _close();
                }
            });
    }

    /// <summary>Показывает поле или скрытое поле текста и нужный способ вставки, переносит текст в видимое поле.</summary>
    private void ApplyTextMode()
    {
        var secret = _secret.IsChecked == true;
        var hidden = secret && !_revealed;
        _syncing = true;
        _text.Text = _value;
        _password.Password = _value;
        _syncing = false;
        _text.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        _password.Visibility = hidden ? Visibility.Visible : Visibility.Collapsed;
        _reveal.Visibility = secret ? Visibility.Visible : Visibility.Collapsed;
        _reveal.Content = _revealed ? Strings.SnipHide : Strings.SnipShow;
        AutomationProperties.SetName(_reveal, _revealed ? Strings.SnipHide : Strings.SnipShow);
        _insert.Visibility = secret ? Visibility.Collapsed : Visibility.Visible;
        _insertSecret.Visibility = secret ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Подписи «Вкл.» / «Откл.» у переключателей.</summary>
    private void RefreshEnabledLabels()
    {
        _enabledLabel.Text = _enabled.IsChecked == true ? Strings.SwitchOn : Strings.SwitchOff;
        _secretLabel.Text = _secret.IsChecked == true ? Strings.SwitchOn : Strings.SwitchOff;
    }

    /// <summary>Поле изменилось: обновляет подписи и замечания.</summary>
    private void OnEdited(bool refreshLabels = false)
    {
        if (refreshLabels)
        {
            RefreshEnabledLabels();
        }

        Revalidate();
    }

    /// <summary>Собирает черновик из состояния полей.</summary>
    private SnippetDraft BuildDraft() => new()
    {
        Id = _original?.Id ?? string.Empty,
        Keys = _keys.Text,
        Enabled = _enabled.IsChecked == true,
        Secret = _secret.IsChecked == true,
        Text = _value,
        Insert = _insert.Value,
        Newline = _newline.Value,
        After = _after.Steps,
    };

    /// <summary>Снимок черновика для сравнения: тот же JSON, что пишется в файл; живёт только в памяти.</summary>
    private static string Snapshot(SnippetDraft draft) => SettingsJson.SerializeSnippet(draft.ToSnippet(null));

    /// <summary>Остальные сокращения: само редактируемое среди них не нужно, иначе оно занимало бы свои же клавиши.</summary>
    private IReadOnlyList<Snippet> Others() => [.. _settings.Current.Snippets.Where(s => !IsOriginal(s))];

    /// <summary>Это редактируемое сокращение: та же ссылка или тот же непустой идентификатор.</summary>
    private bool IsOriginal(Snippet snippet) => _original is not null
        && (ReferenceEquals(snippet, _original) || (_original.Id.Length > 0 && string.Equals(snippet.Id, _original.Id, StringComparison.Ordinal)));

    /// <summary>Проверяет сокращение; предупреждения о перекрытии и вид в другой раскладке видны всегда, ошибки — после первой попытки сохранить.</summary>
    private SnippetValidationResult Revalidate()
    {
        var draft = BuildDraft();
        var result = SnippetValidation.Validate(draft, Others(), _tables, _scriptsDir);

        var other = SnippetKeys.InOtherLayout(draft.Keys, _tables);
        _otherLayout.Text = other is null ? string.Empty : RuleSummary.Fill(Strings.SnipInOther, SnippetTable.DisplayKeys(other));
        _otherLayout.Visibility = other is null ? Visibility.Collapsed : Visibility.Visible;

        ShowKeysError(result);
        ShowOverlaps(result);
        _textError.Text = Strings.SnipErrTextEmpty;
        _textError.Visibility = result.Problems.HasFlag(SnippetProblems.TextEmpty) && _submitted ? Visibility.Visible : Visibility.Collapsed;
        if (_value.Length > 0)
        {
            _secretMissing.Visibility = Visibility.Collapsed;
        }

        _after.ShowProblems(result.StepProblems, noSteps: false, _submitted);
        return result;
    }

    /// <summary>Показывает ошибку сокращения: пустое, не набирается, занято (со ссылкой на занявшее).</summary>
    private void ShowKeysError(SnippetValidationResult result)
    {
        _keysError.Inlines.Clear();
        var problems = result.Problems;
        if (!_submitted || (problems & (SnippetProblems.KeysEmpty | SnippetProblems.KeysUntypable | SnippetProblems.KeysTaken)) == 0)
        {
            _keysError.Visibility = Visibility.Collapsed;
            return;
        }

        if (problems.HasFlag(SnippetProblems.KeysEmpty))
        {
            _keysError.Inlines.Add(new Run(Strings.SnipErrKeysEmpty));
        }
        else if (problems.HasFlag(SnippetProblems.KeysUntypable))
        {
            _keysError.Inlines.Add(new Run(Strings.SnipErrUntypable));
        }
        else if (result.Taken is { } taken)
        {
            _keysError.Inlines.Add(new Run(RuleSummary.Fill(Strings.SnipErrTaken, SnippetTable.DisplayKeys(taken.Keys)) + " "));
            var link = new Hyperlink(new Run(Strings.SnipOpenSnippet));
            link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
            link.Click += (_, _) => _openSnippet(taken);
            _keysError.Inlines.Add(link);
        }

        _keysError.Visibility = Visibility.Visible;
    }

    /// <summary>Показывает предупреждения «одно сокращение — начало другого».</summary>
    private void ShowOverlaps(SnippetValidationResult result)
    {
        _overlaps.Inlines.Clear();
        foreach (var overlap in result.Overlaps)
        {
            var template = overlap.Relation == SnippetRelation.PrefixOf ? Strings.SnipWarnPrefix : Strings.SnipWarnStartsWith;
            if (_overlaps.Inlines.Count > 0)
            {
                _overlaps.Inlines.Add(new LineBreak());
            }

            _overlaps.Inlines.Add(new Run(RuleSummary.Fill(template, SnippetTable.DisplayKeys(overlap.Other.Keys))));
        }

        _overlaps.Visibility = result.Overlaps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Удаляет сокращение после подтверждения и возвращается к таблице.</summary>
    private void OnDelete()
    {
        if (_original is null
            || _dialogs.Ask(
                Strings.DeleteSnipTitle, RuleSummary.Fill(Strings.DeleteSnipText, SnippetTable.DisplayKeys(_original.Keys)),
                Strings.DialogDelete, Strings.DialogCancel) != 0)
        {
            return;
        }

        var result = _saver.Delete(_original);
        if (result == SnippetSaveResult.Saved)
        {
            _baseline = Snapshot(BuildDraft());
            _close();
        }
        else
        {
            ShowSaveError(result);
        }
    }

    /// <summary>Показывает полосу с причиной, почему не записалось.</summary>
    private void ShowSaveError(SnippetSaveResult result)
    {
        _saveErrorText.Text = result == SnippetSaveResult.SecretsFailed ? Strings.SnipSecretFailed : Strings.RuleSaveFailed;
        _saveError.Visibility = Visibility.Visible;
    }
}
