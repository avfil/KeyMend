using KeyMend.Actions;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>
/// Тесты <see cref="TextCorrector"/>: исправление выделенного и до начала строки, консоли, запасной путь через клавиши,
/// возврат каретки, повторное выделение, лимит длины, переключение языка и журнал без текста.
/// </summary>
public sealed class TextCorrectorTests
{
    /// <summary>Английская раскладка.</summary>
    private static readonly LayoutInfo _en = new(0x04090409, "EN");

    /// <summary>Русская раскладка.</summary>
    private static readonly LayoutInfo _ru = new(0x04190419, "RU");

    /// <summary>Сервис раскладок-заглушка: знает EN и RU; запоминает переключения.</summary>
    private sealed class FakeLayouts : ILayoutService
    {
        /// <summary>Установлена ли русская раскладка.</summary>
        public bool HasRussian { get; set; } = true;

        /// <summary>Текущая раскладка окна.</summary>
        public LayoutInfo? Current { get; set; } = _en;

        /// <summary>Языки, на которые просили переключиться.</summary>
        public List<int> Switched { get; } = [];

        /// <inheritdoc />
        public LayoutInfo? GetCurrent() => Current;

        /// <inheritdoc />
        public LayoutSnapshot? Snapshot() => null;

        /// <inheritdoc />
        public LayoutInfo? SwitchPair(int languageA, int languageB) => null;

        /// <inheritdoc />
        public LayoutInfo? SwitchTo(int languageId)
        {
            Switched.Add(languageId);
            return languageId == 0x0419 ? _ru : _en;
        }

        /// <inheritdoc />
        public LayoutPairResolution? ResolvePair(int languageA, int languageB) => null;

        /// <inheritdoc />
        public LayoutInfo? FindInstalled(int languageId) => languageId switch
        {
            0x0409 => _en,
            0x0419 when HasRussian => _ru,
            _ => null,
        };
    }

    /// <summary>Таблицы из запасных раскладок US и ЙЦУКЕН.</summary>
    private sealed class FallbackTables : IKeyTables
    {
        /// <inheritdoc />
        public LayoutTable GetTable(nint hkl) => FallbackLayouts.ForLanguage((int)(hkl & 0xFFFF)) ?? new LayoutTable();
    }

    /// <summary>Замена выделенного-заглушка: «выделенный» текст прогоняется через переданную функцию.</summary>
    private sealed class FakeGuard : IClipboardGuard
    {
        /// <summary>Выделенный текст; <c>null</c> — ничего не выделено.</summary>
        public string? Selection { get; set; }

        /// <summary>Сколько раз вызывали замену.</summary>
        public int Calls { get; private set; }

        /// <summary>Что получилось вставить.</summary>
        public string? Pasted { get; private set; }

        /// <inheritdoc />
        public Task<bool> PasteTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(true);

        /// <inheritdoc />
        public Task<ReplaceResult> ReplaceSelectionAsync(Func<string, string?> convert, CancellationToken cancellationToken)
        {
            Calls++;
            if (Selection is null)
            {
                return Task.FromResult(ReplaceResult.NothingSelected);
            }

            Pasted = convert(Selection);
            return Task.FromResult(Pasted is null ? ReplaceResult.Rejected : ReplaceResult.Replaced);
        }
    }

    /// <summary>Доступ к тексту окна-заглушка с настраиваемыми ответами.</summary>
    private sealed class FakeFocus : ITextFocus
    {
        /// <summary>Консоль ли на переднем плане.</summary>
        public bool Terminal { get; set; }

        /// <summary>Ответ про выделение.</summary>
        public SelectionState State { get; set; } = SelectionState.Selected;

        /// <summary>Ответ про выделение до начала строки.</summary>
        public LineSelectResult Line { get; set; } = LineSelectResult.Selected;

        /// <summary>Получилось ли выделить вставленный текст через UI Automation.</summary>
        public bool ReselectWorks { get; set; } = true;

        /// <summary>Тексты, которые просили выделить перед кареткой.</summary>
        public List<string> Reselected { get; } = [];

