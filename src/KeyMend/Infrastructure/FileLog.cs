using System.Globalization;
using System.IO;

namespace KeyMend.Infrastructure;

/// <summary>
/// Текстовый лог с ежедневной сменой файла и хранением нескольких дней. Время берётся из <see cref="TimeProvider"/>,
/// поэтому тесты подменяют часы. Логирование никогда не бросает исключений ввода-вывода. Пока настройка «Вести журнал работы»
/// не прочитана, записи копятся в памяти; выключенный журнал не пишет на диск ничего (SPEC 2.3).
/// </summary>
/// <param name="directory">Папка лога.</param>
/// <param name="timeProvider">Источник времени.</param>
/// <param name="keepDays">Сколько дней хранить файлы.</param>
internal sealed class FileLog(string directory, TimeProvider timeProvider, int keepDays = 7) : ILog
{
    /// <summary>Префикс имени файла лога.</summary>
    internal const string FilePrefix = "log-";

    /// <summary>Расширение файла лога.</summary>
    internal const string FileExtension = ".txt";

    /// <summary>Сколько записей помнить, пока настройка «Вести журнал работы» ещё не прочитана.</summary>
    internal const int MaxPending = 200;

    /// <summary>Состояние журнала (SPEC 2.3).</summary>
    private enum State
    {
        /// <summary>Настройка ещё не прочитана: записи копятся в памяти, на диск ничего не пишется.</summary>
        Pending,

        /// <summary>Журнал ведётся.</summary>
        On,

        /// <summary>Журнал выключен: на диск не пишется ничего, даже ошибки.</summary>
        Off,
    }

    /// <summary>Блокировка записи: лог пишут из разных потоков.</summary>
    private readonly Lock _lock = new();

    /// <summary>Записи, накопленные до чтения настройки.</summary>
    private readonly List<(DateTime Time, string Line)> _pending = [];

    /// <summary>День, в который в последний раз чистили старые файлы, чтобы делать это раз в сутки.</summary>
    private DateOnly? _purgedOn;

    /// <summary>Состояние журнала.</summary>
    private State _state = State.Pending;

    /// <summary>
    /// Включает или выключает журнал по настройке. Включение сбрасывает накопленное в файл; выключение выбрасывает накопленное,
    /// больше ничего не пишет и удаляет файлы журнала (SPEC 2.3).
    /// </summary>
    /// <param name="enabled"><c>true</c> — вести журнал.</param>
    public void SetEnabled(bool enabled)
    {
        lock (_lock)
        {
            if (enabled)
            {
                _state = State.On;
                foreach (var (time, line) in _pending)
                {
                    Append(time, line);
                }
            }
            else
            {
                _state = State.Off;
                DeleteAllFiles();
            }

            _pending.Clear();
        }
    }

    /// <inheritdoc />
    public void Info(string message) => Write("INFO", message);

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    /// <summary>Добавляет строку в файл за текущий день или копит её до чтения настройки. Выключенный журнал не пишет ничего.</summary>
    /// <param name="level">Уровень записи.</param>
    /// <param name="message">Текст записи.</param>
    private void Write(string level, string message)
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var line = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
        lock (_lock)
        {
            switch (_state)
            {
                case State.On:
                    Append(now, line);
                    break;
                case State.Pending when _pending.Count < MaxPending:
                    _pending.Add((now, line));
                    break;
            }
        }
    }

    /// <summary>Дописывает строку в файл за день записи и при необходимости удаляет старые файлы. Ошибки ввода-вывода глотает.</summary>
    /// <param name="time">Время записи.</param>
    /// <param name="line">Готовая строка с переводом строки.</param>
    private void Append(DateTime time, string line)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, FilePrefix + time.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + FileExtension), line);
            PurgeOldFiles(DateOnly.FromDateTime(time));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Лог не должен ломать приложение.
        }
    }

    /// <summary>Удаляет все файлы журнала; занятый или недоступный файл пропускается.</summary>
    private void DeleteAllFiles()
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory, FilePrefix + "*" + FileExtension))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Журнал выключен: не получилось удалить — остаётся до следующего раза, ломать из-за этого нечего.
        }

        _purgedOn = null;
    }

    /// <summary>Удаляет файлы лога старше срока хранения; выполняется не чаще раза в сутки.</summary>
    /// <param name="today">Сегодняшний день.</param>
    private void PurgeOldFiles(DateOnly today)
    {
        if (_purgedOn == today)
        {
            return;
        }

        _purgedOn = today;
        var oldest = today.AddDays(1 - keepDays);
        foreach (var file in Directory.EnumerateFiles(directory, FilePrefix + "*" + FileExtension))
        {
            var stamp = Path.GetFileNameWithoutExtension(file)[FilePrefix.Length..];
            if (DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && date < oldest)
            {
                File.Delete(file);
            }
        }
    }
}
