using System.Globalization;
using System.IO;
using System.Threading.Channels;
using KeyMend.Infrastructure;
using KeyMend.Resources;
using KeyMend.UI;

namespace KeyMend.Storage;

/// <summary>
/// Следит за <c>settings.json</c> и применяет правки без перезапуска (SPEC F11, этап 1: до окна настроек файл правят руками).
/// События файловой системы сливаются и выдерживаются <see cref="DefaultDebounce"/>: редакторы пишут файл в несколько приёмов.
/// Ошибочный файл не применяется: прежние настройки остаются, приходит уведомление с описанием ошибки.
/// </summary>
internal sealed class SettingsWatcher : IDisposable
{
    /// <summary>Пауза после последнего события перед чтением файла.</summary>
    internal static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>Сколько раз пробовать прочитать файл, занятый редактором.</summary>
    internal const int ReadAttempts = 3;

    /// <summary>Запросы на перечитку; ёмкость один, лишние запросы сливаются.</summary>
    private readonly Channel<bool> _requests =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>Отмена при завершении программы.</summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Читает и проверяет файл.</summary>
    private readonly Func<SettingsParseResult> _read;

    /// <summary>Применяет новые настройки.</summary>
    private readonly Action<AppSettings> _apply;

    /// <summary>Показывает уведомления.</summary>
    private readonly INotifier _notifier;

    /// <summary>Источник времени.</summary>
    private readonly TimeProvider _time;

    /// <summary>Журнал.</summary>
    private readonly ILog _log;

    /// <summary>Пауза после последнего события.</summary>
    private readonly TimeSpan _debounce;

    /// <summary>Слежение за файлом; <c>null</c>, если не запрошено (тесты).</summary>
    private readonly FileSystemWatcher? _fileWatcher;

    /// <summary>Запускает цикл перечитки и, если указан файл, слежение за ним.</summary>
    /// <param name="read">Читает и проверяет файл (обычно <see cref="SettingsStore.TryRead"/>).</param>
    /// <param name="apply">Применяет настройки; вызывается из потока пула.</param>
    /// <param name="notifier">Показывает ошибки пользователю.</param>
    /// <param name="time">Источник времени.</param>
    /// <param name="log">Журнал.</param>
    /// <param name="watchedFile">Полный путь к файлу настроек или <c>null</c>, чтобы не следить за файлом.</param>
    /// <param name="debounce">Пауза после последнего события; по умолчанию <see cref="DefaultDebounce"/>.</param>
    public SettingsWatcher(
        Func<SettingsParseResult> read,
        Action<AppSettings> apply,
        INotifier notifier,
        TimeProvider time,
        ILog log,
        string? watchedFile = null,
        TimeSpan? debounce = null)
    {
        _read = read;
        _apply = apply;
        _notifier = notifier;
        _time = time;
        _log = log;
        _debounce = debounce ?? DefaultDebounce;
        Completion = Task.Run(() => ReloadLoopAsync(_cts.Token));

        if (watchedFile is not null && Path.GetDirectoryName(watchedFile) is { Length: > 0 } directory)
        {
            _fileWatcher = new FileSystemWatcher(directory, Path.GetFileName(watchedFile))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            _fileWatcher.Changed += (_, _) => RequestReload();
            _fileWatcher.Created += (_, _) => RequestReload();
            _fileWatcher.Renamed += (_, _) => RequestReload();
            _fileWatcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>Завершается после остановки цикла перечитки.</summary>
    public Task Completion { get; }

    /// <summary>Просит перечитать файл; можно вызывать из любого потока.</summary>
    public void RequestReload() => _requests.Writer.TryWrite(true);

    /// <summary>Останавливает слежение и цикл перечитки; не ждёт его завершения.</summary>
    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _requests.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>Ждёт запросы, выдерживает паузу и перечитывает файл.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task ReloadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in _requests.Reader.ReadAllAsync(cancellationToken))
            {
                await Task.Delay(_debounce, _time, cancellationToken);
                try
                {
                    await ReloadAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.Error("settings reload failed", ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Программа завершается.
        }
    }

    /// <summary>Читает файл (с повтором, если он занят) и применяет либо сообщает об ошибке.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var result = _read();
        for (var attempt = 1; attempt < ReadAttempts && result.Error == SettingsStore.ReadFailedMessage; attempt++)
        {
            await Task.Delay(_debounce, _time, cancellationToken);
            result = _read();
        }

        if (result.Settings is { } settings)
        {
            _apply(settings);
            _log.Info($"settings reloaded: {settings.Rules.Count} rules");
            if (result.Warnings.Count > 0)
            {
                _notifier.Notify(Strings.AppName, Strings.Fill(Strings.SettingsSkipped, result.Warnings.Count.ToString(CultureInfo.CurrentCulture)), NotifyLevel.Warning);
            }

            return;
        }

        _log.Error($"settings reload rejected: {result.Error}");
        _notifier.Notify(Strings.AppName, Strings.Fill(Strings.SettingsRejected, result.Error), NotifyLevel.Error);
    }
}
