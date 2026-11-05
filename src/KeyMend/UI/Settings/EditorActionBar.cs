using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>
/// Нижняя панель редакторов правила и сокращения (SPEC 5): «Удалить» слева, «Отмена» и «Сохранить» справа. Окно прижимает её к
/// низу поверх содержимого.
/// </summary>
internal static class EditorActionBar
{
    /// <summary>Собирает панель.</summary>
    /// <param name="owner">Элемент страницы: из его ресурсов берутся стили и кисти.</param>
    /// <param name="delete">Кнопка «Удалить …»; обработчик нажатия ставит страница.</param>
    /// <param name="cancel">Действие «Отмена»: вернуться к таблице без вопросов.</param>
    /// <param name="save">Действие «Сохранить»: страница сама проверяет, сохраняет и закрывается.</param>
    /// <returns>Панель.</returns>
    public static Border Create(FrameworkElement owner, Button delete, Action cancel, Action save)
    {
        delete.Foreground = (Brush)owner.FindResource("Km.Error");
        var cancelButton = new Button { Content = Strings.DialogCancel, MinHeight = 32, MinWidth = 96, Margin = new Thickness(0, 0, 8, 0) };
        cancelButton.Click += (_, _) => cancel();
        var saveButton = new Button { Style = (Style)owner.FindResource("Km.AccentButton"), Content = Strings.EditorSave, MinWidth = 96 };
        saveButton.Click += (_, _) => save();

        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(cancelButton);
        right.Children.Add(saveButton);
        delete.HorizontalAlignment = HorizontalAlignment.Left;
        var grid = new Grid();
        grid.Children.Add(delete);
        grid.Children.Add(right);

        var bar = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid,
            Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Direction = 270, Opacity = 0.2, Color = Colors.Black },
        };
        bar.SetResourceReference(Border.BackgroundProperty, "Km.Card");
        bar.SetResourceReference(Border.BorderBrushProperty, "Km.Line");
        return bar;
    }
}
