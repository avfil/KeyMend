using KeyMend.Actions;

namespace KeyMend.Infrastructure;

/// <summary>Запущенный процесс, за которым следит шаг «Запустить программу».</summary>
internal interface IRunningProcess : IDisposable
{
    /// <summary>Процесс уже завершился.</summary>
    bool HasExited { get; }

    /// <summary>Ждёт завершения процесса.</summary>
    /// <param name="cancellationToken">Отмена ожидания (сам процесс продолжает работать).</param>
    /// <returns>Код возврата процесса.</returns>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Принудительно завершает процесс (с дочерними, если получится).</summary>
    void Kill();
}

/// <summary>Запускает процессы с правами обычного пользователя (SPEC 2.6).</summary>
internal interface IProcessLauncher
{
    /// <summary>Запускает команду.</summary>
    /// <param name="command">Готовая команда.</param>
    /// <returns>Запущенный процесс или <c>null</c>, если система запустила программу, но отдельного процесса за ней нет.</returns>
    /// <exception cref="InvalidOperationException">Запустить не удалось (файл не открывается, нет оболочки и т. п.).</exception>
    IRunningProcess? Start(LaunchCommand command);
}
