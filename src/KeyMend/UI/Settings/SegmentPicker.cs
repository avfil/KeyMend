using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace KeyMend.UI.Settings;

/// <summary>Ряд сегментов «один из нескольких» в стиле <c>Km.Segment</c>; у каждого экземпляра своя группа, поэтому несколько таких рядов в одном правиле не мешают друг другу.</summary>
/// <typeparam name="T">Тип значения сегмента.</typeparam>
internal sealed class SegmentPicker<T> : StackPanel
    where T : struct, Enum
{
    /// <summary>Имя группы радиокнопок этого ряда.</summary>
    private readonly string _group = Guid.NewGuid().ToString("N");

    /// <summary>Сегменты по значениям.</summary>
    private readonly List<(T Value, RadioButton Button)> _items = [];

    /// <summary>Создаёт ряд.</summary>
    /// <param name="name">Имя группы для программ чтения с экрана.</param>
    /// <param name="options">Значения и подписи слева направо.</param>
    /// <param name="selected">Выбранное значение.</param>
    public SegmentPicker(string name, IEnumerable<(T Value, string Title)> options, T selected)
    {
        Orientation = Orientation.Horizontal;
        AutomationProperties.SetName(this, name);
        foreach (var (value, title) in options)
        {
            var button = new RadioButton
            {
                Content = title,
                GroupName = _group,
                IsChecked = EqualityComparer<T>.Default.Equals(value, selected),
                Margin = new Thickness(_items.Count == 0 ? 0 : 4, 0, 0, 0),
            };
            button.SetResourceReference(StyleProperty, "Km.Segment");
            button.Checked += OnChecked;
            _items.Add((value, button));
            Children.Add(button);
        }

    }

    /// <summary>Пользователь выбрал другой сегмент.</summary>
    public event Action? Changed;

    /// <summary>Выбранное значение.</summary>
    public T Value => _items.FirstOrDefault(i => i.Button.IsChecked == true).Value;

    /// <summary>Выбран один из сегментов.</summary>
    private void OnChecked(object sender, RoutedEventArgs e)
    {
        Changed?.Invoke();
    }
}

/// <summary>Выпадающий список значений перечисления с подписями.</summary>
/// <typeparam name="T">Тип значения.</typeparam>
internal sealed class ChoiceBox<T> : ComboBox
    where T : struct, Enum
{
    /// <summary>Создаёт список.</summary>
    /// <param name="name">Имя списка для программ чтения с экрана.</param>
    /// <param name="options">Значения и подписи.</param>
    /// <param name="selected">Выбранное значение.</param>
    public ChoiceBox(string name, IEnumerable<(T Value, string Title)> options, T selected)
    {
        SetResourceReference(StyleProperty, typeof(ComboBox));
        MinWidth = 260;
        MinHeight = 32;
        AutomationProperties.SetName(this, name);
        DisplayMemberPath = nameof(Choice.Title);
        SelectedValuePath = nameof(Choice.Value);
        var items = options.Select(o => new Choice(o.Value, o.Title)).ToList();
        ItemsSource = items;
        SelectedItem = items.FirstOrDefault(i => EqualityComparer<T>.Default.Equals(i.Value, selected));
        SelectionChanged += (_, _) => Changed?.Invoke();
    }

    /// <summary>Пользователь выбрал другое значение.</summary>
    public event Action? Changed;

    /// <summary>Выбранное значение.</summary>
    public T Value => SelectedValue is T value ? value : default;

    /// <summary>Пункт списка.</summary>
    /// <param name="Value">Значение.</param>
    /// <param name="Title">Подпись.</param>
    private sealed record Choice(T Value, string Title)
    {
        /// <summary>Подпись: её читают программы чтения с экрана.</summary>
        /// <returns>Подпись пункта.</returns>
        public override string ToString() => Title;
    }
}