        /// <inheritdoc />
        public bool IsTerminalForeground() => Terminal;

        /// <inheritdoc />
        public Task<SelectionState> GetSelectionStateAsync(CancellationToken cancellationToken) => Task.FromResult(State);

        /// <inheritdoc />
        public Task<LineSelectResult> SelectToLineStartAsync(int maxChars, CancellationToken cancellationToken) => Task.FromResult(Line);

        /// <inheritdoc />
        public Task<bool> SelectBeforeCaretAsync(string expected, CancellationToken cancellationToken)
        {
            Reselected.Add(expected);
            return Task.FromResult(ReselectWorks);
        }
    }

    /// <summary>Отправка ввода-заглушка: запоминает серии клавиш.</summary>
    private sealed class FakeSender : IInputSender
    {
        /// <summary>Серии вида «Home×1 shift».</summary>
        public List<string> Repeats { get; } = [];

        /// <inheritdoc />
        public void SendMask(VirtualKey release)
        {
        }

        /// <inheritdoc />
        public bool SendTyped(int backspaces, string text, NewlineMode newline) => true;

        /// <inheritdoc />
        public bool SendKeys(KeyChord chord) => true;

        /// <inheritdoc />
        public bool SendChord(VirtualKey modifier, VirtualKey key) => true;

        /// <inheritdoc />
        public bool SendKeyRepeat(VirtualKey key, int count, bool shift)
        {
            Repeats.Add($"{key}x{count}{(shift ? " shift" : string.Empty)}");
            return true;
        }
    }

    /// <summary>Ожидание модификаторов-заглушка.</summary>
    private sealed class FakeWaiter : IModifierWaiter
    {
        /// <summary>Отпущены ли модификаторы вовремя.</summary>
        public bool Released { get; set; } = true;

        /// <inheritdoc />
        public Task<bool> WaitReleasedAsync(bool ignoreShift, CancellationToken cancellationToken) => Task.FromResult(Released);
    }

    /// <summary>Получатель сообщений о своей смене раскладки: запоминает коды.</summary>
    private sealed class FakeNotifier : ILayoutNotifier
    {
        /// <summary>Коды языков, о которых сообщили.</summary>
        public List<string> Notified { get; } = [];

        /// <inheritdoc />
        public Task OnOwnSwitchAsync(LayoutInfo layout, CancellationToken cancellationToken)
        {
            Notified.Add(layout.Code);
            return Task.CompletedTask;
        }
    }

    /// <summary>Раскладки под тестом.</summary>
    private readonly FakeLayouts _layouts = new();

    /// <summary>Замена выделенного под тестом.</summary>
    private readonly FakeGuard _guard = new();

    /// <summary>Доступ к тексту окна под тестом.</summary>
    private readonly FakeFocus _focus = new();

    /// <summary>Отправка ввода под тестом.</summary>
    private readonly FakeSender _sender = new();

    /// <summary>Ожидание модификаторов под тестом.</summary>
    private readonly FakeWaiter _waiter = new();

    /// <summary>Получатель сообщений о смене раскладки под тестом.</summary>
    private readonly FakeNotifier _notifier = new();

    /// <summary>Журнал теста.</summary>
    private readonly TestLog _log = new();

    /// <summary>Шаг исправления выделенного.</summary>
    private static CorrectStep SelectionStep(int maxChars = 10_000, bool switchLayout = true, CaretAfter caretAfter = CaretAfter.AsIs) =>
        new() { Scope = CorrectScope.Selection, MaxChars = maxChars, SwitchLayout = switchLayout, CaretAfter = caretAfter };

    /// <summary>Шаг исправления до начала строки.</summary>
    private static CorrectStep LineStep(int maxChars = 10_000, bool switchLayout = true) =>
        new() { Scope = CorrectScope.Line, MaxChars = maxChars, SwitchLayout = switchLayout };

    /// <summary>Выполняет шаг на исправлении под тестом.</summary>
    private Task Run(CorrectStep step) =>
        new TextCorrector(_layouts, new FallbackTables(), _guard, _focus, _sender, _waiter, _notifier, _log)
            .CorrectAsync(step, TestContext.Current.CancellationToken);

