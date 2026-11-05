using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KeyMend.UI.Settings;

/// <summary>Два списка языков со стрелкой ⇄ между ними: пара, между которой переключается язык или исправляется текст.</summary>
internal sealed class LanguagePairPicker : StackPanel
{
    /// <summary>Первый язык пары.</summary>
    private readonly ComboBox _first = Combo();

    /// <summary>Второй язык пары.</summary>
    private readonly ComboBox _second = Combo();

    /// <summary>Идёт заполнение из кода: выбор ничего не сообщает.</summary>
    private bool _syncing;

    /// <summary>Создаёт пару списков.</summary>
    public LanguagePairPicker()
    {
        Orientation = Orientation.Horizontal;
        var arrow = new Path
        {
            Data = Geometry.Parse("M3,7 H16 L13,4 M17,13 H4 L7,16"),
            Width = 20,
            Height = 20,
            StrokeThickness = 1.3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Margin = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        arrow.SetResourceReference(Shape.StrokeProperty, "Km.Text2");
        Children.Add(_first);
        Children.Add(arrow);
        Children.Add(_second);
        _first.SelectionChanged += OnSelectionChanged;
        _second.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>Пользователь выбрал другой язык.</summary>
    public event Action? Changed;

    /// <summary>Теги выбранных языков; <c>null</c> в позиции, где ничего не выбрано.</summary>
    public (string? First, string? Second) Selected => (_first.SelectedValue as string, _second.SelectedValue as string);

    /// <summary>Заполняет списки и выбирает язык в каждом.</summary>
    /// <param name="choices">Языки для выбора.</param>
    /// <param name="first">Тег первого языка.</param>
    /// <param name="second">Тег второго языка.</param>
    /// <param name="nameFirst">Имя первого списка для программ чтения с экрана.</param>
    /// <param name="nameSecond">Имя второго списка для программ чтения с экрана.</param>
    public void Load(IReadOnlyList<LanguageChoice> choices, string first, string second, string nameFirst, string nameSecond)
    {
        _syncing = true;
        Fill(_first, choices, first, nameFirst);
        Fill(_second, choices, second, nameSecond);
        _syncing = false;
    }

    /// <summary>Создаёт список языка.</summary>
    private static ComboBox Combo() => new() { MinWidth = 180, MinHeight = 32, DisplayMemberPath = nameof(LanguageChoice.Name), SelectedValuePath = nameof(LanguageChoice.Tag) };

    /// <summary>Кладёт языки в список и выбирает нужный без учёта регистра тега.</summary>
    private static void Fill(ComboBox combo, IReadOnlyList<LanguageChoice> choices, string tag, string name)
    {
        combo.ItemsSource = choices;
        combo.SelectedItem = choices.FirstOrDefault(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase));
        AutomationProperties.SetName(combo, name);
    }

    /// <summary>Выбор в любом из списков.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing)
        {
            Changed?.Invoke();
        }
    }
}
