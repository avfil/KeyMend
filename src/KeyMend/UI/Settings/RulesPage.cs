using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyMend.Infrastructure;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «Правила» (SPEC 5): таблица правил по группам, чипы-фильтры, поиск, «Вкл.» в строке, «Добавить правило».
/// Страница собирается из кода, как поле «Клавиши»: строки таблицы зависят от данных. Правки применяются сразу.
/// </summary>
internal sealed class RulesPage : UserControl
{
    /// <summary>Геометрии значков групп на сетке 16×16.</summary>
    private static readonly Dictionary<RuleGroup, string> _groupIcons = new Dictionary<RuleGroup, string>
    {
        [RuleGroup.Language] = "M2,3 H9 M5.5,1.8 V3 M3.5,3 C4,5.5 5.5,7.5 8,8.5 M7.5,3 C7,5.5 5.5,7.5 3,8.5 M8.5,14.2 L11,8.2 L13.5,14.2 M9.3,12.2 H12.7",
        [RuleGroup.Correction] = "M2,5 H13 L10.5,2.5 M14,11 H3 L5.5,13.5",
        [RuleGroup.Launch] = "M3.5,3 H12.5 A1.5,1.5 0 0 1 14,4.5 V11.5 A1.5,1.5 0 0 1 12.5,13 H3.5 A1.5,1.5 0 0 1 2,11.5 V4.5 A1.5,1.5 0 0 1 3.5,3 Z M2,6 H14 M6.5,8.5 L8.5,10 L6.5,11.5",
        [RuleGroup.Power] = "M13,9.5 A5.5,5.5 0 1 1 6.5,3 A4.5,4.5 0 0 0 13,9.5 Z",
        [RuleGroup.Other] = "M3,8 H3.01 M8,8 H8.01 M13,8 H13.01",
    };

    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Сочетания смены языка в Windows: нужны проверке конфликтов при включении.</summary>
    private readonly ILanguageHotkeysSource _hotkeys;

    /// <summary>Вопросы с кнопками.</summary>
    private readonly IDialogs _dialogs;

    /// <summary>Открывает редактор правила.</summary>
    private readonly Action<Rule> _openRule;

    /// <summary>Открывает редактор нового правила в группе.</summary>
    private readonly Action<RuleGroup> _addRule;

    /// <summary>Полоса над таблицей: отказ включить правило или ошибка записи.</summary>
    private readonly Border _notice = new() { Visibility = Visibility.Collapsed };

    /// <summary>Текст полосы.</summary>
    private readonly TextBlock _noticeText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    /// <summary>Панель чипов.</summary>
    private readonly WrapPanel _chips = new() { Margin = new Thickness(0, 14, 0, 4) };

    /// <summary>Поиск по названию и клавишам.</summary>
    private readonly TextBox _search = new() { Width = 280, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Подсказка в пустом поиске.</summary>
    private readonly TextBlock _searchHint = new() { Text = Strings.RulesSearch, IsHitTestVisible = false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };

    /// <summary>Фильтр по состоянию.</summary>
    private readonly ComboBox _state = new() { MinWidth = 150, MinHeight = 32 };

    /// <summary>Таблица: заголовок, группы и строки.</summary>
    private readonly StackPanel _table = new();

    /// <summary>Выбранная группа; <c>null</c> — «Все».</summary>
    private RuleGroup? _group;

    /// <summary>Идёт перестройка: обработчики выбора ничего не делают.</summary>
    private bool _building;

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="hotkeys">Сочетания смены языка в Windows.</param>
    /// <param name="dialogs">Вопросы с кнопками.</param>
    /// <param name="openRule">Открывает редактор правила.</param>
    /// <param name="addRule">Открывает редактор нового правила в группе.</param>
    /// <param name="initialGroup">Группа, выбранная при открытии; <c>null</c> — «Все».</param>
    public RulesPage(
        SettingsService settings, ILanguageHotkeysSource hotkeys, IDialogs dialogs, Action<Rule> openRule, Action<RuleGroup> addRule,
        RuleGroup? initialGroup = null)
    {
        _group = initialGroup;
        _settings = settings;
        _hotkeys = hotkeys;
        _dialogs = dialogs;
        _openRule = openRule;
        _addRule = addRule;
        Content = BuildLayout();
        Rebuild();
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            Rebuild();
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Правила изменились: правкой из окна или файла.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => Rebuild();

    /// <summary>Собирает неподвижную часть страницы.</summary>
    private StackPanel BuildLayout()
    {
        var lead = new TextBlock { Text = Strings.RulesLead };
        SettingsCards.AddHelp(lead, Strings.SectionRules, Strings.HelpRulesLead);
        lead.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");

        _notice.Style = (Style)FindResource("Km.InfoBar");
        _notice.Margin = new Thickness(0, 12, 0, 0);
        _notice.Child = _noticeText;

        foreach (var text in new[] { Strings.RuleFilterAll, Strings.RuleFilterEnabled, Strings.RuleFilterDisabled })
        {
            _ = _state.Items.Add(text);
        }

        _state.SelectedIndex = 0;
        _state.SelectionChanged += (_, _) => OnFilterChanged();
        AutomationProperties.SetName(_state, Strings.RuleFilterState);
        _searchHint.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _search.TextChanged += (_, _) =>
        {
            _searchHint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            OnFilterChanged();
        };
        AutomationProperties.SetName(_search, Strings.RulesSearch);

        var searchBox = new Grid();
        searchBox.Children.Add(_search);
        searchBox.Children.Add(_searchHint);
        var add = new Button { Style = (Style)FindResource("Km.AccentButton"), Content = Strings.RulesAdd + "  ▾" };
        add.Click += OnAddClick;
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 14), LastChildFill = false };
        DockPanel.SetDock(add, Dock.Right);
        row.Children.Add(add);
        searchBox.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(searchBox, Dock.Left);
        row.Children.Add(searchBox);
        _state.Margin = new Thickness(0);
        DockPanel.SetDock(_state, Dock.Left);
        row.Children.Add(_state);

        var panel = new StackPanel();
        panel.Children.Add(lead);
        panel.Children.Add(_notice);
        panel.Children.Add(_chips);
        panel.Children.Add(row);
        panel.Children.Add(_table);
        return panel;
    }

