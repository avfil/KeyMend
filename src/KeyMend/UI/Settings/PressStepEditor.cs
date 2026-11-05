using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KeyMend.Input;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>
/// Редактор шага «Нажать клавишу» (SPEC 3.2): текстовое поле с сочетанием (<c>Ctrl+Enter</c>) и меню «Выбрать ▾» для тех, кто
/// не помнит имён клавиш. Поле и меню правят одно и то же сочетание.
/// </summary>
internal sealed class PressStepEditor : StepEditor
{
    /// <summary>Модификаторы меню: бит «любой» семейства и подпись.</summary>
    private static readonly (ModifierSet Family, string Title)[] _modifiers =
    [
        (ModifierSet.AnyCtrl, "Ctrl"), (ModifierSet.AnyShift, "Shift"), (ModifierSet.AnyAlt, "Alt"), (ModifierSet.AnyWin, "Win"),
    ];

    /// <summary>Клавиши, лежащие в меню прямо.</summary>
    private static readonly VirtualKey[] _plainKeys =
    [
        VirtualKey.Return, VirtualKey.Tab, VirtualKey.Escape, VirtualKey.Space, VirtualKey.Back, VirtualKey.Delete,
        VirtualKey.Insert, VirtualKey.Home, VirtualKey.End, VirtualKey.PageUp, VirtualKey.PageDown,
    ];

    /// <summary>Клавиши подменю «Стрелки».</summary>
    private static readonly VirtualKey[] _arrowKeys = [VirtualKey.Up, VirtualKey.Down, VirtualKey.Left, VirtualKey.Right];

    /// <summary>Исходный шаг.</summary>
    private readonly PressStep _step;

    /// <summary>Поле сочетания.</summary>
    private readonly TextBox _text = new() { MinHeight = 32, Width = 260, VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>Плашки разобранного сочетания.</summary>
    private readonly WrapPanel _chips = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Кнопка меню.</summary>
    private readonly Button _choose = new() { Content = Strings.PressChoose, MinHeight = 32, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 0, 14, 0) };

    /// <summary>Сочетание, которое сейчас в поле; без клавиши, если текст не разобран.</summary>
    private KeyChord _chord;

    /// <summary>Текст ставится из кода: разбор и событие не нужны.</summary>
    private bool _settingText;

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    public PressStepEditor(PressStep step)
    {
        _step = step;
        _chord = step.Keys;
        AutomationProperties.SetName(_text, Strings.PressKeys);
        _text.ToolTip = Strings.PressPlaceholder;
        SetText(step.Keys.Key == VirtualKey.None && step.Keys.Modifiers == ModifierSet.None ? string.Empty : KeyChordParser.Format(step.Keys));

        _text.TextChanged += OnTextChanged;
        _choose.Click += (_, _) => ShowMenu();

        var row = new WrapPanel();
        row.Children.Add(_text);
        row.Children.Add(_choose);
        row.Children.Add(_chips);
        var form = CreateForm();
        AddRow(form, Strings.PressKeys, row, help: Strings.HelpPress);
        Content = form;
        RenderChips();
    }

    /// <inheritdoc/>
    public override Step Current => _step with { Keys = _chord };

    /// <summary>Пользователь правит текст: сочетание разбирается заново.</summary>
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_settingText)
        {
            return;
        }

        _chord = KeyChordParser.TryParse(_text.Text, out var parsed) ? parsed : default;
        RenderChips();
        RaiseChanged();
    }

    /// <summary>Ставит текст без разбора.</summary>
    private void SetText(string text)
    {
        _settingText = true;
        _text.Text = text;
        _settingText = false;
    }

    /// <summary>Сочетание изменено из меню: текст и плашки обновляются.</summary>
    private void Apply(KeyChord chord)
    {
        _chord = chord;
        SetText(chord.Key == VirtualKey.None && chord.Modifiers == ModifierSet.None ? string.Empty : KeyChordParser.Format(chord));
        RenderChips();
        RaiseChanged();
    }

    /// <summary>Рисует плашки разобранного сочетания; для неразобранного текста их нет.</summary>
    private void RenderChips()
    {
        _chips.Children.Clear();
        if (_chord.Key == VirtualKey.None)
        {
            return;
        }

        var names = KeyChordParser.Format(_chord).Split('+');
        for (var i = 0; i < names.Length; i++)
        {
            if (i > 0)
            {
                _chips.Children.Add(KeyChipsBuilder.Label("+", "Km.Text2"));
            }

            _chips.Children.Add(new Border
            {
                Style = (Style)FindResource("Km.KeyChip"),
                Margin = new Thickness(0, 0, 6, 0),
                Child = KeyChipsBuilder.Label(names[i], "Km.Text", 13, margin: false),
            });
        }
    }

    /// <summary>Открывает меню под кнопкой: модификаторы с галочками, потом клавиши.</summary>
    private void ShowMenu()
    {
        var menu = new ContextMenu { PlacementTarget = _choose, Placement = PlacementMode.Bottom };
        foreach (var (family, title) in _modifiers)
        {
            var captured = family;
            var item = new MenuItem { Header = title, IsCheckable = true, IsChecked = ChordEdit.HasModifier(_chord, family), StaysOpenOnClick = false };
            item.Click += (_, _) => Apply(ChordEdit.ToggleModifier(_chord, captured));
            _ = menu.Items.Add(item);
        }

        _ = menu.Items.Add(new Separator());
        foreach (var key in _plainKeys)
        {
            _ = menu.Items.Add(KeyItem(key));
        }

        _ = menu.Items.Add(Submenu(Strings.PressMenuArrows, _arrowKeys));
        _ = menu.Items.Add(Submenu(Strings.PressMenuFKeys, Enumerable.Range(0, 24).Select(i => (VirtualKey)(KeyChordParser.F1 + i))));
        menu.IsOpen = true;
    }

    /// <summary>Подменю клавиш.</summary>
    private MenuItem Submenu(string title, IEnumerable<VirtualKey> keys)
    {
        var parent = new MenuItem { Header = title };
        foreach (var key in keys)
        {
            _ = parent.Items.Add(KeyItem(key));
        }

        return parent;
    }

    /// <summary>Пункт меню с клавишей; выбранная сейчас помечена галочкой.</summary>
    private MenuItem KeyItem(VirtualKey key)
    {
        var item = new MenuItem { Header = KeyChordParser.Format(new KeyChord(ModifierSet.None, key)), IsChecked = _chord.Key == key };
        item.Click += (_, _) => Apply(ChordEdit.SetKey(_chord, key));
        return item;
    }
}
