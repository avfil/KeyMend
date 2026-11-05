using System.Diagnostics.CodeAnalysis;
using KeyMend.Actions;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="InputStepRunner"/>: стирание набранного, печать и вставка через буфер, секреты, ожидание модификаторов.</summary>
public sealed class InputStepRunnerTests
{
    /// <summary>Отправка ввода-заглушка: записывает вызовы.</summary>
    private sealed class FakeSender : IInputSender
    {
        /// <summary>Что отправляли, по порядку.</summary>
        public List<string> Calls { get; } = [];

        /// <summary>Принимает ли система ввод.</summary>
        public bool Accepts { get; set; } = true;

        /// <inheritdoc />
        public void SendMask(VirtualKey release)
        {
        }

        /// <inheritdoc />
        public bool SendTyped(int backspaces, string text, NewlineMode newline)
        {
            Calls.Add($"typed {backspaces} '{text}' {newline}");
            return Accepts;
        }

        /// <inheritdoc />
        public bool SendKeys(KeyChord chord)
        {
            Calls.Add($"keys {chord.Modifiers}+{chord.Key}");
            return Accepts;
        }

        /// <inheritdoc />
        public bool SendChord(VirtualKey modifier, VirtualKey key) => Accepts;

        /// <inheritdoc />
        public bool SendKeyRepeat(VirtualKey key, int count, bool shift)
        {
            Calls.Add($"repeat {key} {count}");
            return Accepts;
        }
    }

    /// <summary>Вставка через буфер-заглушка.</summary>
    private sealed class FakeGuard(List<string> calls) : IClipboardGuard
    {
        /// <summary>Получилась ли вставка.</summary>
        public bool Succeeds { get; set; } = true;

        /// <inheritdoc />
        public Task<bool> PasteTextAsync(string text, CancellationToken cancellationToken)
        {
            calls.Add($"paste '{text}'");
            return Task.FromResult(Succeeds);
        }

        /// <inheritdoc />
        public Task<ReplaceResult> ReplaceSelectionAsync(Func<string, string?> convert, CancellationToken cancellationToken) =>
            Task.FromResult(ReplaceResult.NothingSelected);
    }

    /// <summary>Хранилище секретов-заглушка.</summary>
    private sealed class FakeSecrets : ISecretStore
    {
        /// <summary>Секреты по ключам.</summary>
        public Dictionary<string, string> Items { get; } = [];

        /// <inheritdoc />
        public bool TryGet(string key, [NotNullWhen(true)] out string? text) => Items.TryGetValue(key, out text);

        /// <inheritdoc />
        public bool SetMany(IReadOnlyDictionary<string, string> secrets) => true;

        /// <inheritdoc />
        public bool Remove(IReadOnlyCollection<string> keys) => true;
    }

    /// <summary>Ожидание модификаторов-заглушка.</summary>
    private sealed class FakeWaiter : IModifierWaiter
    {
        /// <summary>Отпущены ли модификаторы вовремя.</summary>
        public bool Released { get; set; } = true;

        /// <summary>Значения <c>ignoreShift</c> в вызовах.</summary>
        public List<bool> IgnoreShiftCalls { get; } = [];

        /// <inheritdoc />
        public Task<bool> WaitReleasedAsync(bool ignoreShift, CancellationToken cancellationToken)
        {
            IgnoreShiftCalls.Add(ignoreShift);
            return Task.FromResult(Released);
        }
    }

    /// <summary>Отправка под тестом.</summary>
    private readonly FakeSender _sender = new();

    /// <summary>Вставка через буфер под тестом.</summary>
    private readonly FakeGuard _guard;

    /// <summary>Секреты под тестом.</summary>
    private readonly FakeSecrets _secrets = new();

    /// <summary>Ожидание модификаторов под тестом.</summary>
    private readonly FakeWaiter _waiter = new();

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Правило для сообщений.</summary>
    private readonly Rule _rule = new() { Name = "snippet #1", Trigger = new PressTrigger { Key = VirtualKey.None } };

    /// <summary>Исполнитель под тестом.</summary>
    private readonly InputStepRunner _runner;

    /// <summary>Собирает исполнитель на заглушках.</summary>
    public InputStepRunnerTests()
    {
        _guard = new FakeGuard(_sender.Calls);
        _runner = new InputStepRunner(_sender, _guard, _secrets, _waiter, _log);
    }

    /// <summary>Печать: стирание и текст уходят одним вызовом с нужным разделителем строк.</summary>
    [Fact]
    public async Task InsertAsync_TypeMode_ErasesAndTypesInOneCall()
    {
        var step = new InsertTextStep { Text = "Добрый день!", Erase = 3, Newline = NewlineMode.Enter };

        await _runner.InsertAsync(_rule, step, TestContext.Current.CancellationToken);

        Assert.Equal(["typed 3 'Добрый день!' Enter"], _sender.Calls);
    }

