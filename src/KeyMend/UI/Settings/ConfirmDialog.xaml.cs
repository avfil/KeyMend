using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyMend.UI.Themes;

namespace KeyMend.UI.Settings;

/// <summary>Вопросы окна настроек, на которые отвечают кнопкой (SPEC 5): удалить правило, сохранить изменения.</summary>
internal interface IDialogs
{
    /// <summary>Показывает вопрос и ждёт ответа.</summary>
    /// <param name="title">Заголовок вопроса.</param>
    /// <param name="text">Пояснение.</param>
    /// <param name="buttons">Подписи кнопок слева направо; первая — главная.</param>
    /// <returns>Номер нажатой кнопки или −1, если окно закрыли (Esc, крестик).</returns>
    int Ask(string title, string text, params string[] buttons);
}

/// <summary>Своё окно вопроса в теме приложения: системное <c>MessageBox</c> не следует тёмной теме.</summary>
internal sealed partial class ConfirmDialog : Window
{
    /// <summary>Номер нажатой кнопки.</summary>
    private int _result = -1;

    /// <summary>Создаёт окно вопроса.</summary>
    /// <param name="owner">Окно, над которым оно открывается.</param>
    /// <param name="themes">Тема: окну ставится светлая или тёмная рамка.</param>
    /// <param name="title">Заголовок вопроса.</param>
    /// <param name="text">Пояснение.</param>
    /// <param name="buttons">Подписи кнопок слева направо; первая — главная.</param>
    public ConfirmDialog(Window owner, ThemeManager themes, string title, string text, IReadOnlyList<string> buttons)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        TitleText.Text = title;
        BodyText.Text = text;
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button { Content = buttons[i], MinWidth = 96, Height = 32, Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0) };
            if (i == 0)
            {
                button.Style = (Style)FindResource("Km.AccentButton");
                button.IsDefault = true;
            }

            button.Click += (_, _) =>
            {
                _result = index;
                Close();
            };
            _ = Buttons.Children.Add(button);
        }

        themes.Attach(this);
    }

    /// <summary>Показывает окно и возвращает ответ.</summary>
    /// <returns>Номер нажатой кнопки или −1.</returns>
    public int ShowAndGetResult()
    {
        _ = ShowDialog();
        return _result;
    }

    /// <summary>Esc закрывает вопрос без ответа.</summary>
    /// <param name="sender">Окно.</param>
    /// <param name="e">Данные нажатия.</param>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