    /// <summary>Выделенное: русский текст в английской раскладке превращается в русский, язык переключается.</summary>
    [Fact]
    public async Task Selection_WrongLayoutText_IsConvertedAndLayoutSwitched()
    {
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.Equal("привет", _guard.Pasted);
        Assert.Equal([0x0419], _layouts.Switched);
        Assert.Equal(["RU"], _notifier.Notified);
    }

    /// <summary>После исправления выделенного результат выделяется снова: через UI Automation, клавиши не нужны.</summary>
    [Fact]
    public async Task Selection_AfterReplace_ReselectsResultViaUia()
    {
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.Equal(["привет"], _focus.Reselected);
        Assert.Empty(_sender.Repeats);
    }

    /// <summary>Если UI Automation не смог выделить результат, выделяем клавишами Shift+Влево по числу символов.</summary>
    [Fact]
    public async Task Selection_ReselectFails_FallsBackToShiftLeft()
    {
        _guard.Selection = "ghbdtn";
        _focus.ReselectWorks = false;

        await Run(SelectionStep());

        Assert.Equal(["Leftx6 shift"], _sender.Repeats);
    }

    /// <summary>CaretAfter.Restore: результат выделен, каретка справа (влево на длину, затем Shift+Вправо).</summary>
    [Fact]
    public async Task Selection_CaretRestore_SelectsWithCaretOnRight()
    {
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep(caretAfter: CaretAfter.Restore));

