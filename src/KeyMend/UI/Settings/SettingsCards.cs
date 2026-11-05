using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace KeyMend.UI.Settings;

/// <summary>Сборка карточек страниц настроек из кода: заголовок, пояснение и элемент управления справа (SPEC 5).</summary>
internal static class SettingsCards
{
    /// <summary>Создаёт карточку «название, пояснение, элемент справа».</summary>
    /// <param name="title">Название настройки.</param>
    /// <param name="hint">Одна короткая фраза, зачем она нужна.</param>
    /// <param name="right">Элемент управления справа или <c>null</c>.</param>
    /// <returns>Карточка с отступом снизу.</returns>
    /// <param name="help">Текст подсказки «?» (SPEC F9) или <c>null</c>, если подсказки нет.</param>
    public static Border Card(string title, string hint, UIElement? right, string? help = null) =>
        Card(title, new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap }.WithStyle("Km.CardHint"), right, help);

    /// <summary>Создаёт заголовок настройки; с подсказкой в конце строки стоит значок «?».</summary>
    /// <param name="title">Название настройки.</param>
    /// <param name="help">Текст подсказки «?» или <c>null</c>.</param>
    /// <returns>Заголовок.</returns>
    public static TextBlock Title(string title, string? help)
    {
        var block = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }.WithStyle("Km.CardTitle");
        if (help is not null)
        {
            block.Inlines.Add(new InlineUIContainer(new HelpButton(title, help)) { BaselineAlignment = BaselineAlignment.Center });
        }

        return block;
    }

    /// <summary>Создаёт карточку «название, произвольный элемент под ним, элемент справа».</summary>
    /// <param name="title">Название настройки.</param>
    /// <param name="detail">Элемент под названием: пояснение, путь.</param>
    /// <param name="right">Элемент управления справа или <c>null</c>.</param>
    /// <returns>Карточка с отступом снизу.</returns>
    /// <param name="help">Текст подсказки «?» или <c>null</c>.</param>
    public static Border Card(string title, UIElement detail, UIElement? right, string? help = null)
    {
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Title(title, help));
        texts.Children.Add(detail);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(texts);
        if (right is not null)
        {
            right.SetValue(Grid.ColumnProperty, 1);
            ((FrameworkElement)right).Margin = new Thickness(16, 0, 0, 0);
            grid.Children.Add(right);
        }

        var border = new Border { Margin = new Thickness(0, 0, 0, 4), Child = grid };
        border.SetResourceReference(FrameworkElement.StyleProperty, "Km.CardBorder");
        return border;
    }

    /// <summary>Добавляет значок «?» в конец строки (пояснение под заголовком страницы).</summary>
    /// <param name="block">Строка, в конце которой встанет значок.</param>
    /// <param name="topic">О чём подсказка: для читалок экрана.</param>
    /// <param name="help">Текст подсказки «?».</param>
    public static void AddHelp(TextBlock block, string topic, string help) =>
        block.Inlines.Add(new InlineUIContainer(new HelpButton(topic, help)) { BaselineAlignment = BaselineAlignment.Center });

    /// <summary>Создаёт заголовок группы карточек.</summary>
    /// <param name="text">Название группы.</param>
    /// <returns>Заголовок.</returns>
    public static TextBlock GroupTitle(string text) => new TextBlock { Text = text }.WithStyle("Km.GroupTitle");

    /// <summary>Ставит стиль из ресурсов приложения, чтобы он менялся вместе с темой.</summary>
    private static TextBlock WithStyle(this TextBlock block, string key)
    {
        block.SetResourceReference(FrameworkElement.StyleProperty, key);
        return block;
    }
}
