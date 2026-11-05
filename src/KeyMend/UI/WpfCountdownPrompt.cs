using KeyMend.Actions;

namespace KeyMend.UI;

/// <summary>Показывает <see cref="CountdownWindow"/> в потоке UI и превращает его итог в задачу.</summary>
/// <param name="post">Ставит действие в очередь потока UI.</param>
internal sealed class WpfCountdownPrompt(Action<Action> post) : ICountdownPrompt
{
    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, int seconds, CancellationToken cancellationToken)
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        post(() =>
        {
            var window = new CountdownWindow(title, message, seconds);
            window.Completed += confirmed => result.TrySetResult(confirmed);

            // Отмена другим способом (меню трея, замена таймера): окно закрывается, ожидающий получает отмену.
            var registration = cancellationToken.Register(() => post(() =>
            {
                _ = result.TrySetCanceled(cancellationToken);
                window.Close();
            }));
            window.Closed += (_, _) => registration.Dispose();
            window.Show();
        });
        return result.Task;
    }
}
