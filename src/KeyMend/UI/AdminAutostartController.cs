using System.IO;
using System.Security;
using System.Windows;
using KeyMend.Infrastructure;
using KeyMend.Resources;

namespace KeyMend.UI;

/// <summary>
/// Диалоги и порядок действий для пункта трея «Автозапуск от администратора» (SPEC 2.6): каждое действие — с явного
/// согласия владельца и запроса UAC; после включения обычный автозапуск выключается (оба сразу дали бы два экземпляра).
/// Вызывается из потока UI.
/// </summary>
/// <param name="admin">Автозапуск от администратора.</param>
/// <param name="autostart">Обычный автозапуск.</param>
/// <param name="log">Журнал.</param>
/// <param name="exit">Завершает этот экземпляр (после перехода на администраторский).</param>
internal sealed class AdminAutostartController(AdminAutostart admin, Autostart autostart, ILog log, Action exit)
{
    /// <summary>Заголовок окон.</summary>
    internal static string Caption => Strings.AdminAutostartCaption;

    /// <summary>Закэшированное состояние: задача есть. Меню трея читает его без запроса к Планировщику.</summary>
    private volatile bool _isOn;

    /// <summary>Есть ли задача автозапуска (по последней проверке).</summary>
    public bool IsOn => _isOn;

    /// <summary>Спрашивает у Планировщика, есть ли задача, и обновляет кэш.</summary>
    /// <returns>Задача: кэш обновлён.</returns>
    public async Task RefreshAsync() => _isOn = await admin.ExistsAsync();

    /// <summary>Включает или выключает автозапуск от администратора, в конце обновляет кэш.</summary>
    /// <returns>Задача: операция закончена или отменена.</returns>
    public async Task ToggleAsync()
    {
        try
        {
            if (await admin.ExistsAsync())
            {
                await RemoveAsync();
            }
            else
            {
                await InstallAsync();
            }
        }
        finally
        {
            await RefreshAsync();
        }
    }

    /// <summary>Спрашивает согласие, ставит копию и задачу, затем предлагает сразу перейти на администраторский экземпляр.</summary>
    private async Task InstallAsync()
    {
        var question = MessageBox.Show(
            Strings.AdminAutostartAsk,
            Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (question != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await admin.InstallAsync();
        if (result == AdminAutostartResult.Cancelled)
        {
            return;
        }

        if (result == AdminAutostartResult.Failed)
        {
            MessageBox.Show(
                Strings.AdminAutostartInstallFailed,
                Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DisableAutostartQuietly();
        var now = MessageBox.Show(
            Strings.AdminAutostartDone,
            Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (now == MessageBoxResult.Yes && await admin.StartNowAsync())
        {
            exit();
        }
    }

    /// <summary>Спрашивает согласие и удаляет задачу и копию (запрос UAC).</summary>
    private async Task RemoveAsync()
    {
        var question = MessageBox.Show(
            Strings.AdminAutostartRemoveAsk,
            Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (question != MessageBoxResult.Yes)
        {
            return;
        }

        if (await admin.RemoveAsync() == AdminAutostartResult.Failed)
        {
            MessageBox.Show(
                Strings.AdminAutostartRemoveFailed,
                Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Выключает обычный автозапуск; ошибка доступа к реестру только пишется в лог.</summary>
    private void DisableAutostartQuietly()
    {
        try
        {
            autostart.Disable();
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            log.Error("autostart disable failed", ex);
        }
    }
}
