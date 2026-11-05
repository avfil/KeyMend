using System.Threading.Channels;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Actions;

/// <summary>
/// Поток функций (SPEC 7.2): единственный читатель канала сообщений от хука. Медленную работу (смена раскладки,
/// <c>SendInput</c>, запуск программ) выполняет здесь, а не в колбэке хука. Цепочки правил идут по одной из очереди; правила
/// из одних служебных шагов (пауза, настройки) выполняются сразу, мимо очереди, чтобы паузу можно было снять во время
/// долгой цепочки.
/// </summary>
internal sealed class ChainRunner : IDisposable
{
    /// <summary>Канал сообщений от хука; без ограничения размера, чтобы <c>TryWrite</c> в колбэке никогда не блокировался.</summary>
    private readonly Channel<HookMessage> _input =
        Channel.CreateUnbounded<HookMessage>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Очередь цепочек, ожидающих выполнения.</summary>
    private readonly Channel<Rule> _chains =
        Channel.CreateUnbounded<Rule>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    /// <summary>Отмена при завершении программы.</summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Исполнитель шагов.</summary>
    private readonly IStepExecutor _executor;

    /// <summary>Отправка ввода (маска Alt/Win).</summary>
    private readonly IInputSender _sender;

    /// <summary>Просьба сверить раскладку (вызывается из потока функций; сама проверка отложена).</summary>
    private readonly Action _requestLayoutCheck;

    /// <summary>Куда передать результат записи триггера (SPEC 3.1, п. 5).</summary>
    private readonly Action<RecordedGesture> _triggerRecorded;

    /// <summary>Журнал.</summary>
    private readonly ILog _log;

    /// <summary>Сколько цепочек с исправлением ввода ждёт очереди или выполняется; пока больше нуля, новые такие нажатия отбрасываются.</summary>
    private int _pendingCorrections;

    /// <summary>Запускает циклы чтения сообщений и выполнения цепочек в пуле потоков.</summary>
    /// <param name="executor">Исполнитель шагов.</param>
    /// <param name="sender">Отправка ввода.</param>
    /// <param name="requestLayoutCheck">Что делать на просьбу хука сверить раскладку.</param>
    /// <param name="triggerRecorded">Что делать с результатом записи триггера в настройках.</param>
    /// <param name="log">Журнал.</param>
    public ChainRunner(
        IStepExecutor executor, IInputSender sender, Action requestLayoutCheck, Action<RecordedGesture> triggerRecorded, ILog log)
    {
        _executor = executor;
        _sender = sender;
        _requestLayoutCheck = requestLayoutCheck;
        _triggerRecorded = triggerRecorded;
        _log = log;
        Completion = Task.WhenAll(
            Task.Run(() => ReadLoopAsync(_cts.Token)),
            Task.Run(() => ChainLoopAsync(_cts.Token)));
    }

    /// <summary>Сторона записи канала: передаётся хуку.</summary>
    public ChannelWriter<HookMessage> Writer => _input.Writer;

    /// <summary>Завершается, когда канал закрыт и все принятые сообщения обработаны, либо после отмены.</summary>
    public Task Completion { get; }

    /// <summary>Закрывает канал и отменяет текущие шаги; не ждёт завершения циклов.</summary>
    public void Dispose()
    {
        _input.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>Читает сообщения по одному; ошибка в одном не останавливает остальные.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _input.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await HandleAsync(message, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.Error($"message {message.Kind} failed", ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Программа завершается: оставшиеся сообщения не нужны.
        }
        finally
        {
            _chains.Writer.TryComplete();
        }
    }

    /// <summary>Выполняет цепочки из очереди по одной.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task ChainLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chain in _chains.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await RunChainAsync(chain, cancellationToken);
                }
                finally
                {
                    if (IsCorrection(chain))
                    {
                        _ = Interlocked.Decrement(ref _pendingCorrections);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Программа завершается.
        }
    }

    /// <summary>Обрабатывает одно сообщение: сначала маска Alt/Win (если нужна), затем само действие.</summary>
    /// <param name="message">Сообщение от хука.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task HandleAsync(HookMessage message, CancellationToken cancellationToken)
    {
        if (message.SendMask)
        {
            _sender.SendMask(message.ReleaseKey);
        }

        switch (message.Kind)
        {
            case HookMessageKind.RuleFired when message.Rule is { } rule:
                if (IsServiceOnly(rule))
                {
                    await RunChainAsync(rule, cancellationToken);
                }
                else if (IsCorrection(rule) && Interlocked.CompareExchange(ref _pendingCorrections, 1, 0) != 0)
                {
                    // Исправление уже идёт или ждёт очереди. Текст может быть большим, а замена идёт долго: повторные нажатия не копим в очереди.
                    _log.Info("correction ignored: busy");
                }
                else
                {
                    // Для исправления счётчик уже поднят проверкой выше.
                    _chains.Writer.TryWrite(rule);
                }

                break;
            case HookMessageKind.LayoutCheck:
                _requestLayoutCheck();
                break;
            case HookMessageKind.TriggerRecorded when message.Gesture is { } gesture:
                _triggerRecorded(gesture);
                break;
            case HookMessageKind.HookError:
                _log.Error("hook callback failed");
                break;
        }
    }

    /// <summary>Выполняет шаги правила по порядку; ошибка шага прерывает эту цепочку, но не остальные.</summary>
    /// <param name="rule">Правило.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task RunChainAsync(Rule rule, CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var step in rule.Steps)
        {
            index++;
            try
            {
                await _executor.ExecuteAsync(rule, step, cancellationToken);
            }
            catch (StepCancelledException)
            {
                _log.Info($"rule '{rule.Name}' step {index} cancelled by the user, the chain is stopped");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Error($"rule '{rule.Name}' step {index} failed", ex);
                return;
            }
        }
    }

    /// <summary>Есть ли в правиле шаг исправления ввода.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns><c>true</c>, если шаг исправления есть.</returns>
    private static bool IsCorrection(Rule rule) => rule.Steps.Any(s => s is CorrectStep);

    /// <summary>Состоит ли правило только из служебных шагов.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns><c>true</c>, если шаги есть и все служебные.</returns>
    private static bool IsServiceOnly(Rule rule) => rule.Steps.Count > 0 && rule.Steps.All(s => s is ServiceStep);
}
