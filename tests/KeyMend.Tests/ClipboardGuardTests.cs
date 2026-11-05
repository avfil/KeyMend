using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="ClipboardGuard"/>: порядок Ctrl+C → замена → Ctrl+V → восстановление и отказы без порчи буфера.</summary>
public sealed class ClipboardGuardTests
{
    /// <summary>Буфер обмена-заглушка: хранит текст, номер изменения и журнал вызовов.</summary>
    private sealed class FakeClipboard(TestTimeProvider time) : IClipboard
    {
        /// <summary>Содержимое «прежнего» буфера пользователя.</summary>
        private static readonly ClipboardSnapshot _original = new([new ClipboardItem(13, [1, 2, 3])]);

        /// <summary>Текст в буфере сейчас.</summary>
        public string? Text { get; set; } = "old";

        /// <summary>Номер изменения.</summary>
        public uint Sequence { get; set; } = 1;

        /// <summary>Что вернёт <see cref="SaveAsync"/>; <c>null</c> — буфер недоступен.</summary>
        public ClipboardSnapshot? SaveResult { get; set; } = _original;

        /// <summary>В буфере нет текста (например, скопирована картинка).</summary>
        public bool HasNoText { get; set; }

        /// <summary>Получится ли положить текст.</summary>
        public bool SetSucceeds { get; set; } = true;

        /// <summary>Что делали с буфером, по порядку.</summary>
        public List<string> Events { get; } = [];

        /// <summary>Моменты восстановления буфера.</summary>
        public List<DateTimeOffset> RestoredAt { get; } = [];

        /// <summary>Какую копию восстановили.</summary>
        public List<ClipboardSnapshot> Restored { get; } = [];

        /// <inheritdoc />
        public uint SequenceNumber => Sequence;

        /// <inheritdoc />
        public Task<ClipboardSnapshot?> SaveAsync(CancellationToken cancellationToken)
        {
            Events.Add("save");
            return Task.FromResult(SaveResult);
        }

        /// <inheritdoc />
        public Task<string?> GetTextAsync(CancellationToken cancellationToken)
        {
            Events.Add("get");
            return Task.FromResult(HasNoText ? null : Text);
        }

        /// <inheritdoc />
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken)
        {
            Events.Add("set");
            if (SetSucceeds)
            {
                Text = text;
                Sequence++;
            }

            return Task.FromResult(SetSucceeds);
        }

