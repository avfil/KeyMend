using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>Страница-редактор, из которой нельзя уйти молча, если в ней есть несохранённые изменения (SPEC 5).</summary>
internal interface IEditorPage
{
    /// <summary>Заголовок для пути «Правила › Имя».</summary>
    string TitleText { get; }

    /// <summary>Шаблон вопроса о несохранённых изменениях; <c>{0}</c> — <see cref="TitleText"/>.</summary>
    string UnsavedTemplate { get; }

    /// <summary>Заголовок изменился: название правила правят в поле.</summary>
    event Action? TitleChanged;

    /// <summary>Панель «Сохранить», «Отмена», «Удалить», которую окно прижимает к низу поверх содержимого.</summary>
    UIElement ActionBar { get; }

    /// <summary>Есть изменения, которых нет в настройках.</summary>
    bool HasChanges { get; }

    /// <summary>Проверяет и сохраняет правило.</summary>
    /// <returns><c>true</c>, если сохранено; <c>false</c>, если мешают ошибки (они показаны на странице).</returns>
    bool TrySave();
}

/// <summary>
/// Редактор правила (SPEC 5): название, «включено», клавиши с проверками, «где работает» и шаги. Собирается из кода, потому что
/// шаги разных видов состоят из разных редакторов. «Сохранить» проверяет правило и пишет настройки, остальные страницы
/// применяют изменения сразу, а здесь они ждут кнопки (F11).
/// </summary>
internal sealed class RuleEditorPage : UserControl, IEditorPage
{
    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Сочетания смены языка в Windows.</summary>
    private readonly ILanguageHotkeysSource _hotkeys;

    /// <summary>Вопросы с кнопками.</summary>
    private readonly IDialogs _dialogs;

    /// <summary>Возврат к таблице правил без вопросов.</summary>
    private readonly Action _close;

    /// <summary>Правило, которое правят; <c>null</c> — новое.</summary>
    private readonly Rule? _original;

