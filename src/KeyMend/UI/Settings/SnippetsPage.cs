using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «Автозамена» (SPEC 5): «Память набора», таблица сокращений с поиском и фильтром «Только секреты», «Вкл.» в строке.
/// Собирается из кода, как «Правила». Правки применяются сразу; сокращение правится на странице-редакторе.
/// </summary>
internal sealed class SnippetsPage : UserControl
{
    /// <summary>Шрифт сокращений: пробел на конце и похожие знаки должны различаться.</summary>
    private static readonly FontFamily _mono = new("Cascadia Mono, Consolas");

    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Таблицы установленных раскладок.</summary>
    private readonly IInstalledTables _tables;

    /// <summary>Открывает редактор сокращения.</summary>
    private readonly Action<Snippet> _openSnippet;

    /// <summary>Полоса над таблицей: отказ включить сокращение или ошибка записи.</summary>
    private readonly Border _notice = new() { Visibility = Visibility.Collapsed };

    /// <summary>Текст полосы.</summary>
    private readonly TextBlock _noticeText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    /// <summary>Память набора.</summary>
    private readonly NumberBox _buffer = new() { Width = 80, MinHeight = 32 };

    /// <summary>Поиск по сокращению и тексту.</summary>
    private readonly TextBox _search = new() { Width = 300, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Подсказка в пустом поиске.</summary>
    private readonly TextBlock _searchHint = new() { Text = Strings.SnipSearch, IsHitTestVisible = false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };

    /// <summary>Фильтр «Только секреты».</summary>
    private readonly CheckBox _onlySecrets = new() { Content = Strings.SnipOnlySecrets, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };

    /// <summary>Счётчик «Сокращений: N · секретов: M» под строкой поиска: в одну строку с ней он не помещался при узком окне.</summary>
    private readonly TextBlock _count = new() { FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };

    /// <summary>Таблица: заголовок и строки.</summary>
    private readonly StackPanel _table = new();

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="tables">Таблицы установленных раскладок.</param>
    /// <param name="openSnippet">Открывает редактор сокращения.</param>
    /// <param name="addSnippet">Открывает редактор нового сокращения.</param>
    public SnippetsPage(SettingsService settings, IInstalledTables tables, Action<Snippet> openSnippet, Action addSnippet)
    {
        _settings = settings;
        _tables = tables;
        _openSnippet = openSnippet;
        Content = BuildLayout(addSnippet);
        Rebuild();
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            Rebuild();
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Сокращения изменились: правкой из окна или файла.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => Rebuild();

    /// <summary>Собирает неподвижную часть страницы.</summary>
    private StackPanel BuildLayout(Action addSnippet)
    {
        var lead = new TextBlock { Text = Strings.SnipLead };
        SettingsCards.AddHelp(lead, Strings.SectionSnippets, Strings.HelpSnipLead);
        lead.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");

        _notice.Style = (Style)FindResource("Km.InfoBar");
        _notice.Margin = new Thickness(0, 12, 0, 0);
        _notice.Child = _noticeText;

        _searchHint.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _search.TextChanged += (_, _) =>
        {
            _searchHint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Rebuild();
        };
        AutomationProperties.SetName(_search, Strings.SnipSearch);
        _onlySecrets.Checked += (_, _) => Rebuild();
        _onlySecrets.Unchecked += (_, _) => Rebuild();
        _count.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");

        var searchBox = new Grid();
        searchBox.Children.Add(_search);
        searchBox.Children.Add(_searchHint);
        var add = new Button { Style = (Style)FindResource("Km.AccentButton"), Content = "+  " + Strings.SnipAdd };
        add.Click += (_, _) => addSnippet();
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 10), LastChildFill = false };
        DockPanel.SetDock(add, Dock.Right);
        row.Children.Add(add);
        foreach (UIElement item in new UIElement[] { searchBox, _onlySecrets })
        {
            DockPanel.SetDock(item, Dock.Left);
            row.Children.Add(item);
        }

        var panel = new StackPanel();
        panel.Children.Add(lead);
        panel.Children.Add(_notice);
        panel.Children.Add(BufferCard());
        panel.Children.Add(row);
        panel.Children.Add(_count);
        panel.Children.Add(_table);
        return panel;
    }

