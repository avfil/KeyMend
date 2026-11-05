using KeyMend.Infrastructure;
using KeyMend.Storage;

namespace KeyMend.UI;

/// <summary>
/// Связывает поток функций и окно подсказки: ищет точку привязки (в потоке функций, это может быть медленно)
/// и показывает окно в потоке UI. Настройки читает при каждом показе, поэтому изменения применяются сразу.
/// </summary>
/// <param name="getSettings">Текущие настройки подсказки.</param>
/// <param name="indicator">Окно подсказки; принадлежит потоку UI.</param>
/// <param name="postToUi">Ставит действие в очередь потока UI.</param>
/// <param name="log">Журнал.</param>
internal sealed class IndicatorPresenter(
    Func<IndicatorSettings> getSettings, LayoutIndicator indicator, Action<Action> postToUi, ILog log) : IIndicatorPresenter
{
    /// <inheritdoc />
    public async Task ShowAsync(string code, CancellationToken cancellationToken)
    {
        var settings = getSettings();
        if (!settings.Enabled)
        {
            return;
        }

        var anchor = await CaretLocator.LocateAsync(settings.Position, cancellationToken);
        if (anchor is not { } found)
        {
            return;
        }

        log.Info($"indicator anchor {found.Source}{(found.Obscured is null ? string.Empty : " shell-overlay")}");
        postToUi(() => indicator.ShowAt(code, found, settings.DurationMs));
    }
}
