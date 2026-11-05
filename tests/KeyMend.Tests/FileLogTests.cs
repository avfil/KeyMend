using KeyMend.Infrastructure;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="FileLog"/>: логи пишутся во временную папку, время подменяется.</summary>
public sealed class FileLogTests : IDisposable
{
    /// <summary>Временная папка лога, уникальная для каждого теста.</summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KeyMendTests_" + Guid.NewGuid().ToString("N"));

    /// <summary>Удаляет временную папку.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>Создаёт часы на заданную дату.</summary>
    private static TestTimeProvider At(int year, int month, int day) => new(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Информационное событие попадает в файл за текущий день.</summary>
    [Fact]
    public void Info_Called_WritesLineToDailyFile()
    {
        var log = Enabled(new FileLog(_dir, At(2026, 10, 4)));

        log.Info("converted 5 chars");

        var text = File.ReadAllText(Path.Combine(_dir, "log-20261004.txt"));
        Assert.Contains("INFO converted 5 chars", text, StringComparison.Ordinal);
    }

    /// <summary>Исключение добавляется к сообщению об ошибке.</summary>
    [Fact]
    public void Error_WithException_IncludesException()
    {
        var log = Enabled(new FileLog(_dir, At(2026, 10, 4)));

        log.Error("boom", new InvalidOperationException("bad state"));

        var text = File.ReadAllText(Path.Combine(_dir, "log-20261004.txt"));
        Assert.Contains("ERROR boom", text, StringComparison.Ordinal);
        Assert.Contains("bad state", text, StringComparison.Ordinal);
    }

    /// <summary>Файлы старше срока хранения (7 дней) удаляются при записи, свежие остаются.</summary>
    [Fact]
    public void Write_OldFilesExist_DeletesFilesOlderThanKeepDays()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "log-20260920.txt"), "old");
        File.WriteAllText(Path.Combine(_dir, "log-20261001.txt"), "recent");
        var log = Enabled(new FileLog(_dir, At(2026, 10, 4), keepDays: 7));

        log.Info("x");

        Assert.False(File.Exists(Path.Combine(_dir, "log-20260920.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "log-20261001.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "log-20261004.txt")));
    }

    /// <summary>После смены суток запись идёт в новый файл.</summary>
    [Fact]
    public void Info_DayChanges_WritesToNewFile()
    {
        var time = At(2026, 10, 4);
        var log = Enabled(new FileLog(_dir, time));

        log.Info("first");
        time.Advance(TimeSpan.FromDays(1));
        log.Info("second");

        Assert.True(File.Exists(Path.Combine(_dir, "log-20261004.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "log-20261005.txt")));
    }

    /// <summary>Включает журнал, как это делает приложение после чтения настроек.</summary>
    private static FileLog Enabled(FileLog log)
    {
        log.SetEnabled(true);
        return log;
    }

    /// <summary>Пока настройка не прочитана, на диск не пишется ничего.</summary>
    [Fact]
    public void Info_BeforeSetEnabled_WritesNothing()
    {
        var log = new FileLog(_dir, At(2026, 10, 4));

        log.Info("early");

        Assert.False(Directory.Exists(_dir));
    }

    /// <summary>Включение сбрасывает накопленное на диск в файл дня записи.</summary>
    [Fact]
    public void SetEnabled_True_FlushesPendingLines()
    {
        var time = At(2026, 10, 4);
        var log = new FileLog(_dir, time);
        log.Info("early");
        time.Advance(TimeSpan.FromDays(1));

        log.SetEnabled(true);
        log.Info("late");

        Assert.Contains("early", File.ReadAllText(Path.Combine(_dir, "log-20261004.txt")), StringComparison.Ordinal);
        Assert.Contains("late", File.ReadAllText(Path.Combine(_dir, "log-20261005.txt")), StringComparison.Ordinal);
    }

    /// <summary>Выключенный журнал не пишет ничего, даже ошибки, а накопленное выбрасывает.</summary>
    [Fact]
    public void SetEnabled_False_WritesNothingNotEvenErrors()
    {
        var log = new FileLog(_dir, At(2026, 10, 4));
        log.Info("early");

        log.SetEnabled(false);
        log.Error("boom", new InvalidOperationException("bad"));
        log.Info("later");

        Assert.False(Directory.Exists(_dir));
    }

    /// <summary>Выключение удаляет файлы журнала, а повторное включение снова пишет.</summary>
    [Fact]
    public void SetEnabled_FalseThenTrue_DeletesFilesThenWritesAgain()
    {
        var log = Enabled(new FileLog(_dir, At(2026, 10, 4)));
        log.Info("one");
        File.WriteAllText(Path.Combine(_dir, "log-20261001.txt"), "old");
        File.WriteAllText(Path.Combine(_dir, "other.txt"), "keep");

        log.SetEnabled(false);

        Assert.Empty(Directory.GetFiles(_dir, "log-*.txt"));
        Assert.True(File.Exists(Path.Combine(_dir, "other.txt")));

        log.SetEnabled(true);
        log.Info("two");

        Assert.Contains("two", File.ReadAllText(Path.Combine(_dir, "log-20261004.txt")), StringComparison.Ordinal);
    }

    /// <summary>Накопление до чтения настройки ограничено: лишнее отбрасывается.</summary>
    [Fact]
    public void Info_ManyBeforeSetEnabled_KeepsOnlyFirstMaxPending()
    {
        var log = new FileLog(_dir, At(2026, 10, 4));
        for (var i = 0; i < FileLog.MaxPending + 50; i++)
        {
            log.Info("line " + i);
        }

        log.SetEnabled(true);

        Assert.Equal(FileLog.MaxPending, File.ReadAllLines(Path.Combine(_dir, "log-20261004.txt")).Length);
    }
}