    /// <summary>Название.</summary>
    private readonly TextBox _name = new() { Width = 360, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Ошибка названия.</summary>
    private readonly TextBlock _nameError = ErrorText();

    /// <summary>Переключатель «Включено».</summary>
    private readonly CheckBox _enabled = new() { VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Подпись «Вкл./Откл.» постоянной ширины.</summary>
    private readonly TextBlock _enabledLabel = new() { Width = 44, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Поле клавиш.</summary>
    private readonly TriggerBox _trigger = new();

    /// <summary>Ошибка клавиш: не записаны.</summary>
    private readonly TextBlock _triggerError = ErrorText();

    /// <summary>Выбор «Во всех программах».</summary>
    private readonly RadioButton _whereAll = new() { Content = Strings.WhereAll, GroupName = "RuleWhere" };

    /// <summary>Выбор «Только в выбранных».</summary>
    private readonly RadioButton _whereSelected = new() { Content = Strings.WhereSelected, GroupName = "RuleWhere" };

    /// <summary>Блок выбранных программ.</summary>
    private readonly StackPanel _programsBlock = new() { Margin = new Thickness(0, 10, 0, 0) };

    /// <summary>Плашки выбранных программ.</summary>
    private readonly WrapPanel _programChips = new();

    /// <summary>Поле имени новой программы.</summary>
    private readonly TextBox _programInput = new() { Width = 260, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Ошибка программ.</summary>
    private readonly TextBlock _programsError = ErrorText();

    /// <summary>Папка скриптов: от неё проверяются файлы шагов запуска.</summary>
    private readonly string _scriptsDir;

    /// <summary>Шаги правила.</summary>
    private readonly StepListEditor _stepList;

    /// <summary>Кнопка «Удалить правило».</summary>
    private readonly Button _delete = new() { Content = Strings.DeleteRule, MinHeight = 32, Padding = new Thickness(14, 0, 14, 0) };

    /// <summary>Полоса ошибки записи файла.</summary>
    private readonly Border _saveError = new() { Visibility = Visibility.Collapsed };

    /// <summary>Программы режима «только в выбранных».</summary>
    private IReadOnlyList<string> _processes;

    /// <summary>Снимок правила, с которым сравнивается текущее состояние.</summary>
    private string _baseline = string.Empty;

    /// <summary>Пользователь уже пробовал сохранить: ошибки полей показываются сразу.</summary>
    private bool _submitted;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="recording">Запись клавиш через хук.</param>
    /// <param name="hotkeys">Сочетания смены языка в Windows.</param>
    /// <param name="context">Окружение редакторов шагов: языки, папка скриптов, выбор файлов.</param>
    /// <param name="dialogs">Вопросы с кнопками.</param>
    /// <param name="original">Правило для правки или <c>null</c> для нового.</param>
    /// <param name="newStep">Первый шаг нового правила; для правки не используется.</param>
    /// <param name="close">Возврат к таблице.</param>
    /// <param name="openRule">Открывает редактор другого правила (ссылка «Открыть это правило»).</param>
    public RuleEditorPage(
        SettingsService settings, ITriggerRecording recording, ILanguageHotkeysSource hotkeys, StepContext context, IDialogs dialogs,
        Rule? original, Step? newStep, Action close, Action<Rule> openRule)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _dialogs = dialogs;
        _scriptsDir = context.ScriptsDir;
        _original = original;
        _close = close;
        Focusable = true;
        IsTabStop = false;
        FocusVisualStyle = null;

        var draft = original is null ? new RuleDraft { Steps = newStep is null ? [] : [newStep] } : RuleDraft.From(original);
        _processes = draft.Processes;
        _trigger.Initialize(recording);
        _trigger.SetTrigger(draft.Trigger);
        _trigger.OpenRuleRequested += openRule;

        _name.Text = draft.Name;
        _enabled.Style = (Style)FindResource("Km.Switch");
        _enabled.IsChecked = draft.Enabled;
        AutomationProperties.SetName(_enabled, Strings.EditorEnabled);
        AutomationProperties.SetName(_name, Strings.EditorName);
        _whereAll.IsChecked = !draft.OnlySelected;
        _whereSelected.IsChecked = draft.OnlySelected;

        _stepList = new StepListEditor(draft.Steps, context);

        Content = BuildLayout();
        ActionBar = BuildActionBar();
        _delete.Visibility = original is null ? Visibility.Collapsed : Visibility.Visible;
        RefreshPrograms();
        RefreshEnabledLabel();
        _baseline = Snapshot(BuildDraft());

        _name.TextChanged += (_, _) =>
        {
            TitleChanged?.Invoke();
            Revalidate();
        };
        _enabled.Click += (_, _) =>
        {
            RefreshEnabledLabel();
            Revalidate();
        };
        _trigger.TriggerChanged += _ => Revalidate();
        _whereAll.Checked += OnWhereChecked;
        _whereSelected.Checked += OnWhereChecked;
        _stepList.Changed += () => Revalidate();

        Loaded += (_, _) => Revalidate();
    }

    /// <inheritdoc/>
    public event Action? TitleChanged;

    /// <inheritdoc/>
    public string TitleText => string.IsNullOrWhiteSpace(_name.Text) ? Strings.EditorNewRule : _name.Text.Trim();

    /// <inheritdoc/>
    public string UnsavedTemplate => Strings.UnsavedText;

    /// <inheritdoc/>
    public UIElement ActionBar { get; }

    /// <inheritdoc/>
    public bool HasChanges => Snapshot(BuildDraft()) != _baseline;

    /// <inheritdoc/>
    public bool TrySave()
    {
        _submitted = true;
        var draft = BuildDraft();
        var result = Revalidate();
        if (!result.CanSave || draft.Trigger is not { } trigger)
        {
            return false;
        }

        var rule = draft.ToRule(trigger);
        _saveError.Visibility = Visibility.Collapsed;
        if (!_settings.Update(s => s with { Rules = RuleList.Upsert(s.Rules, _original, rule, Guid.NewGuid) }))
        {
            ShowSaveError();
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

    /// <summary>Собирает страницу: карточка с полями, затем шаги.</summary>
    private StackPanel BuildLayout()
    {
        var card = new Border { Style = (Style)FindResource("Km.CardBorder"), Padding = new Thickness(20, 18, 20, 18) };
        var form = StepEditor.CreateForm();
        form.Margin = new Thickness(0);

        var nameBox = new StackPanel();
        nameBox.Children.Add(_name);
        nameBox.Children.Add(_nameError);
        StepEditor.AddRow(form, Strings.EditorName, nameBox);

        var enabled = new StackPanel { Orientation = Orientation.Horizontal };
        enabled.Children.Add(_enabled);
        enabled.Children.Add(_enabledLabel);
        StepEditor.AddRow(form, Strings.EditorEnabled, enabled);

        var keys = new StackPanel();
        keys.Children.Add(_trigger);
        keys.Children.Add(_triggerError);
        StepEditor.AddRow(form, Strings.EditorKeys, keys, top: true, stretch: true, help: Strings.HelpKeys);

        var whereRow = new WrapPanel();
        _whereSelected.Margin = new Thickness(24, 0, 0, 0);
        whereRow.Children.Add(_whereAll);
        whereRow.Children.Add(_whereSelected);
        var where = new StackPanel();
        where.Children.Add(whereRow);
        where.Children.Add(BuildProgramsBlock());
        StepEditor.AddRow(form, Strings.EditorWhere, where, top: true, help: Strings.HelpWhere);
        card.Child = form;

        var title = new TextBlock { Style = (Style)FindResource("Km.GroupTitle") };
        title.Inlines.Add(new System.Windows.Documents.Run(Strings.EditorSteps));
        var hint = new System.Windows.Documents.Run("  " + Strings.EditorStepsHint) { FontWeight = FontWeights.Normal };
        hint.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Km.Text2");
        title.Inlines.Add(hint);

        var page = new StackPanel();
        _saveError.Style = (Style)FindResource("Km.InfoBar");
        _saveError.Margin = new Thickness(0, 0, 0, 12);
        _saveError.Child = new TextBlock { Text = Strings.RuleSaveFailed, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        _saveError.SetResourceReference(Border.BorderBrushProperty, "Km.Error");
        page.Children.Add(_saveError);
        page.Children.Add(card);
        page.Children.Add(title);
        page.Children.Add(_stepList);
        return page;
    }

    /// <summary>Блок выбранных программ: плашки, поле имени и кнопка «Добавить».</summary>
    private StackPanel BuildProgramsBlock()
    {
        var add = new Button { Content = Strings.ProgramAdd, MinHeight = 32, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 0, 14, 0) };
        add.Click += (_, _) => AddProgram();
        _programInput.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                AddProgram();
            }
        };
        AutomationProperties.SetName(_programInput, Strings.ProgramPlaceholder);
        _programInput.ToolTip = Strings.ProgramPlaceholder;

        var input = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        input.Children.Add(_programInput);
        input.Children.Add(add);
        _programsBlock.Children.Add(_programChips);
        _programsBlock.Children.Add(input);
        _programsBlock.Children.Add(_programsError);
        return _programsBlock;
    }

    /// <summary>Нижняя панель: «Удалить правило» слева, «Отмена» и «Сохранить» справа.</summary>
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

    /// <summary>Собирает черновик из состояния полей.</summary>
    private RuleDraft BuildDraft() => new()
    {
        Id = _original?.Id ?? string.Empty,
        Name = _name.Text,
        Enabled = _enabled.IsChecked == true,
        Trigger = _trigger.Trigger,
        OnlySelected = _whereSelected.IsChecked == true,
        Processes = _processes,
        Steps = _stepList.Steps,
    };

    /// <summary>Снимок черновика для сравнения: тот же JSON, что пишется в файл, плюс то, чего в правиле нет.</summary>
    private static string Snapshot(RuleDraft draft)
    {
        var placeholder = new TapTrigger { Keys = ModifierSet.None };
        var rule = draft.ToRule(draft.Trigger ?? placeholder) with { Name = draft.Name, Processes = draft.Processes };
        return SettingsJson.SerializeRule(rule) + draft.OnlySelected + (draft.Trigger is null);
    }

    /// <summary>Остальные правила: само редактируемое среди них не нужно, иначе оно занимало бы свои же клавиши.</summary>
    private IReadOnlyList<Rule> Others() => [.. _settings.Current.Rules.Where(r => !IsOriginal(r))];

    /// <summary>Это редактируемое правило: та же ссылка или тот же непустой идентификатор.</summary>
    private bool IsOriginal(Rule rule) => _original is not null
        && (ReferenceEquals(rule, _original) || (_original.Id.Length > 0 && string.Equals(rule.Id, _original.Id, StringComparison.Ordinal)));

    /// <summary>Проверяет правило, показывает замечания к клавишам и (после первой попытки сохранить) ошибки полей.</summary>
    private RuleValidationResult Revalidate()
    {
        var result = RuleValidation.Validate(BuildDraft(), Others(), _hotkeys.Read(), _scriptsDir);
        _trigger.ShowIssues(result.TriggerIssues);
        Show(_nameError, Strings.ErrNameEmpty, result.Problems.HasFlag(RuleProblems.NameEmpty));
        Show(_triggerError, Strings.ErrTriggerMissing, result.Problems.HasFlag(RuleProblems.TriggerMissing));
        Show(_programsError, Strings.ErrNoPrograms, result.Problems.HasFlag(RuleProblems.NoPrograms));
        _stepList.ShowProblems(result.StepProblems, result.Problems.HasFlag(RuleProblems.NoSteps), _submitted);
        return result;
    }

    /// <summary>Показывает текст ошибки, если она есть и владелец уже пробовал сохранить.</summary>
    private void Show(TextBlock block, string text, bool has)
    {
        block.Text = text;
        block.Visibility = has && _submitted ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Подпись «Вкл.» или «Откл.» у переключателя.</summary>
    private void RefreshEnabledLabel() => _enabledLabel.Text = _enabled.IsChecked == true ? Strings.SwitchOn : Strings.SwitchOff;

    /// <summary>Переключение «Во всех программах» / «Только в выбранных».</summary>
    private void OnWhereChecked(object sender, RoutedEventArgs e)
    {
        RefreshPrograms();
        Revalidate();
    }

    /// <summary>Показывает блок программ, когда выбрано «Только в выбранных», и перерисовывает плашки.</summary>
    private void RefreshPrograms()
    {
        _programsBlock.Visibility = _whereSelected.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _programChips.Children.Clear();
        foreach (var name in _processes)
        {
            var label = KeyChipsBuilder.Label(name, "Km.Text", 13, margin: false);
            var remove = new Button { Style = (Style)FindResource("Km.IconButton"), Content = "✕", Width = 24, Height = 24, FontSize = 11 };
            AutomationProperties.SetName(remove, RuleSummary.Fill(Strings.ProgramRemove, name));
            var program = name;
            remove.Click += (_, _) =>
            {
                _processes = [.. _processes.Where(p => !string.Equals(p, program, StringComparison.OrdinalIgnoreCase))];
                RefreshPrograms();
                Revalidate();
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(label);
            row.Children.Add(remove);
            _programChips.Children.Add(new Border { Style = (Style)FindResource("Km.KeyChip"), Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(10, 2, 2, 2), Child = row });
        }
    }

    /// <summary>Добавляет введённую программу в список.</summary>
    private void AddProgram()
    {
        var next = ProcessNames.Add(_processes, _programInput.Text);
        if (ProcessNames.Normalize(_programInput.Text) is not null)
        {
            _programInput.Clear();
        }

        _processes = next;
        RefreshPrograms();
        Revalidate();
    }

    /// <summary>Удаляет правило после подтверждения и возвращается к таблице.</summary>
    private void OnDelete()
    {
        if (_original is null
            || _dialogs.Ask(Strings.DeleteRuleTitle, RuleSummary.Fill(Strings.DeleteRuleText, _original.Name), Strings.DialogDelete, Strings.DialogCancel) != 0)
        {
            return;
        }

        if (_settings.Update(s => s with { Rules = RuleList.Remove(s.Rules, _original) }))
        {
            _baseline = Snapshot(BuildDraft());
            _close();
        }
        else
        {
            ShowSaveError();
        }
    }

    /// <summary>Показывает полосу «Не удалось сохранить настройки».</summary>
    private void ShowSaveError() => _saveError.Visibility = Visibility.Visible;
}
