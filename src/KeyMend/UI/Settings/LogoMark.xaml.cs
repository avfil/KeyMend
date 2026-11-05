using System.Windows;
using System.Windows.Controls;

namespace KeyMend.UI.Settings;

/// <summary>Логотип KeyMend: обычный, серый (выключен) и со щитом администратора (SPEC F10).</summary>
internal sealed partial class LogoMark : UserControl
{
    /// <summary>Свойство «серый логотип».</summary>
    public static readonly DependencyProperty GreyedProperty =
        DependencyProperty.Register(nameof(Greyed), typeof(bool), typeof(LogoMark), new PropertyMetadata(false));

    /// <summary>Свойство «красный щит в углу».</summary>
    public static readonly DependencyProperty ShieldProperty =
        DependencyProperty.Register(nameof(Shield), typeof(bool), typeof(LogoMark), new PropertyMetadata(false));

    /// <summary>Создаёт логотип.</summary>
    public LogoMark() => InitializeComponent();

    /// <summary>Серая плитка и серое ядро: KeyMend на паузе.</summary>
    public bool Greyed
    {
        get => (bool)GetValue(GreyedProperty);
        set => SetValue(GreyedProperty, value);
    }

    /// <summary>Красный щит в правом нижнем углу: режим администратора.</summary>
    public bool Shield
    {
        get => (bool)GetValue(ShieldProperty);
        set => SetValue(ShieldProperty, value);
    }
}