    /// <summary>Карточка «Память набора»: число применяется сразу.</summary>
    private Border BufferCard()
    {
        var title = SettingsCards.Title(Strings.SnipBufferTitle, Strings.HelpSnipBuffer);
        var hint = new TextBlock { Style = (Style)FindResource("Km.CardHint"), Text = Strings.SnipBufferHint };
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(title);
        texts.Children.Add(hint);

        _buffer.SetValue(_settings.Current.WordBufferLimit);
        AutomationProperties.SetName(_buffer, Strings.SnipBufferTitle);
        _buffer.Committed += OnBufferCommitted;
        var unit = KeyChipsBuilder.Label(Strings.SnipBufferUnit, "Km.Text2", 13, margin: false);
        unit.Margin = new Thickness(8, 0, 0, 0);
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(_buffer);
        right.Children.Add(unit);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(right, 1);
        grid.Children.Add(texts);
        grid.Children.Add(right);
        return new Border { Style = (Style)FindResource("Km.CardBorder"), Margin = new Thickness(0, 16, 0, 16), Child = grid };
    }

    /// <summary>Число в поле принято: границы 20–1000 (SPEC F5.1), запись сразу; нечисло возвращает прежнее значение.</summary>
    private void OnBufferCommitted()
    {
        var value = Math.Clamp(_buffer.Value ?? _settings.Current.WordBufferLimit, AppSettings.MinWordBufferLimit, AppSettings.MaxWordBufferLimit);
        _buffer.SetValue(value);
        if (value == _settings.Current.WordBufferLimit)
        {
            return;
        }

        _notice.Visibility = Visibility.Collapsed;
        if (!_settings.Update(s => s with { WordBufferLimit = value }))
        {
            _buffer.SetValue(_settings.Current.WordBufferLimit);
            ShowNotice(Strings.RuleSaveFailed);
        }
    }

    /// <summary>Перестраивает таблицу и счётчик по настройкам и условиям.</summary>
    private void Rebuild()
    {
        var snippets = _settings.Current.Snippets;
        var view = SnippetTable.Build(snippets, new SnippetTableFilter(_search.Text, _onlySecrets.IsChecked == true), _tables.Tables());
        _count.Text = RuleSummary.Fill(
            Strings.SnipCount, view.Total.ToString(CultureInfo.CurrentCulture), view.Secrets.ToString(CultureInfo.CurrentCulture));
        if (!_buffer.IsKeyboardFocusWithin)
        {
            _buffer.SetValue(_settings.Current.WordBufferLimit);
        }

        BuildTable(view);
    }

