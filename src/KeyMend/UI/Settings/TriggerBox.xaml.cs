using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shapes;
using System.Windows.Threading;
using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;
using Trigger = KeyMend.Rules.Trigger;

namespace KeyMend.UI.Settings;

/// <summary>
/// Поле «Клавиши» редактора правила (SPEC 5): запись жеста с клавиатуры, вид нажатия (одиночное или двойное), время
/// и замечания проверок. Само ничего не сохраняет: отдаёт триггер хозяину событием <see cref="TriggerChanged"/>.
/// </summary>
internal sealed partial class TriggerBox : UserControl
{
    /// <summary>Хук записи; задаётся <see cref="Initialize"/>.</summary>
    private ITriggerRecording? _recording;

    /// <summary>Триггер, который показывает поле; <c>null</c> — не задан.</summary>
    private Trigger? _trigger;

    /// <summary>Идёт запись.</summary>
    private bool _isRecording;

    /// <summary>Замечания проверок к текущему триггеру.</summary>
    private IReadOnlyList<TriggerIssue> _issues = [];

    /// <summary>Элементы заполняются из кода: обработчики выбора в этот момент ничего не делают.</summary>
    private bool _syncing;

    /// <summary>Окно, которому принадлежит поле: при потере им активности запись прерывается.</summary>
    private Window? _window;

    /// <summary>Тик раз в секунду, пока идёт запись.</summary>
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>Момент (<see cref="Environment.TickCount64"/>), когда запись отменится сама.</summary>
    private long _recordingEnds;

    /// <summary>Создаёт поле без триггера.</summary>
    public TriggerBox()
    {
        InitializeComponent();
        _countdown.Tick += OnCountdownTick;
        TimeBox.Committed += OnTimeCommitted;
        TimeGroup.Children.Add(new HelpButton(Strings.TriggerHoldFor, Strings.HelpTime));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Render();
    }

    /// <summary>Пользователь записал новый триггер или изменил вид и время; <c>null</c> не бывает.</summary>
    public event Action<Trigger?>? TriggerChanged;

    /// <summary>Пользователь нажал «Открыть это правило» в сообщении о занятых клавишах.</summary>
    public event Action<Rule>? OpenRuleRequested;

    /// <summary>Триггер в поле; <c>null</c> — не задан.</summary>
    public Trigger? Trigger => _trigger;

    /// <summary>Подключает запись клавиш.</summary>
    /// <param name="recording">Запись триггера через хук.</param>
    public void Initialize(ITriggerRecording recording) => _recording = recording;

    /// <summary>Показывает триггер (не вызывает <see cref="TriggerChanged"/>).</summary>
    /// <param name="trigger">Триггер или <c>null</c>.</param>
    public void SetTrigger(Trigger? trigger)
    {
        _trigger = trigger;
        Render();
    }

    /// <summary>Показывает замечания проверок под полем.</summary>
    /// <param name="issues">Замечания: ошибки, предупреждения, сведения.</param>
    public void ShowIssues(IReadOnlyList<TriggerIssue> issues)
    {
        _issues = issues;
        Render();
    }