    /// <summary>Условия отбора из полей страницы.</summary>
    private RuleTableFilter CurrentFilter() => new(
        _group,
        _state.SelectedIndex switch
        {
            1 => RuleStateFilter.Enabled,
            2 => RuleStateFilter.Disabled,
            _ => RuleStateFilter.All,
        },
        _search.Text);

    /// <summary>Поменяли состояние или поиск.</summary>
    private void OnFilterChanged()
    {
        if (!_building)
        {
            Rebuild();
        }
    }

    /// <summary>Перестраивает чипы и таблицу по настройкам и фильтру.</summary>
    private void Rebuild()
    {
        _building = true;
        _notice.Visibility = Visibility.Collapsed;
        var rules = _settings.Current.Rules;
        var view = RuleTable.Build(rules, CurrentFilter());
        BuildChips(view);
        BuildTable(view, rules);
        _building = false;
    }

    /// <summary>Рисует чипы: «Все» и группы, в которых есть правила (выбранная — всегда).</summary>
    private void BuildChips(RuleTableView view)
    {
        _chips.Children.Clear();
        _chips.Children.Add(Chip(Strings.RuleFilterAll, view.Total, _group is null, null));
        foreach (var group in RuleGroups.All.Where(g => view.Counts[g] > 0 || g == _group))
        {
            _chips.Children.Add(Chip(RuleGroups.Title(group), view.Counts[group], _group == group, group));
        }
    }

