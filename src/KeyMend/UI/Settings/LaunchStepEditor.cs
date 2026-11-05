using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KeyMend.Resources;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Редактор шага «Запустить программу» (SPEC 5, F4.1): файл, чем запускать, параметры, окно, повторный запуск, ожидание, рабочая папка.</summary>
internal sealed class LaunchStepEditor : StepEditor
{
    /// <summary>Исходный шаг.</summary>
    private readonly LaunchStep _step;

    /// <summary>Папка скриптов и диалоги выбора файла.</summary>
    private readonly StepContext _context;

    /// <summary>Файл.</summary>
    private readonly TextBox _file = Mono();

    /// <summary>Чем запускать.</summary>
    private readonly ChoiceBox<LaunchType> _type;

    /// <summary>Параметры.</summary>
    private readonly TextBox _arguments = Mono();

    /// <summary>Окно программы.</summary>
    private readonly SegmentPicker<LaunchWindow> _window;

    /// <summary>Что делать при повторном запуске.</summary>
    private readonly ChoiceBox<LaunchRepeat> _repeat;

    /// <summary>Ждать завершения.</summary>
    private readonly CheckBox _wait = new();

    /// <summary>Рабочая папка.</summary>
    private readonly TextBox _workDir = Mono();

    /// <summary>Создаёт редактор.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="context">Папка скриптов и диалоги выбора файла.</param>
    public LaunchStepEditor(LaunchStep step, StepContext context)
    {
        _step = step;
        _context = context;
        _type = new ChoiceBox<LaunchType>(
            Strings.LaunchType,
            [
                (LaunchType.Exe, Strings.LaunchTypeExe),
                (LaunchType.Pwsh, Strings.LaunchTypePwsh),
                (LaunchType.PowerShell, Strings.LaunchTypePowerShell),
                (LaunchType.Cmd, Strings.LaunchTypeCmd),
            ],
            step.Type);
        _window = new SegmentPicker<LaunchWindow>(
            Strings.LaunchWindow,
            [(LaunchWindow.Normal, Strings.LaunchWindowNormal), (LaunchWindow.Hidden, Strings.LaunchWindowHidden), (LaunchWindow.Minimized, Strings.LaunchWindowMin)],
            step.Window);
        _repeat = new ChoiceBox<LaunchRepeat>(
            Strings.LaunchRepeat,
            [(LaunchRepeat.Ignore, Strings.LaunchRepeatIgnore), (LaunchRepeat.Another, Strings.LaunchRepeatAnother), (LaunchRepeat.StopPrevious, Strings.LaunchRepeatStop)],
            step.Repeat);

        _file.Text = step.File;
        _arguments.Text = step.Arguments;
        _workDir.Text = step.WorkingDirectory ?? string.Empty;
        _wait.IsChecked = step.Wait;
        _wait.SetResourceReference(StyleProperty, "Km.Switch");
        AutomationProperties.SetName(_file, Strings.LaunchFile);
        AutomationProperties.SetName(_arguments, Strings.LaunchArgs);
        AutomationProperties.SetName(_workDir, Strings.LaunchWorkDir);
        AutomationProperties.SetName(_wait, Strings.LaunchWait);


        _file.TextChanged += (_, _) => RaiseChanged();
        _arguments.TextChanged += (_, _) => RaiseChanged();
        _workDir.TextChanged += (_, _) => RaiseChanged();
        _type.Changed += RaiseChanged;
        _window.Changed += RaiseChanged;
        _repeat.Changed += RaiseChanged;
        _wait.Click += (_, _) => RaiseChanged();

        var form = CreateForm();
        AddRow(form, Strings.LaunchFile, WithBrowse(_file, OnBrowseFile), stretch: true, help: Strings.HelpLaunchFile);
        AddRow(form, Strings.LaunchType, _type, help: Strings.HelpLaunchType);
        AddRow(form, Strings.LaunchArgs, _arguments, stretch: true, help: Strings.HelpLaunchArgs);
        AddRow(form, Strings.LaunchWindow, _window, help: Strings.HelpLaunchWindow);
        AddRow(form, Strings.LaunchRepeat, _repeat, help: Strings.HelpLaunchRepeat);
        AddRow(form, Strings.LaunchWait, _wait, help: Strings.HelpLaunchWait);
        AddRow(form, Strings.LaunchWorkDir, WithBrowse(_workDir, OnBrowseFolder, Strings.LaunchWorkDirHint), stretch: true, help: Strings.HelpLaunchWorkDir);
        Content = form;
    }

    /// <inheritdoc/>
    public override Step Current => _step with
    {
        File = _file.Text.Trim(),
        Type = _type.Value,
        Arguments = _arguments.Text.Trim(),
        Window = _window.Value,
        Repeat = _repeat.Value,
        Wait = _wait.IsChecked == true,
        WorkingDirectory = string.IsNullOrWhiteSpace(_workDir.Text) ? null : _workDir.Text.Trim(),
    };

    /// <summary>Моноширинное поле, как на макете.</summary>
    private static TextBox Mono() => new()
    {
        MinHeight = 32,
        MinWidth = 260,
        FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>Поле и кнопка «Обзор…» справа; у пустого поля — серая подсказка.</summary>
    private static Grid WithBrowse(TextBox box, Action browse, string? hint = null)
    {
        var button = new Button { Content = Strings.LaunchBrowse, MinHeight = 32, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 0, 14, 0) };
        button.Click += (_, _) => browse();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(box);
        if (hint is not null)
        {
            var placeholder = KeyChipsBuilder.Label(hint, "Km.Text2", 14, margin: false);
            placeholder.IsHitTestVisible = false;
            placeholder.Margin = new Thickness(12, 0, 0, 0);
            placeholder.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (_, _) => placeholder.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            grid.Children.Add(placeholder);
        }

        Grid.SetColumn(button, 1);
        grid.Children.Add(button);
        return grid;
    }

    /// <summary>«Обзор…» у файла: подставляет путь и подбирает тип запуска по расширению.</summary>
    private void OnBrowseFile()
    {
        var start = LaunchFileChoice.StartDirectory(_context.ScriptsDir, Directory.Exists, (_file.Text, true));
        if (_context.Picker.PickFile(start) is not { } chosen)
        {
            return;
        }

        _file.Text = LaunchFileChoice.ToStored(chosen, _context.ScriptsDir);
        _type.SelectedValue = LaunchFileChoice.TypeFor(Path.GetExtension(chosen), _type.Value);
    }

    /// <summary>«Обзор…» у рабочей папки: диалог открывается в уже заданной папке, иначе в папке файла.</summary>
    private void OnBrowseFolder()
    {
        var start = LaunchFileChoice.StartDirectory(_context.ScriptsDir, Directory.Exists, (_workDir.Text, false), (_file.Text, true));
        if (_context.Picker.PickFolder(start) is { } chosen)
        {
            _workDir.Text = chosen;
        }
    }
}
