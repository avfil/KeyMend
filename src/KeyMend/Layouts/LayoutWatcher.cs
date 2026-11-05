using System.Threading.Channels;
using KeyMend.Infrastructure;
using KeyMend.Storage;
using KeyMend.UI;

namespace KeyMend.Layouts;

/// <summary>
/// Показывает подсказку при любой смене раскладки (SPEC F2.2, В3). Свою смену сообщает исполнитель шагов
/// (<see cref="OnOwnSwitchAsync"/>); смену извне хук замечает косвенно: после отпускания модификатора или клика он просит
/// сверить раскладку (<see cref="RequestCheck"/>), и через короткую паузу окно переднего плана читается заново.
/// Опроса по таймеру нет: в простое нагрузки на процессор нет.
/// </summary>
internal sealed class LayoutWatcher : ILayoutNotifier, IDisposable
{
    /// <summary>Пауза перед сверкой: система успевает применить смену раскладки.</summary>
    internal static readonly TimeSpan DefaultCheckDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>Запросы на сверку; ёмкость один, лишние запросы сливаются.</summary>
    private readonly Channel<bool> _requests =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>Отмена при завершении программы.</summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Сервис раскладок.</summary>
    private readonly ILayoutService _layouts;

    /// <summary>Показ подсказки.</summary>
    private readonly IIndicatorPresenter _indicator;

    /// <summary>Текущие настройки подсказки.</summary>
    private readonly Func<IndicatorSettings> _getSettings;

    /// <summary>Источник времени.</summary>
    private readonly TimeProvider _time;

    /// <summary>Пауза перед сверкой.</summary>
    private readonly TimeSpan _checkDelay;

    /// <summary>Журнал.</summary>
    private readonly ILog _log;

    /// <summary>Логика обнаружения смены; трогается только из потока функций и цикла сверки под блокировкой.</summary>
    private readonly LayoutChangeDetector _detector = new();

    /// <summary>Блокировка детектора: к нему обращаются исполнитель шагов и цикл сверки.</summary>
    private readonly Lock _lock = new();

    /// <summary>Запускает цикл сверки.</summary>
    /// <param name="layouts">Сервис раскладок.</param>
    /// <param name="indicator">Показ подсказки.</param>
    /// <param name="getSettings">Текущие настройки подсказки.</param>
    /// <param name="time">Источник времени.</param>
    /// <param name="log">Журнал.</param>
    /// <param name="checkDelay">Пауза перед сверкой; по умолчанию <see cref="DefaultCheckDelay"/>.</param>
    public LayoutWatcher(
        ILayoutService layouts,
        IIndicatorPresenter indicator,
        Func<IndicatorSettings> getSettings,
        TimeProvider time,
        ILog log,
        TimeSpan? checkDelay = null)
    {
        _layouts = layouts;
        _indicator = indicator;
        _getSettings = getSettings;
        _time = time;
        _checkDelay = checkDelay ?? DefaultCheckDelay;
        _log = log;
        Completion = Task.Run(() => CheckLoopAsync(_cts.Token));
    }

    /// <summary>Завершается после остановки цикла сверки.</summary>
    public Task Completion { get; }

    /// <summary>Просит сверить раскладку окна переднего плана; можно вызывать из любого потока.</summary>
    public void RequestCheck() => _requests.Writer.TryWrite(true);

    /// <inheritdoc />
    public async Task OnOwnSwitchAsync(LayoutInfo layout, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _detector.NoteOwnSwitch(layout.Hkl);
        }

        await _indicator.ShowAsync(layout.Code, cancellationToken);
    }

    /// <summary>Останавливает цикл сверки; не ждёт его завершения.</summary>
    public void Dispose()
    {
        _requests.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>Ждёт запросы, выдерживает паузу и сверяет раскладку.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task CheckLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in _requests.Reader.ReadAllAsync(cancellationToken))
            {
                await Task.Delay(_checkDelay, _time, cancellationToken);
                try
                {
                    await CheckAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.Error("layout check failed", ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Программа завершается.
        }
    }

    /// <summary>Читает раскладку окна переднего плана и показывает подсказку, если её сменили извне.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        if (_layouts.Snapshot() is not { } snapshot)
        {
            return;
        }

        bool changed;
        lock (_lock)
        {
            changed = _detector.Observe(snapshot.Window, snapshot.Layout.Hkl);
        }

        if (changed && !_getSettings().OnlyOwnSwitch)
        {
            _log.Info($"layout changed externally to {snapshot.Layout.Code}");
            await _indicator.ShowAsync(snapshot.Layout.Code, cancellationToken);
        }
    }
}
