using System.Windows;
using System.Windows.Controls;
using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>Рисует клавиши триггера плашками; общий для поля «Клавиши» и таблицы правил.</summary>
internal static class KeyChipsBuilder
{
    /// <summary>Добавляет в панель плашки клавиш: «держать», удерживаемые, плюс, нажимаемые, «×2».</summary>
    /// <param name="panel">Панель, в которую добавляются элементы.</param>
    /// <param name="chips">Из чего состоит триггер.</param>
    /// <param name="chipStyle">Стиль плашки клавиши (<c>Km.KeyChip</c>).</param>
    /// <param name="compact">Мелкий вариант для таблицы: шрифт на единицу меньше.</param>
    public static void Fill(Panel panel, TriggerChips chips, Style chipStyle, bool compact = false)
    {
        if (chips.Hold.Count > 0)
        {
            panel.Children.Add(Label(Strings.TriggerHoldLabel, "Km.Text2", 12));
            AddJoined(panel, chips.Hold, chipStyle, compact);
            panel.Children.Add(Label("+", "Km.Text2"));
        }

        AddJoined(panel, chips.Keys, chipStyle, compact);
        if (chips.IsDouble)
        {
            panel.Children.Add(Label("×2", "Km.Text2"));
        }
    }

    /// <summary>Создаёт подпись цветом из темы.</summary>
    /// <param name="text">Текст.</param>
    /// <param name="brushKey">Ключ кисти темы.</param>
    /// <param name="size">Размер шрифта.</param>
    /// <param name="margin">Отступ справа 6 px.</param>
    /// <returns>Подпись, привязанная к теме.</returns>
    public static TextBlock Label(string text, string brushKey, double size = 14, bool margin = true)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin ? new Thickness(0, 0, 6, 0) : default,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }

    /// <summary>Плашки через «+».</summary>
    private static void AddJoined(Panel panel, IReadOnlyList<string> names, Style chipStyle, bool compact)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                panel.Children.Add(Label("+", "Km.Text2"));
            }

            panel.Children.Add(new Border
            {
                Style = chipStyle,
                Child = Label(names[i], "Km.Text", compact ? 12 : 13, margin: false),
            });
        }
    }
}
