using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KeyMend.Infrastructure;
using KeyMend.Resources;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Text;

namespace KeyMend.UI.Settings;

/// <summary>
/// Раздел «Исключения» (SPEC 5, F7): программы, в которых отключены отдельные функции KeyMend. Список правится сразу;
/// добавить можно по имени или из запущенных программ.
/// </summary>
internal sealed class ExclusionsPage : UserControl
{
    /// <summary>Функции в порядке колонок.</summary>
    private static readonly ExclusionFeature[] _features = [ExclusionFeature.LayoutSwitch, ExclusionFeature.Correction, ExclusionFeature.Snippets, ExclusionFeature.Rules];

    /// <summary>Шрифт имён программ.</summary>
    private static readonly FontFamily _mono = new("Cascadia Mono, Consolas");

    /// <summary>Контур корзины на сетке 14 × 14: крышка, ручка, корпус и две полоски.</summary>
    private const string _trashPath = "M2.5,4 H11.5 M5.5,4 V2.5 H8.5 V4 M4,4 L4.6,11.5 H9.4 L10,4";

    /// <summary>Ширина всплывающего окна вместе с отступами под тень, px.</summary>
    private const double _popupWidth = 356;

    /// <summary>Текущие настройки и их запись.</summary>
    private readonly SettingsService _settings;

    /// <summary>Запущенные программы и описания файлов.</summary>
    private readonly IRunningApps _apps;

    /// <summary>Имя exe самого KeyMend: его в список не предлагают.</summary>
    private readonly string? _ownExe = System.IO.Path.GetFileName(Environment.ProcessPath);

    /// <summary>Счётчик «N программ».</summary>
    private readonly TextBlock _count = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Полоса над таблицей: повтор, недопустимое имя или ошибка записи.</summary>
    private readonly Border _notice = new() { Visibility = Visibility.Collapsed };

