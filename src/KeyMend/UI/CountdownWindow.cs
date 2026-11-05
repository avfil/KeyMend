using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KeyMend.Resources;

namespace KeyMend.UI;

/// <summary>
/// Небольшое окно отсчёта с кнопкой «Отменить» (SPEC F4.2). Появляется поверх остальных окон в правом нижнем углу рабочей области
/// и не забирает фокус, чтобы не перехватить набираемый текст. Закрытие крестиком считается отменой.
/// </summary>
internal sealed class CountdownWindow : Window
{
    /// <summary>Отступ окна от краёв рабочей области.</summary>
    private const double _margin = 16;

    /// <summary>Текст про действие без секунд.</summary>
    private readonly string _message;

    /// <summary>Надпись с остатком времени.</summary>
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };

    /// <summary>Секундный таймер отсчёта.</summary>
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>Сколько секунд осталось.</summary>
    private int _left;

    /// <summary>Итог уже сообщён.</summary>
    private bool _completed;

    /// <summary>Создаёт окно; показать его нужно вызовом <see cref="Window.Show"/>.</summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="message">Что произойдёт по окончании отсчёта.</param>
    /// <param name="seconds">Длительность отсчёта.</param>
    public CountdownWindow(string title, string message, int seconds)
    {
        _message = message;
        _left = seconds;
        Title = title;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var cancel = new Button { Content = Strings.CountdownCancel, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), MinWidth = 96 };
        cancel.Click += (_, _) => Finish(confirmed: false);
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(_text);
        panel.Children.Add(cancel);
        Content = panel;

        _timer.Tick += OnTick;
        Loaded += (_, _) => PlaceInCorner();
        Closed += (_, _) => _timer.Stop();
        Closing += (_, _) => Report(confirmed: false);
        ShowText();
    }

    /// <summary>Итог отсчёта: <c>true</c> — время вышло, <c>false</c> — отмена или закрытие окна. Вызывается один раз.</summary>
    public event Action<bool>? Completed;

    /// <summary>Запускает таймер после показа окна.</summary>
    /// <param name="e">Данные события.</param>
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _timer.Start();
    }

    /// <summary>Завершает окно с итогом.</summary>
    /// <param name="confirmed"><c>true</c> — время вышло.</param>
    public void Finish(bool confirmed)
    {
        Report(confirmed);
        Close();
    }

    /// <summary>Уменьшает остаток на секунду; на нуле отсчёт считается завершённым.</summary>
    /// <param name="sender">Таймер.</param>
    /// <param name="e">Данные события.</param>
    private void OnTick(object? sender, EventArgs e)
    {
        _left--;
        if (_left <= 0)
        {
            Finish(confirmed: true);
            return;
        }

        ShowText();
    }

    /// <summary>Выводит сообщение с остатком времени.</summary>
    private void ShowText() => _text.Text = _message + "\n" + Strings.Fill(Strings.CountdownLeft, _left.ToString(CultureInfo.CurrentCulture));

    /// <summary>Сообщает итог один раз.</summary>
    /// <param name="confirmed">Итог.</param>
    private void Report(bool confirmed)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Completed?.Invoke(confirmed);
    }

    /// <summary>Ставит окно в правый нижний угол рабочей области.</summary>
    private void PlaceInCorner()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - _margin;
        Top = area.Bottom - ActualHeight - _margin;
    }
}
