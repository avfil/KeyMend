using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Редактор одного шага в карточке «Что делает»: показывает поля шага и отдаёт шаг с внесёнными правками.</summary>
internal abstract class StepEditor : UserControl
{
    /// <summary>Пользователь изменил поле шага.</summary>
    public event Action? Changed;

    /// <summary>Шаг с текущими значениями полей; поля, которых в окне нет, берутся из исходного шага.</summary>
    public abstract Step Current { get; }

    /// <summary>Сообщает хозяину, что шаг изменился.</summary>
    protected void RaiseChanged() => Changed?.Invoke();

    /// <summary>Создаёт сетку «подпись — поле» для полей шага.</summary>
    /// <returns>Пустая сетка из двух колонок.</returns>
    internal static Grid CreateForm()
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    /// <summary>Добавляет строку «подпись — поле» в сетку.</summary>
    /// <param name="grid">Сетка из <see cref="CreateForm"/>.</param>
    /// <param name="label">Подпись слева.</param>
    /// <param name="control">Поле справа.</param>
    /// <param name="top">Подпись прижата к верху: поле занимает несколько строк.</param>
    /// <param name="stretch">Поле занимает всю ширину колонки, а не свою естественную.</param>
    /// <param name="help">Текст подсказки «?» (SPEC F9) или <c>null</c>, если подсказки нет.</param>
    internal static void AddRow(Grid grid, string label, FrameworkElement control, bool top = false, bool stretch = false, string? help = null)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var text = KeyChipsBuilder.Label(label, "Km.Text", 14, margin: false);
        if (help is not null)
        {
            text.Inlines.Add(new InlineUIContainer(new HelpButton(label, help)) { BaselineAlignment = BaselineAlignment.Center });
        }

        text.Margin = new Thickness(0, top ? 12 : 0, 12, 0);
        text.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(text, row);
        grid.Children.Add(text);
        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + 7, control.Margin.Right, control.Margin.Bottom + 7);
        control.HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
    }
}