    /// <summary>Один чип с числом.</summary>
    private RadioButton Chip(string title, int count, bool selected, RuleGroup? group)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = title });
        content.Children.Add(new TextBlock { Text = count.ToString(CultureInfo.CurrentCulture), Margin = new Thickness(8, 0, 0, 0), Opacity = 0.7 });
        var chip = new RadioButton { Style = (Style)FindResource("Km.Chip"), Content = content, IsChecked = selected, GroupName = "RuleGroup" };
        AutomationProperties.SetName(chip, title);
        chip.Click += (_, _) =>
        {
            _group = group;
            Rebuild();
        };
        return chip;
    }

    /// <summary>Рисует таблицу: заголовок колонок, группы со строками или сообщение, что ничего не найдено.</summary>
    private void BuildTable(RuleTableView view, IReadOnlyList<Rule> allRules)
    {
        _table.Children.Clear();
        var card = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Km.Card");
        card.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        var body = new StackPanel();
        card.Child = body;
        body.Children.Add(HeaderRow());
        if (view.Groups.Count == 0)
        {
            var empty = new TextBlock { Text = Strings.RulesEmpty, Margin = new Thickness(16, 20, 16, 20) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
            body.Children.Add(empty);
        }

        foreach (var group in view.Groups)
        {
            body.Children.Add(GroupRow(group));
            foreach (var rule in group.Rules)
            {
                body.Children.Add(RuleRow(rule, allRules));
            }
        }

        _table.Children.Add(card);
    }

    /// <summary>Колонки, общие для заголовка и строк: Вкл., Название, Клавиши, Что делает, Где работает, меню.</summary>
    private static Grid Columns()
    {
        var grid = new Grid { MinHeight = 40 };
        foreach (var width in new[] { new GridLength(64), new GridLength(1.3, GridUnitType.Star), new GridLength(1.8, GridUnitType.Star), new GridLength(1.5, GridUnitType.Star), new GridLength(1.2, GridUnitType.Star), new GridLength(44) })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }

        return grid;
    }

    /// <summary>Строка заголовков колонок.</summary>
    private static Grid HeaderRow()
    {
        var grid = Columns();
        grid.MinHeight = 36;
        var titles = new[] { Strings.ColEnabled, Strings.ColName, Strings.ColKeys, Strings.ColAction, Strings.ColWhere };
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = KeyChipsBuilder.Label(titles[i], "Km.Text2", 12);
            cell.Margin = new Thickness(i == 0 ? 16 : 8, 0, 0, 0);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    /// <summary>Заголовок группы: значок, название, число.</summary>
    private static Border GroupRow(RuleTableGroup group)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var icon = new Path
        {
            Data = Geometry.Parse(_groupIcons[group.Group]),
            Width = 16,
            Height = 16,
            StrokeThickness = 1.3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Margin = new Thickness(0, 0, 10, 0),
        };
        icon.SetResourceReference(Shape.StrokeProperty, "Km.Text");
        panel.Children.Add(icon);
        var title = KeyChipsBuilder.Label(RuleGroups.Title(group.Group), "Km.Text", 13);
        title.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(title);
        panel.Children.Add(KeyChipsBuilder.Label(group.Rules.Count.ToString(CultureInfo.CurrentCulture), "Km.Text2", 12));
        var row = new Border { Height = 36, Child = panel, BorderThickness = new Thickness(0, 1, 0, 0) };
        row.SetResourceReference(Border.BackgroundProperty, "Km.Card2");
        row.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        return row;
    }

    /// <summary>Строка правила.</summary>
    private Border RuleRow(Rule rule, IReadOnlyList<Rule> allRules)
    {
        var grid = Columns();
        var toggle = new CheckBox { Style = (Style)FindResource("Km.Switch"), IsChecked = rule.Enabled, Margin = new Thickness(16, 0, 0, 0) };
        AutomationProperties.SetName(toggle, Fill(Strings.RuleToggleLabel, rule.Name));
        toggle.Click += (_, _) => OnToggle(rule, toggle);
        grid.Children.Add(toggle);

        var link = new Hyperlink(new Run(rule.Name));
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
        link.Click += (_, _) => _openRule(rule);
        var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 4), ToolTip = rule.Name };
        name.Inlines.Add(link);
        Place(grid, name, 1);

        var keys = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 4, 8, 4) };
        KeyChipsBuilder.Fill(keys, TriggerDisplay.Chips(rule.Trigger), (Style)FindResource("Km.KeyChip"), compact: true);
        AddDelayBadge(keys, rule, allRules);
        Place(grid, keys, 2);

        Place(grid, Cell(RuleSummary.Action(rule), null), 3);
        Place(grid, Cell(RuleSummary.Where(rule), RuleSummary.WhereDetails(rule)), 4);

        var more = new Button { Style = (Style)FindResource("Km.IconButton"), Content = "⋯", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetName(more, Strings.RuleMore);
        more.Click += (_, _) => ShowRuleMenu(more, rule);
        Grid.SetColumn(more, 5);
        grid.Children.Add(more);

        if (!rule.Enabled)
        {
            foreach (UIElement cell in grid.Children)
            {
                if (cell != toggle && cell != more)
                {
                    cell.Opacity = 0.55;
                }
            }
        }

        var row = new Border { Child = grid, BorderThickness = new Thickness(0, 1, 0, 0) };
        row.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        return row;
    }

    /// <summary>Кладёт элемент в колонку.</summary>
    private static void Place(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    /// <summary>Текстовая ячейка с обрезкой по ширине и, при желании, подсказкой.</summary>
    private static TextBlock Cell(string text, string? tip)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 4, 8, 4),
            ToolTip = tip ?? text,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text");
        return block;
    }

    /// <summary>Добавляет бейдж «через N мс», если одиночное правило ждёт конец интервала двойного на тех же клавишах.</summary>
    private void AddDelayBadge(Panel keys, Rule rule, IReadOnlyList<Rule> allRules)
    {
        if (!rule.Enabled || rule.Trigger.DoubleMs is not null)
        {
            return;
        }

        var issue = TriggerChecks.Check(rule, allRules, _hotkeys.Read()).FirstOrDefault(i => i.Kind == TriggerIssueKind.DoubleDelay);
        if (issue is null)
        {
            return;
        }

        var badge = new Border
        {
            Style = (Style)FindResource("Km.Badge"),
            ToolTip = TriggerDisplay.Message(issue, rule.Trigger),
            Child = KeyChipsBuilder.Label(Fill(Strings.RuleDelayBadge, issue.DelayMs?.ToString(CultureInfo.CurrentCulture)), "Km.Warning", 11, margin: false),
        };
        keys.Children.Add(badge);
    }

    /// <summary>Переключатель «Вкл.»: выключение сразу сохраняется, включение — только если клавиши свободны.</summary>
    private void OnToggle(Rule rule, CheckBox toggle)
    {
        var enable = toggle.IsChecked == true;
        if (enable)
        {
            var candidate = rule with { Enabled = true };
            var error = TriggerChecks.Check(candidate, _settings.Current.Rules, _hotkeys.Read())
                .FirstOrDefault(i => i.Severity == TriggerIssueSeverity.Error);
            if (error is not null)
            {
                toggle.IsChecked = false;
                ShowRefusal(rule, error);
                return;
            }
        }

        _notice.Visibility = Visibility.Collapsed;
        if (!_settings.Update(s => s with { Rules = RuleList.Upsert(s.Rules, rule, rule with { Enabled = enable }, Guid.NewGuid) }))
        {
            toggle.IsChecked = !enable;
            ShowNotice(Strings.RuleSaveFailed, "Km.Error");
        }
    }

    /// <summary>Объясняет, почему правило не включилось, со ссылкой на мешающее правило.</summary>
    private void ShowRefusal(Rule rule, TriggerIssue error)
    {
        _noticeText.Inlines.Clear();
        _noticeText.Inlines.Add(new Run(Fill(Strings.RuleNotEnabled, rule.Name, TriggerDisplay.Message(error, rule.Trigger))));
        if (error is { Kind: TriggerIssueKind.Busy, Other: { } other })
        {
            var link = new Hyperlink(new Run(Strings.TriggerOpenRule));
            link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
            link.Click += (_, _) => _openRule(other);
            _noticeText.Inlines.Add(new Run(" "));
            _noticeText.Inlines.Add(link);
        }

        _notice.SetResourceReference(Border.BorderBrushProperty, "Km.Error");
        _notice.Visibility = Visibility.Visible;
    }

    /// <summary>Показывает простое сообщение над таблицей.</summary>
    private void ShowNotice(string text, string edgeKey)
    {
        _noticeText.Inlines.Clear();
        _noticeText.Inlines.Add(new Run(text));
        _notice.SetResourceReference(Border.BorderBrushProperty, edgeKey);
        _notice.Visibility = Visibility.Visible;
    }

    /// <summary>Меню «⋯»: изменить, дублировать, удалить.</summary>
    private void ShowRuleMenu(Button anchor, Rule rule)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Items.Add(MenuItem(Strings.RuleMenuEdit, () => _openRule(rule)));
        menu.Items.Add(MenuItem(Strings.RuleMenuDuplicate, () => Duplicate(rule)));
        menu.Items.Add(MenuItem(Strings.RuleMenuDelete, () => Delete(rule)));
        menu.IsOpen = true;
    }

    /// <summary>Пункт контекстного меню.</summary>
    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Копия правила: выключенная, чтобы не занимать клавиши оригинала, под оригиналом.</summary>
    private void Duplicate(Rule rule)
    {
        var copy = rule with { Id = string.Empty, Name = Fill(Strings.RuleCopyName, rule.Name), Enabled = false };
        if (!_settings.Update(s => s with { Rules = RuleList.InsertAfter(s.Rules, rule, copy, Guid.NewGuid) }))
        {
            ShowNotice(Strings.RuleSaveFailed, "Km.Error");
        }
    }

    /// <summary>Удаляет правило после подтверждения.</summary>
    private void Delete(Rule rule)
    {
        if (_dialogs.Ask(Strings.DeleteRuleTitle, Fill(Strings.DeleteRuleText, rule.Name), Strings.DialogDelete, Strings.DialogCancel) != 0)
        {
            return;
        }

        if (!_settings.Update(s => s with { Rules = RuleList.Remove(s.Rules, rule) }))
        {
            ShowNotice(Strings.RuleSaveFailed, "Km.Error");
        }
    }

    /// <summary>«Добавить правило ▾»: выбор группы нового правила.</summary>
    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Items.Add(MenuItem(Strings.AddRuleLanguage, () => _addRule(RuleGroup.Language)));
        menu.Items.Add(MenuItem(Strings.AddRuleCorrection, () => _addRule(RuleGroup.Correction)));
        menu.Items.Add(MenuItem(Strings.AddRuleLaunch, () => _addRule(RuleGroup.Launch)));
        menu.Items.Add(MenuItem(Strings.AddRulePower, () => _addRule(RuleGroup.Power)));
        menu.IsOpen = true;
    }

    /// <summary>Подставляет аргументы в шаблон строки ресурсов.</summary>
    private static string Fill(string template, params string?[] args) => RuleSummary.Fill(template, args);
}
