using System.ComponentModel;
using System.Windows;

namespace KeyMend.UI;

/// <summary>Строка меню трея для привязки в шаблоне: сама строка, подсветка и видимость частей.</summary>
/// <param name="entry">Описание строки.</param>
internal sealed class TrayMenuRow(TrayMenuEntry entry) : INotifyPropertyChanged
{
    /// <summary>Высота обычного пункта (SPEC F10, макет).</summary>
    private const double _itemHeight = 30;

    /// <summary>Наименьшая высота пункта со второй строкой.</summary>
    private const double _twoLineHeight = 44;

    /// <summary>Под курсором или выбран стрелками.</summary>
    private bool _isHot;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Описание строки.</summary>
    public TrayMenuEntry Entry { get; } = entry;

    /// <summary>Строка подсвечена.</summary>
    public bool IsHot
    {
        get => _isHot;
        set
        {
            if (_isHot != value)
            {
                _isHot = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHot)));
            }
        }
    }

    /// <summary>Наименьшая высота строки-пункта.</summary>
    public double ItemHeight => Entry.SecondLine is null ? _itemHeight : _twoLineHeight;

    /// <summary>Пункт виден (не разделитель).</summary>
    public Visibility ItemVisibility => Entry.IsSeparator ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Разделитель виден.</summary>
    public Visibility SeparatorVisibility => Entry.IsSeparator ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Галочка видна.</summary>
    public Visibility CheckVisibility => Entry.IsChecked ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Вторая строка видна.</summary>
    public Visibility SecondLineVisibility => Entry.SecondLine is null ? Visibility.Collapsed : Visibility.Visible;
}
