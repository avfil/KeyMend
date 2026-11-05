using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>
/// Список шагов правила (SPEC 5): карточки с номером, названием и сводкой, стрелки «Выше» и «Ниже», «Удалить шаг» и «Добавить шаг».
/// Редакторы шагов живут, пока шаг в списке, поэтому при перестановке введённое не теряется.
/// </summary>
internal sealed class StepListEditor : StackPanel
{
    /// <summary>Окружение редакторов шагов.</summary>
    private readonly StepContext _context;

    /// <summary>Шаги по порядку.</summary>
    private readonly List<Entry> _entries = [];

    /// <summary>Карточки.</summary>
    private readonly StackPanel _cards = new();

    /// <summary>Ошибка «Добавьте хотя бы один шаг».</summary>
    private readonly TextBlock _noSteps = ErrorText(Strings.ErrNoSteps);

    /// <summary>Создаёт список.</summary>
    /// <param name="steps">Начальные шаги.</param>
    /// <param name="context">Окружение редакторов шагов.</param>
    public StepListEditor(IEnumerable<Step> steps, StepContext context)
    {
        _context = context;
        foreach (var step in steps)
        {
            _entries.Add(CreateEntry(step));
        }

        Children.Add(_cards);
        Children.Add(_noSteps);
        Children.Add(AddButton());
        Rebuild();
    }

    /// <summary>Список или поле шага изменилось.</summary>
    public event Action? Changed;

    /// <summary>Шаги с внесёнными правками.</summary>
    public IReadOnlyList<Step> Steps => [.. _entries.Select(e => e.Editor.Current)];

