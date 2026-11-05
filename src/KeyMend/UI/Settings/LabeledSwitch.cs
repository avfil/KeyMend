using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;

namespace KeyMend.UI.Settings;

/// <summary>
/// Переключатель с подписью «Вкл./Откл.» справа постоянной ширины (SPEC 5). Событие <see cref="Toggled"/> приходит
/// от пользователя (в том числе через UI Automation); значение, выставленное из кода, подпись обновляет, но событие не вызывает.
/// </summary>
internal sealed class LabeledSwitch : StackPanel
{
    /// <summary>Сам переключатель.</summary>
    private readonly CheckBox _box = new();

    /// <summary>Подпись состояния.</summary>
    private readonly TextBlock _label = new() { Width = 44, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Значение выставляется из кода: <see cref="Toggled"/> не вызывается.</summary>
    private bool _fromCode;

    /// <summary>Создаёт переключатель.</summary>
    /// <param name="automationName">Имя для программ чтения экрана и тестов UI Automation.</param>
    public LabeledSwitch(string automationName)
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        _box.SetResourceReference(StyleProperty, "Km.Switch");
        AutomationProperties.SetName(_box, automationName);
        _label.SetResourceReference(TextBlock.ForegroundProperty, "Km.Text");
        _box.Checked += (_, _) => OnChanged();
        _box.Unchecked += (_, _) => OnChanged();
        Children.Add(_box);
        Children.Add(_label);
        RefreshLabel();
    }

    /// <summary>Пользователь переключил; новое состояние в <see cref="IsOn"/>.</summary>
    public event Action? Toggled;

    /// <summary>Включён ли переключатель.</summary>
    public bool IsOn
    {
        get => _box.IsChecked == true;
        set
        {
            _fromCode = true;
            _box.IsChecked = value;
            _fromCode = false;
            RefreshLabel();
        }
    }

    /// <summary>Состояние изменилось: подпись обновляется, а пользовательское изменение (мышь, клавиатура, UI Automation) передаётся дальше.</summary>
    private void OnChanged()
    {
        RefreshLabel();
        if (!_fromCode)
        {
            Toggled?.Invoke();
        }
    }

    /// <summary>Ставит подпись по состоянию.</summary>
    private void RefreshLabel() => _label.Text = IsOn ? Strings.SwitchOn : Strings.SwitchOff;
}