    /// <summary>Текст полосы.</summary>
    private readonly TextBlock _noticeText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    /// <summary>Строка ввода имени вручную.</summary>
    private readonly DockPanel _manualRow = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };

    /// <summary>Поле имени программы.</summary>
    private readonly TextBox _manualName = new() { MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Подсказка в пустом поле имени.</summary>
    private readonly TextBlock _manualHint = new() { Text = Strings.ExclManualHint, IsHitTestVisible = false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };

    /// <summary>Таблица.</summary>
    private readonly StackPanel _table = new();

    /// <summary>Кнопка «Добавить из запущенных»: к ней привязано всплывающее окно.</summary>
    private readonly Button _fromRunning = new() { Content = Strings.ExclFromRunning + "  ▾" };

    /// <summary>Всплывающее окно выбора запущенных программ.</summary>
    private readonly Popup _popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };

    /// <summary>Поиск в списке запущенных.</summary>
    private readonly TextBox _popupSearch = new() { MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Список запущенных программ во всплывающем окне.</summary>
    private readonly StackPanel _popupList = new();

    /// <summary>Имена, отмеченные в списке запущенных.</summary>
    private readonly HashSet<string> _picked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Запущенные программы на момент открытия списка.</summary>
    private IReadOnlyList<RunningApp> _running = [];

    /// <summary>Описания файлов по имени exe; недостающие подгружаются в фоне.</summary>
    private IReadOnlyDictionary<string, string?> _descriptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Создаёт страницу.</summary>
    /// <param name="settings">Текущие настройки и их запись.</param>
    /// <param name="apps">Запущенные программы и описания файлов.</param>
    public ExclusionsPage(SettingsService settings, IRunningApps apps)
    {
        _settings = settings;
        _apps = apps;
        Content = BuildLayout();
        Rebuild();
        Loaded += (_, _) =>
        {
            _settings.Changed += OnSettingsChanged;
            Rebuild();
        };
        Unloaded += (_, _) => _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>Список изменили из окна или файла.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings) => Rebuild();

    /// <summary>Собирает неподвижную часть страницы.</summary>
    private StackPanel BuildLayout()
    {
        var lead = new TextBlock { Text = Strings.ExclLead };
        SettingsCards.AddHelp(lead, Strings.SectionExclusions, Strings.HelpExclLead);
        lead.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _count.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _notice.SetResourceReference(StyleProperty, "Km.InfoBar");
        _notice.Margin = new Thickness(0, 12, 0, 0);
        _notice.Child = _noticeText;

        var manual = new Button { Content = Strings.ExclManual, Margin = new Thickness(0, 0, 8, 0) };
        manual.Click += (_, _) => ShowManualRow();
        _fromRunning.SetResourceReference(StyleProperty, "Km.AccentButton");
        _fromRunning.Click += OnFromRunningClick;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(manual);
        buttons.Children.Add(_fromRunning);
        var bar = new DockPanel { Margin = new Thickness(0, 14, 0, 12) };
        DockPanel.SetDock(buttons, Dock.Right);
        bar.Children.Add(buttons);
        bar.Children.Add(_count);

        var panel = new StackPanel();
        panel.Children.Add(lead);
        panel.Children.Add(_notice);
        panel.Children.Add(bar);
        panel.Children.Add(BuildManualRow());
        panel.Children.Add(_table);
        panel.Children.Add(BuildPopup());
        return panel;
    }

    /// <summary>Строка ввода имени: поле, «Добавить» и «Отмена».</summary>
    private DockPanel BuildManualRow()
    {
        _manualHint.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _manualName.TextChanged += (_, _) => _manualHint.Visibility = _manualName.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _manualName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                AddManual();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _manualRow.Visibility = Visibility.Collapsed;
            }
        };
        AutomationProperties.SetName(_manualName, Strings.ExclManualHint);

        var cancel = new Button { Content = Strings.DialogCancel, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => _manualRow.Visibility = Visibility.Collapsed;
        var add = new Button { Content = Strings.ExclAdd, Margin = new Thickness(8, 0, 0, 0) };
        add.SetResourceReference(StyleProperty, "Km.AccentButton");
        add.Click += (_, _) => AddManual();

        var field = new Grid();
        field.Children.Add(_manualName);
        field.Children.Add(_manualHint);
        DockPanel.SetDock(cancel, Dock.Right);
        DockPanel.SetDock(add, Dock.Right);
        _manualRow.Children.Add(cancel);
        _manualRow.Children.Add(add);
        _manualRow.Children.Add(field);
        return _manualRow;
    }

    /// <summary>Показывает строку ввода и ставит курсор в поле.</summary>
    private void ShowManualRow()
    {
        _notice.Visibility = Visibility.Collapsed;
        _manualName.Clear();
        _manualRow.Visibility = Visibility.Visible;
        _ = _manualName.Focus();
    }

    /// <summary>Добавляет программу, имя которой введено вручную.</summary>
    private void AddManual()
    {
        if (ProcessNames.Normalize(_manualName.Text) is not { } name)
        {
            ShowNotice(Strings.ExclInvalidName);
            return;
        }

        var result = ExclusionList.Add(_settings.Current.Exclusions, [name]);
        if (result.Duplicates.Count > 0)
        {
            ShowNotice(RuleSummary.Fill(Strings.ExclDuplicate, name));
            return;
        }

        if (Save(result.List))
        {
            _manualRow.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Собирает всплывающее окно выбора из запущенных.</summary>
    private Popup BuildPopup()
    {
        _popupSearch.TextChanged += (_, _) => FillPopupList();
        AutomationProperties.SetName(_popupSearch, Strings.ExclSearchRunning);
        var scroll = new ScrollViewer { MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _popupList, Margin = new Thickness(0, 6, 0, 6) };

        var cancel = new Button { Content = Strings.DialogCancel, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => _popup.IsOpen = false;
        var add = new Button { Content = Strings.ExclAdd };
        add.SetResourceReference(StyleProperty, "Km.AccentButton");
        add.Click += (_, _) => AddPicked();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(cancel);
        actions.Children.Add(add);

        var searchHint = new TextBlock { Text = Strings.ExclSearchRunning, IsHitTestVisible = false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
        searchHint.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
        _popupSearch.TextChanged += (_, _) => searchHint.Visibility = _popupSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var searchBox = new Grid();
        searchBox.Children.Add(_popupSearch);
        searchBox.Children.Add(searchHint);

        var body = new StackPanel { Width = _popupWidth - 16 };
        body.Children.Add(searchBox);
        body.Children.Add(scroll);
        body.Children.Add(actions);
        var card = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Margin = new Thickness(8), Child = body };
        card.SetResourceReference(Border.BackgroundProperty, "Km.Card2");
        card.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.3 };

        _popup.PlacementTarget = _fromRunning;
        _popup.Child = card;
        return _popup;
    }

    /// <summary>Читает запущенные программы в фоне и открывает окно выбора.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Данные нажатия.</param>
    private void OnFromRunningClick(object sender, RoutedEventArgs e) => _ = OpenRunningAsync();

    /// <summary>Читает запущенные программы в фоне и открывает окно выбора.</summary>
    private async Task OpenRunningAsync()
    {
        _notice.Visibility = Visibility.Collapsed;
        _running = await Task.Run(_apps.List);
        _picked.Clear();
        _popupSearch.Clear();
        FillPopupList();
        _popup.HorizontalOffset = _fromRunning.ActualWidth - _popupWidth;
        _popup.IsOpen = true;
        _ = _popupSearch.Focus();
    }

    /// <summary>Заполняет список запущенных по строке поиска; отмеченное сохраняется.</summary>
    private void FillPopupList()
    {
        _popupList.Children.Clear();
        var title = KeyChipsBuilder.Label(Strings.ExclRunningNow, "Km.Text2", 12, margin: false);
        title.Margin = new Thickness(4, 0, 0, 4);
        _popupList.Children.Add(title);
        var found = ExclusionList.Candidates(_running, _settings.Current.Exclusions, _ownExe, _popupSearch.Text);
        foreach (var app in found)
        {
            var box = new CheckBox { IsChecked = _picked.Contains(app.Exe), Margin = new Thickness(4, 3, 4, 3), Content = AppLabel(app) };
            AutomationProperties.SetName(box, app.Exe);
            box.Checked += (_, _) => _picked.Add(app.Exe);
            box.Unchecked += (_, _) => _picked.Remove(app.Exe);
            _popupList.Children.Add(box);
        }

        if (found.Count == 0)
        {
            var none = KeyChipsBuilder.Label(Strings.ExclNoRunning, "Km.Text2", 13, margin: false);
            none.Margin = new Thickness(4, 6, 0, 6);
            _popupList.Children.Add(none);
        }
    }

    /// <summary>Подпись пункта списка: имя exe и серым описание.</summary>
    private static StackPanel AppLabel(RunningApp app)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock { Text = app.Exe, FontFamily = _mono, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        if (app.Description is not null)
        {
            var description = KeyChipsBuilder.Label(app.Description, "Km.Text2", 12, margin: false);
            description.Margin = new Thickness(8, 0, 0, 0);
            panel.Children.Add(description);
        }

        return panel;
    }

    /// <summary>Добавляет отмеченные в списке программы и закрывает окно.</summary>
    private void AddPicked()
    {
        _popup.IsOpen = false;
        if (_picked.Count == 0)
        {
            return;
        }

        _ = Save(ExclusionList.Add(_settings.Current.Exclusions, _picked).List);
    }

    /// <summary>Записывает список; при отказе показывает ошибку.</summary>
    /// <param name="list">Новый список исключений.</param>
    /// <returns><c>true</c>, если записано.</returns>
    private bool Save(IReadOnlyList<ExclusionEntry> list)
    {
        _notice.Visibility = Visibility.Collapsed;
        if (_settings.Update(s => s with { Exclusions = list }))
        {
            return true;
        }

        ShowNotice(Strings.RuleSaveFailed);
        return false;
    }

    /// <summary>Перестраивает счётчик и таблицу; описания недостающих файлов подгружаются в фоне.</summary>
    private void Rebuild()
    {
        var list = _settings.Current.Exclusions;
        _count.Text = list.Count.ToString(CultureInfo.CurrentCulture) + " "
            + UiPlural.Pick(CultureInfo.CurrentUICulture, list.Count, Strings.ExclCountOne, Strings.ExclCountFew, Strings.ExclCountMany);
        BuildTable(list);
        if (list.Any(e => !_descriptions.ContainsKey(e.Process)))
        {
            _ = LoadDescriptionsAsync(list);
        }
    }

    /// <summary>Читает описания файлов в фоне и перерисовывает таблицу, когда они готовы.</summary>
    /// <param name="list">Список, для которого нужны описания.</param>
    private async Task LoadDescriptionsAsync(IReadOnlyList<ExclusionEntry> list)
    {
        var names = list.Select(e => e.Process).ToList();
        var found = await Task.Run(() => _apps.Describe(names));
        _descriptions = found;
        BuildTable(_settings.Current.Exclusions);
    }

    /// <summary>Колонки таблицы: программа, четыре функции, корзина.</summary>
    private static Grid Columns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var _ in _features)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        return grid;
    }

    /// <summary>Рисует таблицу: заголовок в два яруса и строки программ.</summary>
    /// <param name="list">Исключения.</param>
    private void BuildTable(IReadOnlyList<ExclusionEntry> list)
    {
        _table.Children.Clear();
        var card = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Km.Card");
        card.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        var body = new StackPanel();
        card.Child = body;
        body.Children.Add(HeaderRow());
        if (list.Count == 0)
        {
            var empty = new TextBlock { Text = Strings.ExclEmpty, Margin = new Thickness(16, 20, 16, 20) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text2");
            body.Children.Add(empty);
        }

        foreach (var entry in list)
        {
            body.Children.Add(EntryRow(entry));
        }

        _table.Children.Add(card);
    }

    /// <summary>Заголовок: «Программа», над функциями общий заголовок, под ним названия функций.</summary>
    private static Grid HeaderRow()
    {
        var grid = Columns();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var program = KeyChipsBuilder.Label(Strings.ExclColProgram, "Km.Text2", 12, margin: false);
        program.Margin = new Thickness(16, 8, 0, 8);
        program.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetRowSpan(program, 2);
        grid.Children.Add(program);

        var group = KeyChipsBuilder.Label(Strings.ExclColWorks, "Km.Text2", 12, margin: false);
        group.HorizontalAlignment = HorizontalAlignment.Center;
        group.Margin = new Thickness(0, 8, 0, 2);
        Grid.SetColumn(group, 1);
        Grid.SetColumnSpan(group, _features.Length);
        grid.Children.Add(group);

        for (var i = 0; i < _features.Length; i++)
        {
            var title = KeyChipsBuilder.Label(FeatureTitle(_features[i]), "Km.Text2", 12, margin: false);
            title.TextAlignment = TextAlignment.Center;
            title.TextWrapping = TextWrapping.Wrap;
            title.Margin = new Thickness(6, 0, 6, 8);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRow(title, 1);
            Grid.SetColumn(title, i + 1);
            grid.Children.Add(title);
        }

        return grid;
    }

    /// <summary>Строка программы: имя с описанием, четыре галочки, корзина.</summary>
    /// <param name="entry">Исключение.</param>
    private Border EntryRow(ExclusionEntry entry)
    {
        var grid = Columns();
        grid.MinHeight = 56;

        var name = new TextBlock { Text = entry.Process, FontFamily = _mono, FontSize = 13, FontWeight = FontWeights.SemiBold };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text");
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 6, 8, 6) };
        texts.Children.Add(name);
        if (_descriptions.GetValueOrDefault(entry.Process) is { } description)
        {
            var detail = KeyChipsBuilder.Label(description, "Km.Text2", 12, margin: false);
            detail.TextWrapping = TextWrapping.Wrap;
            texts.Children.Add(detail);
        }

        grid.Children.Add(texts);
        for (var i = 0; i < _features.Length; i++)
        {
            var feature = _features[i];
            var box = new CheckBox
            {
                IsChecked = ExclusionList.Works(entry, feature),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(box, FeatureTitle(feature) + ": " + entry.Process);
            void OnChanged(object sender, RoutedEventArgs e) => _ = Save(ExclusionList.Set(_settings.Current.Exclusions, entry.Process, feature, box.IsChecked == true));
            box.Checked += OnChanged;
            box.Unchecked += OnChanged;
            Grid.SetColumn(box, i + 1);
            grid.Children.Add(box);
        }

        var delete = new Button { Style = (Style)FindResource("Km.IconButton"), Content = TrashIcon(), Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(delete, RuleSummary.Fill(Strings.ExclDelete, entry.Process));
        delete.Click += (_, _) => _ = Save(ExclusionList.Remove(_settings.Current.Exclusions, entry.Process));
        Grid.SetColumn(delete, _features.Length + 1);
        grid.Children.Add(delete);

        var row = new Border { Child = grid, BorderThickness = new Thickness(0, 1, 0, 0) };
        row.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        return row;
    }

    /// <summary>Значок корзины: тот же контур и размер 14 × 14, что у «Удалить шаг» в редакторе правила; рисуется линиями и не зависит от шрифтов в системе.</summary>
    private static System.Windows.Shapes.Path TrashIcon()
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(_trashPath),
            Width = 14,
            Height = 14,
            StrokeThickness = 1.3,
            IsHitTestVisible = false,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Km.Text");
        return path;
    }
    /// <summary>Название функции для заголовка колонки.</summary>
    private static string FeatureTitle(ExclusionFeature feature) => feature switch
    {
        ExclusionFeature.LayoutSwitch => Strings.ExclFeatureLayout,
        ExclusionFeature.Correction => Strings.ExclFeatureCorrection,
        ExclusionFeature.Snippets => Strings.ExclFeatureSnippets,
        _ => Strings.ExclFeatureRules,
    };

    /// <summary>Показывает сообщение над таблицей.</summary>
    /// <param name="text">Текст.</param>
    private void ShowNotice(string text)
    {
        _noticeText.Text = text;
        _notice.SetResourceReference(Border.BorderBrushProperty, "Km.Error");
        _notice.Visibility = Visibility.Visible;
    }
}