    /// <summary>Показывает ошибки шагов и пустого списка.</summary>
    /// <param name="problems">Проблема каждого шага по порядку.</param>
    /// <param name="noSteps">В правиле нет шагов.</param>
    /// <param name="visible">Ошибки уже можно показывать (владелец пробовал сохранить).</param>
    public void ShowProblems(IReadOnlyList<StepProblem> problems, bool noSteps, bool visible)
    {
        _noSteps.Visibility = noSteps && visible ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < _entries.Count; i++)
        {
            var problem = i < problems.Count ? problems[i] : StepProblem.None;
            _entries[i].Error.Text = Message(problem);
            _entries[i].Error.Visibility = problem != StepProblem.None && visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Текст ошибки шага.</summary>
    /// <param name="problem">Проблема.</param>
    /// <returns>Текст; для <see cref="StepProblem.None"/> пустой.</returns>
    internal static string Message(StepProblem problem) => problem switch
    {
        StepProblem.SameLanguages => Strings.ErrSameLanguages,
        StepProblem.NoFile => Strings.ErrNoFile,
        StepProblem.BadFile => Strings.ErrBadFile,
        StepProblem.NoKeys => Strings.ErrNoKeys,
        StepProblem.NoText => Strings.ErrNoText,
        _ => string.Empty,
    };

    /// <summary>Создаёт редактор шага нужного вида.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="context">Окружение.</param>
    /// <returns>Редактор.</returns>
    internal static StepEditor CreateEditor(Step step, StepContext context) => step switch
    {
        SwitchLayoutStep s => new SwitchLayoutStepEditor(s, context.InstalledLanguages, context.OpenHint),
        CorrectStep s => new CorrectStepEditor(s, context.InstalledLanguages),
        LaunchStep s => new LaunchStepEditor(s, context),
        PowerStep s => new PowerStepEditor(s, context.HibernateEnabled),
        PressStep s => new PressStepEditor(s),
        DelayStep s => new DelayStepEditor(s),
        InsertTextStep s => new InsertTextStepEditor(s),
        ServiceStep s => new ServiceStepEditor(s),
        _ => throw new ArgumentOutOfRangeException(nameof(step), "Unknown step kind"),
    };

    /// <summary>Текст ошибки под полем.</summary>
    private static TextBlock ErrorText(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 12, Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Km.Error");
        return block;
    }

    /// <summary>Заводит запись шага: редактор и подпись сводки, которая следит за правками.</summary>
    private Entry CreateEntry(Step step)
    {
        var editor = CreateEditor(step, _context);
        var entry = new Entry(editor, KeyChipsBuilder.Label(RuleSummary.Step(step), "Km.Text2", 13, margin: false), ErrorText(string.Empty));
        editor.Changed += () =>
        {
            entry.Summary.Text = RuleSummary.Step(editor.Current);
            Changed?.Invoke();
        };
        return entry;
    }

    /// <summary>Кнопка «Добавить шаг» с меню видов.</summary>
    private Button AddButton()
    {
        var add = new Button { Content = Strings.StepAdd + "  ▾", HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 32, Padding = new Thickness(14, 0, 14, 0) };
        add.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = add, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var kind in StepCatalog.All)
            {
                var captured = kind;
                var item = new MenuItem { Header = StepCatalog.Title(kind) };
                item.Click += (_, _) => Add(StepCatalog.Create(captured));
                _ = menu.Items.Add(item);
            }

            menu.IsOpen = true;
        };
        return add;
    }

    /// <summary>Добавляет шаг в конец, прокручивает к нему и ставит фокус в первое поле.</summary>
    private void Add(Step step)
    {
        var entry = CreateEntry(step);
        _entries.Add(entry);

        // Карточка ещё не показана: прокрутка и фокус — когда редактор встанет в дерево.
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            entry.Editor.Loaded -= OnLoaded;
            entry.Card?.BringIntoView();
            _ = entry.Editor.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
        }

        entry.Editor.Loaded += OnLoaded;
        Rebuild();
        Changed?.Invoke();
    }

    /// <summary>Двигает шаг на место выше или ниже.</summary>
    private void Move(Entry entry, int delta)
    {
        var index = _entries.IndexOf(entry);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _entries.Count)
        {
            return;
        }

        (_entries[index], _entries[target]) = (_entries[target], _entries[index]);
        Rebuild();
        Changed?.Invoke();
    }

    /// <summary>Убирает шаг.</summary>
    private void Remove(Entry entry)
    {
        _ = _entries.Remove(entry);
        Rebuild();
        Changed?.Invoke();
    }

    /// <summary>Перерисовывает карточки по порядку записей.</summary>
    private void Rebuild()
    {
        foreach (var entry in _entries)
        {
            // Редактор и ошибка сидят в старой карточке: отцепляем до сборки новой.
            (entry.Editor.Parent as Panel)?.Children.Remove(entry.Editor);
            (entry.Error.Parent as Panel)?.Children.Remove(entry.Error);
            (entry.Summary.Parent as Panel)?.Children.Remove(entry.Summary);
        }

        _cards.Children.Clear();
        for (var i = 0; i < _entries.Count; i++)
        {
            _cards.Children.Add(BuildCard(i));
        }

        _noSteps.Visibility = Visibility.Collapsed;
    }

    /// <summary>Карточка шага.</summary>
    private Border BuildCard(int index)
    {
        var entry = _entries[index];
        var card = new Border { Style = (Style)FindResource("Km.CardBorder"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(20, 14, 20, 12) };

        var badge = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        badge.SetResourceReference(Border.BackgroundProperty, "Km.Card2");
        var number = KeyChipsBuilder.Label((index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture), "Km.Text", 12, margin: false);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        badge.Child = number;

        var title = KeyChipsBuilder.Label(StepCatalog.Title(entry.Editor.Current), "Km.Text", 14, margin: false);
        title.FontWeight = FontWeights.SemiBold;
        entry.Summary.Margin = new Thickness(0, 2, 0, 0);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(title);
        names.Children.Add(entry.Summary);

        var up = IconButton(Strings.StepUp, "M3,9 L7,5 L11,9", index > 0, () => Move(entry, -1));
        var down = IconButton(Strings.StepDown, "M3,5 L7,9 L11,5", index < _entries.Count - 1, () => Move(entry, 1));
        var remove = IconButton(Strings.StepRemove, "M2.5,4 H11.5 M5.5,4 V2.5 H8.5 V4 M4,4 L4.6,11.5 H9.4 L10,4", true, () => Remove(entry));

        var header = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(up);
        buttons.Children.Add(down);
        buttons.Children.Add(remove);
        DockPanel.SetDock(buttons, Dock.Right);
        header.Children.Add(buttons);
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(badge);
        left.Children.Add(names);
        header.Children.Add(left);

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(entry.Editor);
        entry.Error.Margin = new Thickness(0, 4, 0, 0);
        body.Children.Add(entry.Error);
        card.Child = body;
        entry.Card = card;
        return card;
    }

    /// <summary>Значок-кнопка 14×14 в стиле <c>Km.IconButton</c>.</summary>
    private Button IconButton(string name, string geometry, bool enabled, Action click)
    {
        var path = new Path
        {
            Data = Geometry.Parse(geometry),
            Width = 14,
            Height = 14,
            StrokeThickness = 1.3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false,
        };
        path.SetResourceReference(Shape.StrokeProperty, enabled ? "Km.Text" : "Km.Text2");
        var button = new Button { Style = (Style)FindResource("Km.IconButton"), Content = path, IsEnabled = enabled, Opacity = enabled ? 1 : 0.4 };
        AutomationProperties.SetName(button, name);
        button.ToolTip = name;
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Запись одного шага списка.</summary>
    private sealed class Entry(StepEditor editor, TextBlock summary, TextBlock error)
    {
        /// <summary>Редактор полей шага.</summary>
        public StepEditor Editor { get; } = editor;

        /// <summary>Строка сводки под названием.</summary>
        public TextBlock Summary { get; } = summary;

        /// <summary>Ошибка шага под полями.</summary>
        public TextBlock Error { get; } = error;

        /// <summary>Карточка, в которой шаг показан сейчас.</summary>
        public Border? Card { get; set; }
    }
}
