using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>
/// Значок «?» в круге рядом с настройкой (SPEC F9): по щелчку открывает подсказку с подробностями и примером. Подсказка
/// лежит поверх содержимого и ничего не сдвигает; открывается над значком (или под ним, если сверху нет места) со стрелкой к
/// значку.
/// </summary>
internal sealed class HelpButton : Button
{
    /// <summary>Ширина подсказки.</summary>
    private const double _tipWidth = 420;

    /// <summary>Высота полосы со стрелкой.</summary>
    private const double _arrowRow = 8;

    /// <summary>Сторона значка.</summary>
    private const double _iconSize = 16;

    /// <summary>Зазор между краем значка и стрелкой подсказки.</summary>
    private const double _gap = 1;

    /// <summary>Расстояние от левого края подсказки до левого края значка.</summary>
    private const double _tipLeftOfIcon = 14;

    /// <summary>Подсказка закрылась щелчком по значку не позже чем столько миллисекунд назад: повторно не открываем.</summary>
    private const long _reopenGuardMs = 250;

    /// <summary>Текст подсказки с разметкой из ресурсов.</summary>
    private readonly string _text;

    /// <summary>Окно подсказки.</summary>
    private readonly Popup _popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Relative };

    /// <summary>Окно, на события которого подписана открытая подсказка.</summary>
    private Window? _owner;

    /// <summary>Подсказка открывается над значком.</summary>
    private bool _above = true;

    /// <summary>Сдвиг подсказки по горизонтали относительно значка.</summary>
    private double _shiftX = -_tipLeftOfIcon;

    /// <summary>Время закрытия подсказки (<see cref="Environment.TickCount64"/>).</summary>
    private long _closedAt;

    /// <summary>Создаёт значок.</summary>
    /// <param name="topic">О чём подсказка: для читалок экрана («Подсказка: …»).</param>
    /// <param name="text">Текст подсказки с разметкой <see cref="HelpText"/>.</param>
    public HelpButton(string topic, string text)
    {
        _text = text;
        Content = "?";

        // Значок только для мыши: в Tab не участвует и фокус не берёт, поэтому рамка клавиатурного фокуса на нём не появляется.
        Focusable = false;
        IsTabStop = false;
        SetResourceReference(StyleProperty, "Km.HelpButton");
        AutomationProperties.SetName(this, Strings.HelpButtonName + topic);
        AutomationProperties.SetHelpText(this, HelpText.Plain(text));
        _popup.PlacementTarget = this;
        _popup.Closed += (_, _) => OnClosed();

        // Closed приходит с задержкой и пропадает, если подсказку тут же открыли снова, поэтому момент закрытия берём из IsOpen.
        DependencyPropertyDescriptor.FromProperty(Popup.IsOpenProperty, typeof(Popup)).AddValueChanged(_popup, (_, _) =>
        {
            if (!_popup.IsOpen)
            {
                _closedAt = Environment.TickCount64;
            }
        });
        Click += (_, _) => Toggle();
        Unloaded += (_, _) => _popup.IsOpen = false;
    }

    /// <summary>Открывает подсказку или закрывает, если она открыта.</summary>
    private void Toggle()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }

        // Щелчок по значку при открытой подсказке сначала закрывает её (StaysOpen=false), и только потом приходит Click.
        if (Environment.TickCount64 - _closedAt < _reopenGuardMs)
        {
            return;
        }

        Open();
    }

    /// <summary>Строит содержимое, выбирает сторону и показывает подсказку.</summary>
    private void Open()
    {
        _owner = Window.GetWindow(this);
        var probe = BuildContent(0);
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tipHeight = probe.DesiredSize.Height;

        _above = true;
        _shiftX = -_tipLeftOfIcon;
        if (_owner?.Content is UIElement root)
        {
            var origin = TranslatePoint(new Point(0, 0), root);
            _above = origin.Y - tipHeight - 4 >= 0;
            var overflow = origin.X + _shiftX + _tipWidth - (root.RenderSize.Width - 8);
            if (overflow > 0)
            {
                _shiftX -= overflow;
            }
        }

        // Смещение от значка в DIP считаем сами по измеренной высоте: размер, который WPF отдаёт в обратный вызов, ненадёжен
        // и давал разный зазор у разных подсказок.
        _popup.HorizontalOffset = _shiftX;
        SetVerticalOffset(probe.DesiredSize.Height);
        var content = BuildContent(-_shiftX + (_iconSize / 2) - 5);
        content.SizeChanged += (_, e) => SetVerticalOffset(e.NewSize.Height);
        _popup.Child = content;
        _popup.IsOpen = true;
        Highlight(true);

        if (_owner is not null)
        {
            _owner.Deactivated += OnOwnerChanged;
            _owner.LocationChanged += OnOwnerChanged;
            InputManager.Current.PreProcessInput += OnInput;
            _owner.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnOwnerScrolled));
        }
    }

    /// <summary>Собирает подсказку: рамка с текстом и стрелка.</summary>
    /// <param name="arrowLeft">Расстояние от левого края подсказки до стрелки.</param>
    /// <returns>Корневой элемент окна подсказки.</returns>
    private Grid BuildContent(double arrowLeft)
    {
        var body = new StackPanel();
        foreach (var paragraph in HelpText.Parse(_text))
        {
            var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 19, Margin = new Thickness(0, 0, 0, 6) };
            block.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text");
            foreach (var run in paragraph.Runs)
            {
                if (run.LineBreak)
                {
                    block.Inlines.Add(new System.Windows.Documents.LineBreak());
                }
                else
                {
                    block.Inlines.Add(new System.Windows.Documents.Run(run.Text) { FontWeight = run.Bold ? FontWeights.SemiBold : FontWeights.Normal });
                }
            }

            body.Children.Add(block);
        }

        if (body.Children.Count > 0)
        {
            ((FrameworkElement)body.Children[^1]).Margin = new Thickness(0);
        }

        var tip = new Border
        {
            Width = _tipWidth,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Child = body,
        };
        tip.SetResourceReference(Border.BackgroundProperty, "Km.Card2");
        tip.SetResourceReference(Border.BorderBrushProperty, "Km.Line");

        // Ромб 10×10: половина его выступает из рамки к значку, вторая лежит поверх рамки и закрашена цветом подсказки.
        var arrow = new Border
        {
            Width = 10,
            Height = 10,
            BorderThickness = _above ? new Thickness(0, 0, 1, 1) : new Thickness(1, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = _above ? VerticalAlignment.Bottom : VerticalAlignment.Top,
            Margin = new Thickness(arrowLeft, 0, 0, -5),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(45),
        };
        if (!_above)
        {
            arrow.Margin = new Thickness(arrowLeft, -5, 0, 0);
        }

        arrow.SetResourceReference(Border.BackgroundProperty, "Km.Card2");
        arrow.SetResourceReference(Border.BorderBrushProperty, "Km.Line");

        var column = new Grid();
        column.RowDefinitions.Add(new RowDefinition { Height = _above ? GridLength.Auto : new GridLength(_arrowRow) });
        column.RowDefinitions.Add(new RowDefinition { Height = _above ? new GridLength(_arrowRow) : GridLength.Auto });
        var tipRow = _above ? 0 : 1;
        Grid.SetRow(tip, tipRow);
        column.Children.Add(tip);
        Grid.SetRow(arrow, tipRow);
        column.Children.Add(arrow);
        return column;
    }

    /// <summary>Ставит подсказку над значком или под ним по высоте окна подсказки; вызывается и после настоящей раскладки.</summary>
    /// <param name="height">Высота окна подсказки в DIP, вместе с отступом под тень.</param>
    private void SetVerticalOffset(double height) =>
        _popup.VerticalOffset = _above ? -height - _gap : ActualHeight + _gap;

    /// <summary>Заливает значок акцентом, пока подсказка открыта.</summary>
    /// <param name="on">Подсказка открыта.</param>
    private void Highlight(bool on)
    {
        if (on)
        {
            SetResourceReference(BackgroundProperty, "Km.Accent");
            SetResourceReference(BorderBrushProperty, "Km.Accent");
            SetResourceReference(ForegroundProperty, "Km.AccentText");
        }
        else
        {
            ClearValue(BackgroundProperty);
            ClearValue(BorderBrushProperty);
            ClearValue(ForegroundProperty);
        }
    }

    /// <summary>Подсказка закрыта любым способом: снимаем подписки и заливку.</summary>
    private void OnClosed()
    {
        _closedAt = Environment.TickCount64;
        Highlight(false);
        if (_owner is not null)
        {
            _owner.Deactivated -= OnOwnerChanged;
            _owner.LocationChanged -= OnOwnerChanged;
            InputManager.Current.PreProcessInput -= OnInput;
            _owner.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnOwnerScrolled));
            _owner = null;
        }
    }

    /// <summary>Окно потеряло активность или сдвинулось.</summary>
    private void OnOwnerChanged(object? sender, EventArgs e) => _popup.IsOpen = false;

    /// <summary>Страницу прокрутили: подсказка осталась бы на старом месте.</summary>
    private void OnOwnerScrolled(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.HorizontalChange != 0)
        {
            _popup.IsOpen = false;
        }
    }

    /// <summary>Любая клавиша закрывает подсказку; Esc дальше не идёт.</summary>
    private void OnInput(object sender, PreProcessInputEventArgs e)
    {
        // Слушаем весь ввод приложения, а не окно: после щелчка мышью в окне нет элемента с фокусом, и клавиши до окна не доходят.
        if (e.StagingItem.Input is KeyEventArgs key && key.RoutedEvent == Keyboard.PreviewKeyDownEvent)
        {
            // Esc не идёт дальше (иначе закроет страницу или окно), остальные клавиши работают как обычно.
            key.Handled = key.Key == Key.Escape;
            _popup.IsOpen = false;
        }
    }
}
