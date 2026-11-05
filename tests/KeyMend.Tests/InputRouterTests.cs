using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="InputRouter"/>: буфер набранного для автозамены, его лимит и сбросы.</summary>
public sealed class InputRouterTests
{
    /// <summary>Создаёт триггер tap из строки клавиш.</summary>
    private static TapTrigger Tap(string keys)
    {
        Assert.True(KeyChordParser.TryParse(keys, out var chord));
        return new TapTrigger { Keys = chord.Modifiers };
    }

    /// <summary>Правило из одного шага.</summary>
    private static Rule RuleOf(string name, Trigger trigger, Step step) =>
        new() { Name = name, Trigger = trigger, Steps = [step] };

    /// <summary>Правила для стенда: переключение языка и исправление выделенного.</summary>
    private static Rule[] DefaultRules() =>
    [
        RuleOf("fix", Tap("RShift"), new CorrectStep { Scope = CorrectScope.Selection }),
        RuleOf("lang", Tap("RCtrl"), new SwitchLayoutStep()),
    ];

    /// <summary>Стенд: роутер с движком, «физическое» состояние клавиш и часы, которые двигаются на каждое событие.</summary>
    private sealed class Rig
    {
        /// <summary>Клавиши, зажатые «физически» сейчас.</summary>
        private readonly HashSet<VirtualKey> _held = [];

        /// <summary>Текущее время стенда, мс.</summary>
        private uint _time = 1000;

        /// <summary>Создаёт стенд с правилами по умолчанию.</summary>
        public Rig(int limit = 200)
        {
            Router = new InputRouter(new HotkeyEngine(), limit, _held.Contains, () => Layout);
            Router.SetBindings(BindingTable.Build(DefaultRules()));
        }

        /// <summary>Раскладка окна с фокусом (HKL), которую «читает» роутер; 0 — неизвестна.</summary>
        public nint Layout { get; set; }

        /// <summary>Роутер под тестом.</summary>
        public InputRouter Router { get; }

        /// <summary>Сколько клавиш в буфере набранного.</summary>
        public int Buffered => Router.BufferedCount;

        /// <summary>Включён ли CapsLock для набираемых клавиш.</summary>
        public bool CapsLock { get; set; }

        /// <summary>Нажимает клавишу.</summary>
        public HookDecision Down(VirtualKey key)
        {
            _held.Add(key);
            return Send(key, isUp: false);
        }

        /// <summary>Отпускает клавишу.</summary>
        public HookDecision Up(VirtualKey key)
        {
            _held.Remove(key);
            return Send(key, isUp: true);
        }

        /// <summary>Нажимает и отпускает клавишу; возвращает решение на отпускание.</summary>
        public HookDecision Tap(VirtualKey key)
        {
            Down(key);
            return Up(key);
        }

        /// <summary>Набирает строку в английской раскладке: Shift зажимается для заглавных и знаков.</summary>
        public void Type(string text)
        {
            foreach (var stroke in TextKeys.Type(text, FallbackLayouts.Us))
            {
                if (stroke.Shift)
                {
                    Down(VirtualKey.LShift);
                }

                Tap(stroke.Vk);
                if (stroke.Shift)
                {
                    Up(VirtualKey.LShift);
                }
            }
        }

        /// <summary>Включает автозамену: сокращение <c>дд </c> (то же, что <c>ll </c>) с текстом «Добрый день!».</summary>
        public void UseSnippets()
        {
            var compiled = SnippetCompiler.Compile(
                [new Snippet { Id = "dd", Keys = "дд ", Text = "Добрый день!" }],
                [new InstalledLayout(0x0409, FallbackLayouts.Us), new InstalledLayout(0x0419, FallbackLayouts.Ru)]);
            Router.SetBindings(BindingTable.Build(DefaultRules(), compiled.Matcher));
        }

        /// <summary>Клик мышью.</summary>
        public void Click() => Router.OnMouseDown(_time += 10);