        /// <inheritdoc />
        public Task<bool> RestoreAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken)
        {
            Events.Add("restore");
            RestoredAt.Add(time.GetUtcNow());
            Restored.Add(snapshot);
            return Task.FromResult(true);
        }
    }

    /// <summary>Отправка ввода-заглушка: Ctrl+C «копирует выделенное» в буфер-заглушку.</summary>
    private sealed class FakeSender(FakeClipboard clipboard, TestTimeProvider time) : IInputSender
    {
        /// <summary>Выделенный в окне текст; <c>null</c> — ничего не выделено, Ctrl+C буфер не меняет.</summary>
        public string? Selection { get; set; }

        /// <summary>Принимает ли система ввод.</summary>
        public bool Accepts { get; set; } = true;

        /// <summary>Моменты нажатия Ctrl+V.</summary>
        public List<DateTimeOffset> PastedAt { get; } = [];

        /// <summary>Нажатые сочетания.</summary>
        public List<(VirtualKey Modifier, VirtualKey Key)> Chords { get; } = [];

        /// <inheritdoc />
        public void SendMask(VirtualKey release)
        {
        }

        /// <inheritdoc />
        public bool SendTyped(int backspaces, string text, NewlineMode newline) => true;

        /// <inheritdoc />
        public bool SendKeys(KeyChord chord) => true;

        /// <inheritdoc />
        public bool SendChord(VirtualKey modifier, VirtualKey key)
        {
            Chords.Add((modifier, key));
            clipboard.Events.Add(key == VirtualKey.C ? "copy" : "paste");
            if (key == VirtualKey.C && Selection is not null)
            {
                clipboard.Text = Selection;
                clipboard.Sequence++;
            }

            if (key == VirtualKey.V)
            {
                PastedAt.Add(time.GetUtcNow());
            }

            return Accepts;
        }

        /// <inheritdoc />
        public bool SendKeyRepeat(VirtualKey key, int count, bool shift) => true;
    }

    /// <summary>Ручные часы.</summary>
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Буфер обмена под тестом.</summary>
    private readonly FakeClipboard _clipboard;

    /// <summary>Отправка ввода под тестом.</summary>
    private readonly FakeSender _sender;

    /// <summary>Защитник буфера под тестом.</summary>
    private readonly ClipboardGuard _guard;

    /// <summary>Собирает защитник на заглушках.</summary>
    public ClipboardGuardTests()
    {
        _clipboard = new FakeClipboard(_time);
        _sender = new FakeSender(_clipboard, _time);
        _guard = new ClipboardGuard(_clipboard, _sender, _time, _log);
    }

    /// <summary>Двигает ручные часы, пока задача не завершится.</summary>
    private async Task<ReplaceResult> DriveAsync(Task<ReplaceResult> task)
    {
        while (!task.IsCompleted)
        {
            if (_time.PendingTimerCount > 0)
            {
                _time.Advance(TimeSpan.FromMilliseconds(10));
            }
            else
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>Заменяет выделенное: порядок действий, вставленный текст и возврат прежнего буфера.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_Selected_CopiesReplacesPastesAndRestores()
    {
        _sender.Selection = "ghbdtn";
        string? seen = null;

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(
            text =>
            {
                seen = text;
                return "привет";
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.Replaced, ok);
        Assert.Equal("ghbdtn", seen);
        Assert.Equal(["save", "copy", "get", "set", "paste", "restore"], _clipboard.Events);
        Assert.Equal([(VirtualKey.LControl, VirtualKey.C), (VirtualKey.LControl, VirtualKey.V)], _sender.Chords);
        Assert.Equal("привет", _clipboard.Text);
        Assert.Same(_clipboard.SaveResult, Assert.Single(_clipboard.Restored));
    }

    /// <summary>Вставка текста: сохранить буфер, положить текст, Ctrl+V, вернуть прежний буфер.</summary>
    [Fact]
    public async Task PasteTextAsync_Text_SavesSetsPastesAndRestores()
    {
        var task = _guard.PasteTextAsync("test-secret-1", TestContext.Current.CancellationToken);
        while (!task.IsCompleted)
        {
            if (_time.PendingTimerCount > 0)
            {
                _time.Advance(TimeSpan.FromMilliseconds(10));
            }
            else
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        Assert.True(await task);
        Assert.Equal(["save", "set", "paste", "restore"], _clipboard.Events);
        Assert.Equal("test-secret-1", _clipboard.Text);
        Assert.Same(_clipboard.SaveResult, Assert.Single(_clipboard.Restored));
        Assert.DoesNotContain(_log.Entries, e => e.Contains("test-secret", StringComparison.Ordinal));
    }

    /// <summary>Если система не приняла Ctrl+V, прежний буфер всё равно возвращается.</summary>
    [Fact]
    public async Task PasteTextAsync_SendRejected_ReturnsFalseAndRestores()
    {
        _sender.Accepts = false;

        var ok = await _guard.PasteTextAsync("abc", TestContext.Current.CancellationToken);

        Assert.False(ok);
        Assert.Single(_clipboard.Restored);
    }

    /// <summary>Прежний буфер возвращается не раньше чем через 300 мс после Ctrl+V.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_Restores_AfterPasteDelay()
    {
        _sender.Selection = "abc";

        await DriveAsync(_guard.ReplaceSelectionAsync(_ => "def", TestContext.Current.CancellationToken));

        Assert.True(_clipboard.RestoredAt[0] - _sender.PastedAt[0] >= ClipboardGuard.PasteDelay);
    }

    /// <summary>Ничего не выделено: буфер не менялся, поэтому его не трогаем и не восстанавливаем.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_NothingSelected_LeavesClipboardAlone()
    {
        _sender.Selection = null;

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(_ => "x", TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.NothingSelected, ok);
        Assert.Equal(["save", "copy"], _clipboard.Events);
        Assert.Equal("old", _clipboard.Text);
        Assert.Contains("INFO selection skipped: nothing selected", _log.Entries);
    }

    /// <summary>Текст не подошёл для исправления: ничего не вставляем, буфер возвращаем.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_NotConvertible_RestoresWithoutPaste()
    {
        _sender.Selection = "123";

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(_ => null, TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.Rejected, ok);
        Assert.Equal(["save", "copy", "get", "restore"], _clipboard.Events);
    }

    /// <summary>В выделенном не оказалось текста (например, картинка): буфер возвращаем.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_NoText_RestoresWithoutPaste()
    {
        _sender.Selection = "x";
        _clipboard.HasNoText = true;
        var calls = 0;

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(
            _ =>
            {
                calls++;
                return "y";
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.Rejected, ok);
        Assert.Equal(0, calls);
        Assert.Contains("restore", _clipboard.Events);
        Assert.DoesNotContain("paste", _clipboard.Events);
    }

    /// <summary>Буфер недоступен: Ctrl+C даже не отправляем.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_ClipboardUnavailable_DoesNothing()
    {
        _clipboard.SaveResult = null;

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(_ => "x", TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.Failed, ok);
        Assert.Empty(_sender.Chords);
    }

    /// <summary>Не удалось положить текст: Ctrl+V не отправляем, буфер возвращаем.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_SetFails_RestoresWithoutPaste()
    {
        _sender.Selection = "abc";
        _clipboard.SetSucceeds = false;

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(_ => "def", TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.Failed, ok);
        Assert.DoesNotContain("paste", _clipboard.Events);
        Assert.Contains("restore", _clipboard.Events);
    }

    /// <summary>Система не приняла Ctrl+C: буфер не трогали, восстанавливать нечего.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_CopyChordRejected_DoesNothing()
    {
        _sender.Accepts = false;

        var ok = await DriveAsync(_guard.ReplaceSelectionAsync(_ => "x", TestContext.Current.CancellationToken));

        Assert.Equal(ReplaceResult.Failed, ok);
        Assert.DoesNotContain("restore", _clipboard.Events);
    }

    /// <summary>Отмена во время ожидания результата Ctrl+C возвращает прежний буфер: ввод мог уже изменить его.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_CancelledWhileWaitingForCopy_RestoresClipboard()
    {
        _sender.Selection = null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var task = _guard.ReplaceSelectionAsync(_ => "x", cts.Token);

        while (_sender.Chords.Count == 0)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("restore", _clipboard.Events);
    }

    /// <summary>Отмена посреди паузы после вставки всё равно возвращает прежний буфер.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_CancelledDuringPasteDelay_StillRestores()
    {
        _sender.Selection = "abc";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var task = _guard.ReplaceSelectionAsync(_ => "def", cts.Token);

        while (_sender.PastedAt.Count == 0)
        {
            if (_time.PendingTimerCount > 0)
            {
                _time.Advance(TimeSpan.FromMilliseconds(10));
            }
            else
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("restore", _clipboard.Events);
    }

    /// <summary>В лог не попадает содержимое буфера.</summary>
    [Fact]
    public async Task ReplaceSelectionAsync_Logs_DoNotContainText()
    {
        _sender.Selection = "test-secret-1";

        await DriveAsync(_guard.ReplaceSelectionAsync(_ => null, TestContext.Current.CancellationToken));

        Assert.DoesNotContain(_log.Entries, e => e.Contains("test-secret-1", StringComparison.Ordinal));
    }
}
