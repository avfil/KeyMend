using KeyMend.Infrastructure;

namespace KeyMend.Tests;

/// <summary>Журнал для тестов: запоминает записи в памяти.</summary>
internal sealed class TestLog : ILog
{
    /// <summary>Записанные сообщения.</summary>
    private readonly List<string> _entries = [];

    /// <summary>Записанные сообщения в порядке появления.</summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <inheritdoc />
    public void Info(string message) => _entries.Add("INFO " + message);

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null) => _entries.Add("ERROR " + message);
}