    /// <summary>Подписывается на потерю активности окном.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.Deactivated += OnWindowDeactivated;
        }
    }

    /// <summary>Страница закрыта: незаконченная запись прерывается, чтобы правила снова заработали.</summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        StopRecording();
    }

    /// <summary>Окно потеряло активность: запись, которую пользователь не видит, прерывается.</summary>
    private void OnWindowDeactivated(object? sender, EventArgs e) => StopRecording();

    /// <summary>Прерывает запись, если она идёт.</summary>
    private void StopRecording()
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        _recording?.Cancel();
        Render();
    }

    /// <summary>Кнопка справа: начать запись, записать заново или отменить идущую.</summary>
    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (_recording is null)
        {
            return;
        }

        if (_isRecording)
        {
            StopRecording();
            return;
        }

        _isRecording = true;
        _recordingEnds = Environment.TickCount64 + (long)TriggerRecording.Timeout.TotalMilliseconds;
        _countdown.Start();
        Render();
        _recording.Start(OnRecorded);
    }

    /// <summary>Раз в секунду обновляет остаток времени записи.</summary>
    private void OnCountdownTick(object? sender, EventArgs e) => UpdateSecondsLeft();

    /// <summary>Показывает, через сколько секунд запись отменится сама.</summary>
    private void UpdateSecondsLeft()
    {
        var left = Math.Max(0, (_recordingEnds - Environment.TickCount64 + 999) / 1000);
        SecondsLeft.Text = Strings.TriggerSecondsLeft.Replace("{0}", left.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);
    }

    /// <summary>Запись закончилась жестом или отменой (Esc, тайм-аут).</summary>
    private void OnRecorded(RecordedGesture gesture)
    {
        _isRecording = false;
        if (!gesture.Cancelled)
        {
            _trigger = Merge(gesture.ToTrigger(), _trigger);
            TriggerChanged?.Invoke(_trigger);
        }

        Render();
    }

    /// <summary>Переносит на записанный триггер вид и время прежнего: «двойное» и время удержания остаются.</summary>
    private static Trigger Merge(Trigger recorded, Trigger? previous)
    {
        var result = previous?.DoubleMs is { } doubleMs ? recorded with { DoubleMs = doubleMs } : recorded;
        return result is TapTrigger tap && previous is TapTrigger old ? tap with { MaxHoldMs = old.MaxHoldMs } : result;
    }

    /// <summary>Переключатель «Одиночное / Двойное».</summary>
    private void OnKindChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || _trigger is null)
        {
            return;
        }

        var isDouble = DoubleButton.IsChecked == true;
        if (isDouble == (_trigger.DoubleMs is not null))
        {
            return;
        }

        _trigger = _trigger with { DoubleMs = isDouble ? TriggerLimits.DefaultDoubleMs : null };
        TriggerChanged?.Invoke(_trigger);
        Render();
    }

    /// <summary>Применяет введённое время: удержание для tap или интервал двойного, в допустимых границах.</summary>
    private void OnTimeCommitted()
    {
        if (_syncing || _trigger is null)
        {
            return;
        }

        if (TimeBox.Value is { } value)
        {
            var changed = _trigger switch
            {
                { DoubleMs: not null } => _trigger with { DoubleMs = TriggerLimits.ClampDouble(value) },
                TapTrigger tap => tap with { MaxHoldMs = TriggerLimits.ClampHold(value) },
                _ => _trigger,
            };
            if (changed != _trigger)
            {
                _trigger = changed;
                TriggerChanged?.Invoke(_trigger);
            }
        }

        Render();
    }

    /// <summary>Перерисовывает поле, полосу замечаний и строку времени по текущему состоянию.</summary>
    private void Render()
    {
        _syncing = true;
        RenderField();
        RenderBar();
        RenderTiming();
        _syncing = false;
    }

    /// <summary>Содержимое поля, рамка и подпись кнопки.</summary>
    private void RenderField()
    {
        FieldContent.Children.Clear();
        SecondsLeft.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;
        if (_isRecording)
        {
            UpdateSecondsLeft();
        }
        else
        {
            _countdown.Stop();
        }

        var hasError = !_isRecording && _issues.Any(i => i.Severity == TriggerIssueSeverity.Error);
        var edge = _isRecording ? "Km.AccentEdge" : hasError ? "Km.Error" : "Km.FieldLine";
        Field.SetResourceReference(Border.BorderBrushProperty, edge);
        Field.BorderThickness = _isRecording || hasError ? new Thickness(1, 1, 1, 2) : new Thickness(1);
        if (_isRecording)
        {
            var dot = new Ellipse { Width = 10, Height = 10, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            dot.SetResourceReference(Shape.FillProperty, "Km.Error");
            FieldContent.Children.Add(dot);
            FieldContent.Children.Add(KeyChipsBuilder.Label(Strings.TriggerRecordingPrompt, "Km.Text"));
            ActionButton.Content = Strings.TriggerCancel;
        }
        else if (_trigger is null)
        {
            FieldContent.Children.Add(KeyChipsBuilder.Label(Strings.TriggerNotSet, "Km.Text2"));
            ActionButton.Content = Strings.TriggerRecord;
        }
        else
        {
            KeyChipsBuilder.Fill(FieldContent, TriggerDisplay.Chips(_trigger), (Style)FindResource("Km.KeyChip"));
            ActionButton.Content = Strings.TriggerRerecord;
        }
    }

    /// <summary>Полоса под полем: подсказка во время записи, иначе замечания проверок.</summary>
    private void RenderBar()
    {
        BarText.Inlines.Clear();
        if (_isRecording)
        {
            BarText.Inlines.Add(new Run(Strings.TriggerRecordingNote));
            ShowBar("Km.Line");
            return;
        }

        if (_trigger is null || _issues.Count == 0)
        {
            Bar.Visibility = Visibility.Collapsed;
            return;
        }

        for (var i = 0; i < _issues.Count; i++)
        {
            if (i > 0)
            {
                BarText.Inlines.Add(new LineBreak());
            }

            AddIssue(_issues[i], _trigger);
        }

        var worst = _issues.Max(issue => issue.Severity);
        ShowBar(worst switch
        {
            TriggerIssueSeverity.Error => "Km.Error",
            TriggerIssueSeverity.Warning => "Km.Warning",
            _ => "Km.Line",
        });
    }

    /// <summary>Показывает полосу с рамкой нужного цвета.</summary>
    private void ShowBar(string edgeKey)
    {
        Bar.SetResourceReference(Border.BorderBrushProperty, edgeKey);
        Bar.Visibility = Visibility.Visible;
    }

    /// <summary>Добавляет текст замечания и, для занятых клавиш, ссылку на другое правило.</summary>
    private void AddIssue(TriggerIssue issue, Trigger trigger)
    {
        BarText.Inlines.Add(new Run(TriggerDisplay.Message(issue, trigger)));
        if (issue is { Kind: TriggerIssueKind.Busy, Other: { } other })
        {
            var link = new Hyperlink(new Run(Strings.TriggerOpenRule));
            link.SetResourceReference(TextElement.ForegroundProperty, "Km.AccentInk");
            link.Click += (_, _) => OpenRuleRequested?.Invoke(other);
            BarText.Inlines.Add(new Run(" "));
            BarText.Inlines.Add(link);
        }
    }

    /// <summary>Строка «Одиночное / Двойное» и время; видна, когда триггер задан и запись не идёт.</summary>
    private void RenderTiming()
    {
        if (_trigger is null || _isRecording)
        {
            TimingRow.Visibility = Visibility.Collapsed;
            return;
        }

        TimingRow.Visibility = Visibility.Visible;
        var isDouble = _trigger.DoubleMs is not null;
        SingleButton.IsChecked = !isDouble;
        DoubleButton.IsChecked = isDouble;
        var milliseconds = _trigger switch
        {
            { DoubleMs: { } interval } => interval,
            TapTrigger tap => tap.MaxHoldMs,
            _ => (int?)null,
        };

        // Одиночное сочетание срабатывает на нажатие: времени у него нет.
        TimeGroup.Visibility = milliseconds is null ? Visibility.Collapsed : Visibility.Visible;
        TimeLabel.Text = isDouble ? Strings.TriggerDoubleWithin : Strings.TriggerHoldFor;
        TimeBox.Text = milliseconds?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
    }
}