        /// <summary>Отправляет событие.</summary>
        private HookDecision Send(VirtualKey key, bool isUp)
        {
            _time += 10;
            return Router.OnKey(new KeyEvent(key, 0, isUp, false, false, _time, CapsLock));
        }
    }

    /// <summary>Набранные символьные клавиши попадают в буфер.</summary>
    [Fact]
    public void Typing_AddsKeysToBuffer()
    {
        var rig = new Rig();

        rig.Type("ghbdtn");

        Assert.Equal(6, rig.Buffered);
    }

    /// <summary>Клавиши Shift и CapsLock в буфер сами не попадают.</summary>
    [Fact]
    public void ModifierKeysAndCapsLock_AreNotBuffered()
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Tap(VirtualKey.CapsLock);
        rig.Tap(VirtualKey.LShift);

        Assert.Equal(3, rig.Buffered);
    }

    /// <summary>Заглавные буквы набираются с Shift: клавиша в буфер попадает один раз.</summary>
    [Fact]
    public void ShiftedLetter_IsBufferedOnce()
    {
        var rig = new Rig();
        rig.Down(VirtualKey.LShift);
        rig.Tap((VirtualKey)0x41);
        rig.Up(VirtualKey.LShift);

        Assert.Equal(1, rig.Buffered);
    }

    /// <summary>Сработавшее правило (переключение языка) очищает буфер.</summary>
    [Fact]
    public void AnyRule_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");

        var lang = rig.Tap(VirtualKey.RControl);

        Assert.Equal("lang", lang.Rule?.Name);
        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Правило исправления тоже очищает буфер: оно берёт текст из окна, а набранное больше не актуально.</summary>
    [Fact]
    public void CorrectionRule_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");

        var fix = rig.Tap(VirtualKey.RShift);

        Assert.Equal("fix", fix.Rule?.Name);
        Assert.False(fix.Suppress);
        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Отпускание Ctrl после набора очищает буфер: сочетание закончилось.</summary>
    [Fact]
    public void CtrlRelease_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");
        rig.Down(VirtualKey.LControl);
        rig.Up(VirtualKey.LControl);

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Ctrl при нажатии буфер не очищает.</summary>
    [Fact]
    public void CtrlDown_KeepsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Down(VirtualKey.RControl);

        Assert.Equal(3, rig.Buffered);
    }

    /// <summary>Символьная клавиша с Ctrl — сочетание: в буфер не попадает и очищает его.</summary>
    [Fact]
    public void CtrlShortcut_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");
        rig.Down(VirtualKey.LControl);
        rig.Tap((VirtualKey)0x43);
        rig.Up(VirtualKey.LControl);

        Assert.Equal(0, rig.Buffered);
        rig.Type("x");
        Assert.Equal(1, rig.Buffered);
    }

    /// <summary>Alt и Win очищают буфер сразу при нажатии.</summary>
    [Theory]
    [InlineData((int)VirtualKey.LMenu)]
    [InlineData((int)VirtualKey.RMenu)]
    [InlineData((int)VirtualKey.LWin)]
    [InlineData((int)VirtualKey.RWin)]
    public void AltOrWinDown_ClearsBuffer(int code)
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Down((VirtualKey)code);

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Backspace убирает последнюю клавишу.</summary>
    [Fact]
    public void Backspace_RemovesLastKey()
    {
        var rig = new Rig();
        rig.Type("abcd");

        rig.Tap(VirtualKey.Back);

        Assert.Equal(3, rig.Buffered);
    }

    /// <summary>Ctrl+Backspace стирает слово целиком, поэтому буфер очищается.</summary>
    [Fact]
    public void CtrlBackspace_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");
        rig.Down(VirtualKey.LControl);
        rig.Tap(VirtualKey.Back);
        rig.Up(VirtualKey.LControl);

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Клавиши, меняющие положение или завершающие ввод, очищают буфер.</summary>
    [Theory]
    [InlineData((int)VirtualKey.Return)]
    [InlineData((int)VirtualKey.Tab)]
    [InlineData((int)VirtualKey.Escape)]
    [InlineData((int)VirtualKey.Left)]
    [InlineData((int)VirtualKey.Right)]
    [InlineData((int)VirtualKey.Up)]
    [InlineData((int)VirtualKey.Down)]
    [InlineData((int)VirtualKey.Home)]
    [InlineData((int)VirtualKey.End)]
    [InlineData((int)VirtualKey.PageUp)]
    [InlineData((int)VirtualKey.PageDown)]
    [InlineData((int)VirtualKey.Delete)]
    public void NavigationKey_ClearsBuffer(int code)
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Tap((VirtualKey)code);

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Клик мышью очищает буфер.</summary>
    [Fact]
    public void Click_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Click();

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Смена окна (сброс обработчика) очищает буфер.</summary>
    [Fact]
    public void Reset_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Router.Reset();

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Смена набора привязок очищает буфер: правила и окно другие.</summary>
    [Fact]
    public void SetBindings_ClearsBuffer()
    {
        var rig = new Rig();
        rig.Type("abc");

        rig.Router.SetBindings(BindingTable.Build(DefaultRules()));

        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Буфер хранит не больше лимита: старые клавиши отбрасываются.</summary>
    [Fact]
    public void BufferLimit_KeepsOnlyLastKeys()
    {
        var rig = new Rig(limit: 3);

        rig.Type("abcde");

        Assert.Equal(3, rig.Buffered);
    }

    /// <summary>Сокращение срабатывает на пробеле: клавиша подавлена, правило отдано, буфер очищен.</summary>
    [Fact]
    public void Snippet_TypedAtWordStart_FiresAndSuppressesLastKey()
    {
        var rig = new Rig();
        rig.UseSnippets();
        rig.Type("ll");

        var decision = rig.Down(VirtualKey.Space);

        Assert.True(decision.Suppress);
        Assert.Equal("dd", decision.Rule?.Id);
        Assert.Equal(0, rig.Buffered);
    }

    /// <summary>Автоповтор и отпускание конечной клавиши подавляются; следующее нажатие — уже обычное.</summary>
    [Fact]
    public void Snippet_AfterFire_RepeatAndReleaseSuppressedOnce()
    {
        var rig = new Rig();
        rig.UseSnippets();
        rig.Type("ll");
        rig.Down(VirtualKey.Space);

        var repeat = rig.Down(VirtualKey.Space);
        var release = rig.Up(VirtualKey.Space);
        var next = rig.Down(VirtualKey.Space);

        Assert.True(repeat.Suppress);
        Assert.Null(repeat.Rule);
        Assert.True(release.Suppress);
        Assert.False(next.Suppress);
        Assert.Equal(1, rig.Buffered);
    }

    /// <summary>Если отпускание потерялось, новое нажатие той же клавиши не подавляется.</summary>
    [Fact]
    public void Snippet_ReleaseLost_NextPressIsNormal()
    {
        var rig = new Rig();
        rig.UseSnippets();
        rig.Type("ll");
        rig.Down(VirtualKey.Space);
        rig.Up(VirtualKey.Space);

        rig.Type("ll");
        var second = rig.Down(VirtualKey.Space);

        Assert.Equal("dd", second.Rule?.Id);
    }

    /// <summary>Пробел с зажатым Shift тоже завершает сокращение.</summary>
    [Fact]
    public void Snippet_SpaceWithShift_Fires()
    {
        var rig = new Rig();
        rig.UseSnippets();
        rig.Type("ll");
        rig.Down(VirtualKey.LShift);

        var decision = rig.Down(VirtualKey.Space);

        Assert.Equal("dd", decision.Rule?.Id);
    }

    /// <summary>Заглавные буквы (Shift отпущен до пробела) тоже подходят.</summary>
    [Fact]
    public void Snippet_UpperCaseTyping_Fires()
    {
        var rig = new Rig();
        rig.UseSnippets();
        rig.Type("LL");

        var decision = rig.Down(VirtualKey.Space);

        Assert.Equal("dd", decision.Rule?.Id);
    }

    /// <summary>В середине слова, после клика и после правила буфер ведёт себя по F5.1.</summary>
    [Fact]
    public void Snippet_MidWordClickAndRule_FollowWordStartRules()
    {
        var rig = new Rig();
        rig.UseSnippets();

        rig.Type("xll");
        var inWord = rig.Down(VirtualKey.Space);
        rig.Up(VirtualKey.Space);

        rig.Type("zz");
        rig.Click();
        rig.Type("ll");
        var afterClick = rig.Down(VirtualKey.Space);
        rig.Up(VirtualKey.Space);

        rig.Type("j,");
        rig.Tap(VirtualKey.RControl);
        rig.Type("y");
        var afterRule = rig.Down(VirtualKey.Space);

        Assert.Null(inWord.Rule);
        Assert.Equal("dd", afterClick.Rule?.Id);
        Assert.Null(afterRule.Rule);
    }

    /// <summary>Клавиша «ю»/«.» перед сокращением: в английском окне это точка (слово начинается), в русском — буква (нет).</summary>
    [Fact]
    public void Snippet_KeyBeforeAbbreviation_DependsOnWindowLayout()
    {
        var rig = new Rig();
        rig.UseSnippets();

        rig.Layout = 0x0419;
        rig.Type("a.ll");
        var russian = rig.Down(VirtualKey.Space);
        rig.Up(VirtualKey.Space);

        rig.Layout = 0x0409;
        rig.Type("a.ll");
        var english = rig.Down(VirtualKey.Space);

        Assert.Null(russian.Rule);
        Assert.Equal("dd", english.Rule?.Id);
    }

    /// <summary>Без настроенных сокращений ничего не срабатывает и клавиши не подавляются.</summary>
    [Fact]
    public void Snippet_NotConfigured_NeverFires()
    {
        var rig = new Rig();
        rig.Type("ll");

        var decision = rig.Down(VirtualKey.Space);

        Assert.Null(decision.Rule);
        Assert.False(decision.Suppress);
    }

    /// <summary>Пауза и исключение программы отключают автозамену, остальные программы работают.</summary>
    [Fact]
    public void BindingTables_PausedAndExcludedProcess_HaveNoSnippets()
    {
        var compiled = SnippetCompiler.Compile(
            [new Snippet { Id = "dd", Keys = "дд ", Text = "Добрый день!" }],
                [new InstalledLayout(0x0409, FallbackLayouts.Us), new InstalledLayout(0x0419, FallbackLayouts.Ru)]);

        var tables = BindingTables.Build(
            DefaultRules(), [new ExclusionEntry { Process = "mstsc.exe", Snippets = false }], compiled.Matcher);

        Assert.Equal(1, tables.Default.Snippets.Count);
        Assert.Equal(1, tables.For("notepad.exe").Snippets.Count);
        Assert.Equal(0, tables.For("MSTSC.EXE").Snippets.Count);
        Assert.Equal(0, tables.Paused.Snippets.Count);
    }

    /// <summary>В окне самого KeyMend автозамены нет, но остальные программы её сохраняют.</summary>
    [Fact]
    public void BindingTables_OwnProcess_HasNoSnippets()
    {
        var compiled = SnippetCompiler.Compile(
            [new Snippet { Id = "dd", Keys = "дд ", Text = "Добрый день!" }],
            [new InstalledLayout(0x0409, FallbackLayouts.Us), new InstalledLayout(0x0419, FallbackLayouts.Ru)]);

        var tables = BindingTables.Build(DefaultRules(), [], compiled.Matcher, "KeyMend.exe");

        Assert.Equal(0, tables.For("keymend.exe").Snippets.Count);
        Assert.Equal(1, tables.For("notepad.exe").Snippets.Count);
        Assert.Equal(1, tables.Default.Snippets.Count);
    }

    /// <summary>Смена лимита на лету меняет размер буфера.</summary>
    [Fact]
    public void SetBufferLimit_ResizesBuffer()
    {
        var rig = new Rig();
        rig.Router.SetBufferLimit(2);

        rig.Type("abcde");

        Assert.Equal(2, rig.Buffered);
    }
}