        Assert.Equal(["Leftx6", "Rightx6 shift"], _sender.Repeats);
        Assert.Empty(_focus.Reselected);
    }

    /// <summary>CaretAfter.Left: выделения нет, каретка слева от результата.</summary>
    [Fact]
    public async Task Selection_CaretLeft_PlacesCaretBeforeResult()
    {
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep(caretAfter: CaretAfter.Left));

        Assert.Equal(["Leftx6"], _sender.Repeats);
        Assert.Empty(_focus.Reselected);
    }

    /// <summary>CaretAfter.Right: выделения нет, каретка справа, клавиши не нужны.</summary>
    [Fact]
    public async Task Selection_CaretRight_SendsNoKeys()
    {
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep(caretAfter: CaretAfter.Right));

        Assert.Empty(_sender.Repeats);
        Assert.Empty(_focus.Reselected);
    }

    /// <summary>Для «до начала строки» CaretAfter не действует.</summary>
    [Fact]
    public async Task Line_CaretAfterIgnored()
    {
        _guard.Selection = "ghbdtn";

        await Run(new CorrectStep { Scope = CorrectScope.Line, CaretAfter = CaretAfter.Left });

        Assert.Empty(_sender.Repeats);
    }
    /// <summary>Повторное нажатие переводит выделенный результат обратно.</summary>
    [Fact]
    public async Task Selection_RepeatedOnResult_ConvertsBack()
    {
        _guard.Selection = "привет";

        await Run(SelectionStep());

        Assert.Equal("ghbdtn", _guard.Pasted);
        Assert.Equal([0x0409], _layouts.Switched);
    }

    /// <summary>Каретка без выделения: ничего не делаем и буфер не трогаем.</summary>
    [Fact]
    public async Task Selection_OnlyCaret_DoesNothing()
    {
        _focus.State = SelectionState.Caret;
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.Equal(0, _guard.Calls);
        Assert.Empty(_layouts.Switched);
    }

    /// <summary>Консоль: ни Ctrl+C, ни клавиш, ни переключения.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Terminal_BothActions_DoNothing(bool line)
    {
        _focus.Terminal = true;
        _guard.Selection = "ghbdtn";

        await Run(line ? LineStep() : SelectionStep());

        Assert.Equal(0, _guard.Calls);
        Assert.Empty(_sender.Repeats);
        Assert.Empty(_layouts.Switched);
        Assert.Contains("INFO correction skipped: terminal window", _log.Entries);
    }

    /// <summary>Модификаторы жеста не отпущены вовремя: ничего не делаем.</summary>
    [Fact]
    public async Task Correct_ModifiersNotReleased_DoesNothing()
    {
        _waiter.Released = false;
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.Equal(0, _guard.Calls);
    }

    /// <summary>Без UI Automation копируется как есть, но «строка целиком» (Sublime, VS Code) не исправляется.</summary>
    [Fact]
    public async Task Selection_UiaUnavailableAndCopiedWholeLine_IsRejected()
    {
        _focus.State = SelectionState.Unavailable;
        _guard.Selection = "ghbdtn vbh\n";

        await Run(SelectionStep());

        Assert.Null(_guard.Pasted);
        Assert.Empty(_layouts.Switched);
    }

    /// <summary>Без UI Automation обычное выделение исправляется.</summary>
    [Fact]
    public async Task Selection_UiaUnavailable_StillCorrects()
    {
        _focus.State = SelectionState.Unavailable;
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.Equal("привет", _guard.Pasted);
    }

    /// <summary>При подтверждённом UI Automation выделении перевод строки в конце — часть текста, а не «строка целиком».</summary>
    [Fact]
    public async Task Selection_UiaSelectedWithTrailingBreak_IsCorrected()
    {
        _guard.Selection = "ghbdtn\r\n";

        await Run(SelectionStep());

        Assert.Equal("привет\r\n", _guard.Pasted);
    }

    /// <summary>Текст длиннее лимита не исправляется.</summary>
    [Fact]
    public async Task Selection_LongerThanLimit_IsRejected()
    {
        _guard.Selection = new string('a', 11);

        await Run(SelectionStep(maxChars: 10));

        Assert.Null(_guard.Pasted);
        Assert.Empty(_layouts.Switched);
    }

    /// <summary>Лимит длины включает граничное значение.</summary>
    [Fact]
    public async Task Selection_ExactlyLimit_IsCorrected()
    {
        _guard.Selection = new string('a', 10);

        await Run(SelectionStep(maxChars: 10));

        Assert.Equal(new string('ф', 10), _guard.Pasted);
    }

    /// <summary>Выключенное переключение языка: текст исправлен, язык и подсказка не тронуты. Для обоих действий.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correct_SwitchLayoutDisabled_DoesNotSwitchOrNotify(bool line)
    {
        _guard.Selection = "ghbdtn";

        await Run(line ? LineStep(switchLayout: false) : SelectionStep(switchLayout: false));

        Assert.Equal("привет", _guard.Pasted);
        Assert.Empty(_layouts.Switched);
        Assert.Empty(_notifier.Notified);
    }

    /// <summary>Включённое переключение языка работает у обоих действий независимо.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correct_SwitchLayoutEnabled_SwitchesAndNotifies(bool line)
    {
        _guard.Selection = "ghbdtn";

        await Run(line ? LineStep() : SelectionStep());

        Assert.Equal([0x0419], _layouts.Switched);
        Assert.Equal(["RU"], _notifier.Notified);
    }

    /// <summary>Букв поровну («ab» латиницей и «ты» кириллицей): направление задаёт текущая раскладка окна.</summary>
    [Theory]
    [InlineData(0x0409, "фи ты")]
    [InlineData(0x0419, "ab ns")]
    public async Task Selection_EqualLetters_UsesCurrentLayout(int currentLanguage, string expected)
    {
        _layouts.Current = currentLanguage == 0x0409 ? _en : _ru;
        _guard.Selection = "ab ты";

        await Run(SelectionStep());

        Assert.Equal(expected, _guard.Pasted);
    }

    /// <summary>Букв поровну, а текущая раскладка не из пары: исправления нет.</summary>
    [Fact]
    public async Task Selection_EqualLettersAndForeignLayout_IsRejected()
    {
        _layouts.Current = new LayoutInfo(0x04070407, "DE");
        _guard.Selection = "ab ты";

        await Run(SelectionStep());

        Assert.Null(_guard.Pasted);
    }
    /// <summary>Нет русской раскладки в системе: ничего не делаем.</summary>
    [Fact]
    public async Task Correct_LayoutNotInstalled_DoesNothing()
    {
        _layouts.HasRussian = false;
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.Equal(0, _guard.Calls);
    }

    /// <summary>Неверная пара раскладок пишется в лог как ошибка.</summary>
    [Fact]
    public async Task Correct_InvalidPair_LogsError()
    {
        await Run(new CorrectStep { Scope = CorrectScope.Selection, Pair = ["ru-RU"] });

        Assert.Contains("ERROR correction skipped: layout pair is invalid", _log.Entries);
        Assert.Equal(0, _guard.Calls);
    }

    /// <summary>До начала строки через UI Automation: копируем, вставляем, клавиши не нужны, каретка остаётся на месте.</summary>
    [Fact]
    public async Task Line_UiaSelected_ReplacesWithoutKeys()
    {
        _guard.Selection = "Ctujlyz";

        await Run(LineStep());

        Assert.Equal("Сегодня", _guard.Pasted);
        Assert.Empty(_sender.Repeats);
        Assert.Empty(_focus.Reselected);
    }

    /// <summary>До начала строки: каретка в начале строки, одни пробелы, длинный текст или уже выделенное — ничего не делаем.</summary>
    [Theory]
    [InlineData((int)LineSelectResult.Empty)]
    [InlineData((int)LineSelectResult.TooLong)]
    [InlineData((int)LineSelectResult.HasSelection)]
    public async Task Line_NothingToCorrect_DoesNothing(int result)
    {
        _focus.Line = (LineSelectResult)result;
        _guard.Selection = "ghbdtn";

        await Run(LineStep());

        Assert.Equal(0, _guard.Calls);
        Assert.Empty(_sender.Repeats);
        Assert.Empty(_layouts.Switched);
    }

    /// <summary>Без UI Automation: Shift+Home выделяет строку, после успешной замены каретка уже на месте.</summary>
    [Fact]
    public async Task Line_UiaUnavailable_SelectsWithShiftHome()
    {
        _focus.Line = LineSelectResult.Unavailable;
        _guard.Selection = "ghbdtn";

        await Run(LineStep());

        Assert.Equal(["Homex1 shift"], _sender.Repeats);
        Assert.Equal("привет", _guard.Pasted);
    }

    /// <summary>Без UI Automation и ничего не скопировалось (каретка в начале строки): каретку не двигаем.</summary>
    [Fact]
    public async Task Line_KeysAndNothingCopied_DoesNotMoveCaret()
    {
        _focus.Line = LineSelectResult.Unavailable;
        _guard.Selection = null;

        await Run(LineStep());

        Assert.Equal(["Homex1 shift"], _sender.Repeats);
    }

    /// <summary>Текст не подошёл после выделения (например, одни пробелы): выделение снимается клавишей Вправо, каретка на месте.</summary>
    [Theory]
    [InlineData((int)LineSelectResult.Selected)]
    [InlineData((int)LineSelectResult.Unavailable)]
    public async Task Line_TextRejected_CollapsesSelectionToCaret(int selectedCode)
    {
        var selected = (LineSelectResult)selectedCode;
        _focus.Line = selected;
        _guard.Selection = "   ";

        await Run(LineStep());

        Assert.Null(_guard.Pasted);
        string[] expected = selected == LineSelectResult.Unavailable ? ["Homex1 shift", "Rightx1"] : ["Rightx1"];
        Assert.Equal(expected, _sender.Repeats);
    }

    /// <summary>В выделении «до начала строки» перевода строки быть не может: такой текст отклоняется.</summary>
    [Fact]
    public async Task Line_TextWithLineBreak_IsRejected()
    {
        _guard.Selection = "ghbdtn\nvbh";

        await Run(LineStep());

        Assert.Null(_guard.Pasted);
    }

    /// <summary>В журнал не попадает ни текст, ни его перевод.</summary>
    [Fact]
    public async Task Correct_Logs_DoNotContainText()
    {
        _guard.Selection = "ghbdtn";

        await Run(SelectionStep());

        Assert.NotEmpty(_log.Entries);
        Assert.DoesNotContain(_log.Entries, e => e.Contains("ghbdtn", StringComparison.Ordinal) || e.Contains("привет", StringComparison.Ordinal));
    }
}