    /// <summary>Через буфер: сначала стираются клавиши, затем вставляется текст.</summary>
    [Fact]
    public async Task InsertAsync_ClipboardMode_ErasesThenPastes()
    {
        var step = new InsertTextStep { Text = "текст", Erase = 2, Insert = InsertMode.Clipboard };

        await _runner.InsertAsync(_rule, step, TestContext.Current.CancellationToken);

        Assert.Equal(["repeat Back 2", "paste 'текст'"], _sender.Calls);
    }

    /// <summary>Секрет берётся из хранилища и вставляется только через буфер, даже если шаг просит печать.</summary>
    [Fact]
    public async Task InsertAsync_Secret_PastesFromStoreAndNeverLogsText()
    {
        _secrets.Items["k"] = "test-secret-1";
        var step = new InsertTextStep { SecretRef = "k", Erase = 4, Insert = InsertMode.Type };

        await _runner.InsertAsync(_rule, step, TestContext.Current.CancellationToken);

        Assert.Equal(["repeat Back 4", "paste 'test-secret-1'"], _sender.Calls);
        Assert.DoesNotContain(_log.Entries, e => e.Contains("test-secret", StringComparison.Ordinal));
    }

    /// <summary>Нет секрета в хранилище: шаг падает без ввода, в сообщении нет ключа.</summary>
    [Fact]
    public async Task InsertAsync_MissingSecret_ThrowsAndTypesNothing()
    {
        var step = new InsertTextStep { SecretRef = "test-secret-ref", Erase = 4 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _runner.InsertAsync(_rule, step, TestContext.Current.CancellationToken));

        Assert.Empty(_sender.Calls);
        Assert.DoesNotContain("test-secret", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Модификаторы не отпущены: ничего не стирается и не печатается, цепочка прерывается.</summary>
    [Fact]
    public async Task InsertAsync_ModifiersHeld_ThrowsAndSendsNothing()
    {
        _waiter.Released = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _runner.InsertAsync(_rule, new InsertTextStep { Text = "x", Erase = 1 }, TestContext.Current.CancellationToken));

        Assert.Empty(_sender.Calls);
    }

    /// <summary>Система не приняла ввод: шаг падает, чтобы шаги после вставки (Ctrl+Enter) не выполнялись.</summary>
    [Fact]
    public async Task InsertAsync_SendRejected_Throws()
    {
        _sender.Accepts = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _runner.InsertAsync(_rule, new InsertTextStep { Text = "x" }, TestContext.Current.CancellationToken));
    }

    /// <summary>Вставка через буфер не удалась: шаг падает.</summary>
    [Fact]
    public async Task InsertAsync_PasteFails_Throws()
    {
        _guard.Succeeds = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _runner.InsertAsync(
                _rule, new InsertTextStep { Text = "x", Insert = InsertMode.Clipboard }, TestContext.Current.CancellationToken));
    }

    /// <summary>Шаг «Нажать» отправляет сочетание после отпускания модификаторов.</summary>
    [Fact]
    public async Task PressAsync_Chord_SendsKeys()
    {
        Assert.True(KeyChordParser.TryParse("Ctrl+Enter", out var chord));

        await _runner.PressAsync(_rule, new PressStep { Keys = chord }, TestContext.Current.CancellationToken);

        Assert.Equal(["keys AnyCtrl+Return"], _sender.Calls);
    }

    /// <summary>Печать не ждёт Shift, вставка через буфер и «Нажать» — ждут.</summary>
    [Fact]
    public async Task Steps_ShiftHandling_TypeIgnoresClipboardAndPressWait()
    {
        await _runner.InsertAsync(_rule, new InsertTextStep { Text = "x" }, TestContext.Current.CancellationToken);
        await _runner.InsertAsync(
            _rule, new InsertTextStep { Text = "x", Insert = InsertMode.Clipboard }, TestContext.Current.CancellationToken);
        Assert.True(KeyChordParser.TryParse("Ctrl+Enter", out var chord));
        await _runner.PressAsync(_rule, new PressStep { Keys = chord }, TestContext.Current.CancellationToken);

        Assert.Equal([true, false, false], _waiter.IgnoreShiftCalls);
    }

    /// <summary>Сочетание Ctrl+Enter раскрывается в левый Ctrl, «любой» Shift — в левый Shift, сторонние клавиши остаются.</summary>
    [Fact]
    public void ExpandModifiers_AnyAndSided_MapsToKeys()
    {
        Assert.Equal([VirtualKey.LControl], InputSender.ExpandModifiers(ModifierSet.AnyCtrl));
        Assert.Equal(
            [VirtualKey.RControl, VirtualKey.LShift, VirtualKey.RMenu],
            InputSender.ExpandModifiers(ModifierSet.RCtrl | ModifierSet.AnyShift | ModifierSet.RAlt));
        Assert.Empty(InputSender.ExpandModifiers(ModifierSet.None));
    }
}