    /// <summary>Рисует таблицу: заголовок колонок, строки или сообщение, что ничего не найдено; под ней пояснение про «␣».</summary>
    private void BuildTable(SnippetTableView view)
    {
        _table.Children.Clear();
        var card = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Km.Card");
        card.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        var body = new StackPanel();
        card.Child = body;
        body.Children.Add(HeaderRow());
        if (view.Rows.Count == 0)
        {
            var empty = new TextBlock { Text = Strings.SnipEmpty, Margin = new Thickness(16, 20, 16, 20) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
            body.Children.Add(empty);
        }

        foreach (var row in view.Rows)
        {
            body.Children.Add(SnippetRow(row));
        }

        _table.Children.Add(card);
        var note = KeyChipsBuilder.Label(Strings.SnipSpaceNote, "Km.Text2", 12, margin: false);
        note.Margin = new Thickness(0, 10, 0, 0);
        _table.Children.Add(note);
    }

    /// <summary>Колонки, общие для заголовка и строк: Вкл., Сокращение, Текст, После вставки, Как вставлять.</summary>
    private static Grid Columns()
    {
        var grid = new Grid { MinHeight = 40 };
        foreach (var width in new[] { new GridLength(64), new GridLength(1.3, GridUnitType.Star), new GridLength(2, GridUnitType.Star), new GridLength(1.4, GridUnitType.Star), new GridLength(1.1, GridUnitType.Star) })
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
        var titles = new[] { Strings.ColEnabled, Strings.SnipColKeys, Strings.SnipColText, Strings.SnipColAfter, Strings.SnipColHow };
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = KeyChipsBuilder.Label(titles[i], "Km.Text2", 12);
            cell.Margin = new Thickness(i == 0 ? 16 : 8, 0, 0, 0);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    /// <summary>Строка сокращения.</summary>
    private Border SnippetRow(SnippetTableRow data)
    {
        var snippet = data.Snippet;
        var grid = Columns();
        var toggle = new CheckBox { Style = (Style)FindResource("Km.Switch"), IsChecked = snippet.Enabled, Margin = new Thickness(16, 0, 0, 0) };
        AutomationProperties.SetName(toggle, RuleSummary.Fill(Strings.SnipToggleLabel, SnippetTable.DisplayKeys(snippet.Keys)));
        toggle.Click += (_, _) => OnToggle(snippet, toggle);
        grid.Children.Add(toggle);

        Place(grid, KeysCell(data), 1);
        Place(grid, TextCell(snippet), 2);
        Place(grid, AfterCell(snippet), 3);
        var how = snippet.Secret || snippet.Insert == InsertMode.Clipboard ? Strings.SnipHowClipboard : Strings.SnipHowType;
        var howCell = Cell(how);
        howCell.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        Place(grid, howCell, 4);

        if (!snippet.Enabled)
        {
            foreach (UIElement cell in grid.Children)
            {
                if (cell != toggle)
                {
                    cell.Opacity = 0.55;
                }
            }
        }

        var row = new Border { Child = grid, BorderThickness = new Thickness(0, 1, 0, 0) };
        row.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        return row;
    }

    /// <summary>Ячейка сокращения: ссылка на редактор и серым — те же клавиши в другой раскладке.</summary>
    private TextBlock KeysCell(SnippetTableRow data)
    {
        var link = new Hyperlink(new Run(SnippetTable.DisplayKeys(data.Snippet.Keys))) { TextDecorations = null, FontWeight = FontWeights.SemiBold };
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.Text");
        link.Click += (_, _) => _openSnippet(data.Snippet);
        var block = new TextBlock { FontFamily = _mono, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 4) };
        block.Inlines.Add(link);
        if (data.InOtherLayout is { } other)
        {
            var run = new Run("  " + SnippetTable.DisplayKeys(other));
            run.SetResourceReference(TextElement.ForegroundProperty, "Km.Text2");
            block.Inlines.Add(run);
        }

        return block;
    }

    /// <summary>Ячейка текста: первая строка с многоточием, у секрета — замок и точки вместо текста.</summary>
    private static FrameworkElement TextCell(Snippet snippet)
    {
        if (!snippet.Secret)
        {
            var text = snippet.Text ?? string.Empty;
            var block = Cell(text.Split('\n')[0].TrimEnd('\r'));
            block.TextWrapping = TextWrapping.NoWrap;
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            block.ToolTip = text;
            return block;
        }

        var lockIcon = new Path
        {
            Data = Geometry.Parse("M3,6 H11 V12 H3 Z M4.8,6 V4.5 A2.2,2.2 0 0 1 9.2,4.5 V6"),
            Width = 14,
            Height = 14,
            StrokeThickness = 1.2,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        lockIcon.SetResourceReference(Shape.StrokeProperty, "Km.Text2");
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(lockIcon);
        panel.Children.Add(KeyChipsBuilder.Label(Strings.SnipSecretCell, "Km.Text2", 13, margin: false));
        return panel;
    }

    /// <summary>Ячейка «После вставки»: плашки клавиш у нажатий, текст сводки у остальных шагов, прочерк, если шагов нет.</summary>
    private WrapPanel AfterCell(Snippet snippet)
    {
        var panel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 4, 8, 4) };
        if (snippet.After.Count == 0)
        {
            panel.Children.Add(KeyChipsBuilder.Label("—", "Km.Text2", 13, margin: false));
            return panel;
        }

        foreach (var step in snippet.After)
        {
            if (step is PressStep press)
            {
                panel.Children.Add(new Border
                {
                    Style = (Style)FindResource("Km.KeyChip"),
                    Margin = new Thickness(0, 0, 4, 0),
                    Child = KeyChipsBuilder.Label(KeyChordParser.Format(press.Keys), "Km.Text", 12, margin: false),
                });
            }
            else
            {
                panel.Children.Add(KeyChipsBuilder.Label(RuleSummary.Step(step), "Km.Text", 13));
            }
        }

        return panel;
    }

