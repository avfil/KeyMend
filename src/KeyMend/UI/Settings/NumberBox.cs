using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeyMend.UI.Settings;

/// <summary>
/// Поле целого числа (SPEC 5): только цифры при вводе и вставке, при входе число выделяется целиком, Enter и потеря фокуса
/// применяют значение, после чего курсор уходит из поля. Нечисловое значение не применяется: хозяин перерисует прежнее.
/// </summary>
internal sealed class NumberBox : TextBox
{
    /// <summary>Создаёт поле с шаблоном <c>Km.NumberBoxTemplate</c>.</summary>
    public NumberBox()
    {
        SetResourceReference(StyleProperty, typeof(TextBox));
        SetResourceReference(TemplateProperty, "Km.NumberBoxTemplate");
        MaxLength = 7;
        TextAlignment = TextAlignment.Right;
        VerticalAlignment = VerticalAlignment.Center;
        IsInactiveSelectionHighlightEnabled = false;
        InputMethod.SetIsInputMethodEnabled(this, false);
        DataObject.AddPastingHandler(this, OnPasting);
    }

    /// <summary>Пользователь закончил ввод (Enter или уход из поля); значение читается из <see cref="Value"/>.</summary>
    public event Action? Committed;

    /// <summary>Число в поле или <c>null</c>, если поле пустое или не число.</summary>
    public int? Value => int.TryParse(Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) ? value : null;

    /// <summary>Показывает число, не вызывая <see cref="Committed"/>.</summary>
    /// <param name="value">Число.</param>
    public void SetValue(int value) => Text = value.ToString(CultureInfo.CurrentCulture);

    /// <summary>В поле допускаются только цифры.</summary>
    /// <param name="e">Данные ввода.</param>
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsAsciiDigit);
        base.OnPreviewTextInput(e);
    }

    /// <summary>При входе в поле число выделяется целиком: новое заменяет старое.</summary>
    /// <param name="e">Данные фокуса.</param>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SelectAll();
    }

    /// <summary>Первый щелчок даёт фокус и выделяет всё, а не ставит курсор в точку клика.</summary>
    /// <param name="e">Данные щелчка.</param>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsKeyboardFocusWithin)
        {
            _ = Focus();
            e.Handled = true;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    /// <summary>Уход из поля применяет значение.</summary>
    /// <param name="e">Данные фокуса.</param>
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Select(0, 0);
        Committed?.Invoke();
    }

    /// <summary>Enter применяет значение и убирает курсор из поля.</summary>
    /// <param name="e">Данные нажатия.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            LeaveField();
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Вставка из буфера: только если там одни цифры.</summary>
    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.All(char.IsAsciiDigit))
        {
            e.CancelCommand();
        }
    }

    /// <summary>Уводит фокус на ближайший объект, которому его можно дать: хозяина поля, а не соседний ввод.</summary>
    private void LeaveField()
    {
        Keyboard.ClearFocus();
        for (DependencyObject? parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is UIElement { Focusable: true } element)
            {
                _ = element.Focus();
                return;
            }
        }
    }
}