    /// <summary>Кладёт элемент в колонку.</summary>
    private static void Place(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    /// <summary>Текстовая ячейка.</summary>
    private static TextBlock Cell(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 4, 8, 4),
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text");
        return block;
    }

    /// <summary>Переключатель «Вкл.»: выключение сразу сохраняется, включение — только если клавиши свободны.</summary>
    private void OnToggle(Snippet snippet, CheckBox toggle)
    {
        var enable = toggle.IsChecked == true;
        if (enable)
        {
            var others = _settings.Current.Snippets.Where(s => !IsSame(s, snippet)).ToList();
            if (SnippetValidation.FindTaken(snippet.Keys, others, _tables.Tables()) is { } taken)
            {
                toggle.IsChecked = false;
                ShowRefusal(snippet, taken);
                return;
            }
        }

        _notice.Visibility = Visibility.Collapsed;
        if (!_settings.Update(s => s with { Snippets = SnippetList.Upsert(s.Snippets, snippet, snippet with { Enabled = enable }, Guid.NewGuid) }))
        {
            toggle.IsChecked = !enable;
            ShowNotice(Strings.RuleSaveFailed);
        }
    }

    /// <summary>Это то же сокращение: тот же непустой идентификатор или та же ссылка.</summary>
    private static bool IsSame(Snippet a, Snippet b) =>
        ReferenceEquals(a, b) || (a.Id.Length > 0 && string.Equals(a.Id, b.Id, StringComparison.Ordinal));

    /// <summary>Объясняет, почему сокращение не включилось, со ссылкой на занявшее клавиши.</summary>
    private void ShowRefusal(Snippet snippet, Snippet taken)
    {
        _noticeText.Inlines.Clear();
        var reason = RuleSummary.Fill(Strings.SnipErrTaken, SnippetTable.DisplayKeys(taken.Keys));
        _noticeText.Inlines.Add(new Run(RuleSummary.Fill(Strings.SnipNotEnabled, SnippetTable.DisplayKeys(snippet.Keys), reason)));
        var link = new Hyperlink(new Run(Strings.SnipOpenSnippet));
        link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
        link.Click += (_, _) => _openSnippet(taken);
        _noticeText.Inlines.Add(new Run(" "));
        _noticeText.Inlines.Add(link);
        _notice.SetResourceReference(Border.BorderBrushProperty, "Km.Error");
        _notice.Visibility = Visibility.Visible;
    }

    /// <summary>Показывает ошибку записи над таблицей.</summary>
    private void ShowNotice(string text)
    {
        _noticeText.Inlines.Clear();
        _noticeText.Inlines.Add(new Run(text));
        _notice.SetResourceReference(Border.BorderBrushProperty, "Km.Error");
        _notice.Visibility = Visibility.Visible;
    }
}
