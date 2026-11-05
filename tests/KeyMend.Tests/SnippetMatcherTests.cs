using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SnippetCompiler"/> и <see cref="SnippetMatcher"/> на данных SPEC F5.5.</summary>
public sealed class SnippetMatcherTests
{
    /// <summary>Таблицы установленных раскладок: EN, затем RU.</summary>
    private static readonly LayoutTable[] _tables = [FallbackLayouts.Us, FallbackLayouts.Ru];

    /// <summary>Стартовый набор F5.5.</summary>
    private static readonly IReadOnlyList<Snippet> _starter = LoadStarter();

    /// <summary>Сокращение срабатывает на последней клавише и не зависит от раскладки, в которой его набрали.</summary>
    [Theory]
    [InlineData("дд ", "dd", true)]
    [InlineData("ll ", "dd", false)]
    [InlineData("br ", "br", false)]
    [InlineData("ик ", "br", true)]
    [InlineData("note ", "note", false)]
    [InlineData("тщеу ", "note", true)]
    public void TryMatch_AbbreviationInAnyLayout_Fires(string typed, string id, bool russian)
    {
        var matcher = Compile(_starter);

        var (rule, firedAt) = Type(matcher, new WordBuffer(50), typed, russian ? FallbackLayouts.Ru : FallbackLayouts.Us);

        Assert.Equal(id, rule?.Id);
        Assert.Equal(typed.Length - 1, firedAt);
    }

    /// <summary>Правило несёт текст, число стираний и шаги после вставки.</summary>
    [Fact]
    public void TryMatch_Fired_RuleHasInsertEraseAndAfterSteps()
    {
        var matcher = Compile(_starter);

        var (rule, _) = Type(matcher, new WordBuffer(50), "дд ", FallbackLayouts.Ru);

        Assert.NotNull(rule);
        var insert = Assert.IsType<InsertTextStep>(rule.Steps[0]);
        Assert.Equal("Добрый день!", insert.Text);
        Assert.Equal(2, insert.Erase);
        Assert.Equal(VirtualKey.Return, Assert.IsType<PressStep>(rule.Steps[1]).Keys.Key);
        Assert.Equal("snippet #1", rule.Name);
    }

    /// <summary>Секретное сокращение вставляется через буфер по ключу, без текста.</summary>
    [Fact]
    public void TryMatch_Secret_UsesClipboardAndRef()
    {
        var matcher = Compile(_starter);

        var (rule, _) = Type(matcher, new WordBuffer(50), "sec1 ", FallbackLayouts.Us);

        Assert.NotNull(rule);
        var insert = Assert.IsType<InsertTextStep>(rule.Steps[0]);
        Assert.Null(insert.Text);
        Assert.Equal("test-secret-1", insert.SecretRef);
        Assert.Equal(InsertMode.Clipboard, insert.Insert);
    }

    /// <summary>Регистр и Shift у букв и пробела не важны.</summary>
    [Fact]
    public void TryMatch_UpperCaseAndShiftedSpace_StillFires()
    {
        var matcher = Compile(_starter);
        var keys = TextKeys.Type("ДД ", FallbackLayouts.Ru);
        keys[^1] = keys[^1] with { Shift = true };

        var (rule, _) = Feed(matcher, new WordBuffer(50), keys);

        Assert.Equal("dd", rule?.Id);
    }

    /// <summary>В середине слова сокращение не срабатывает.</summary>
    [Fact]
    public void TryMatch_InsideWord_DoesNotFire()
    {
        var matcher = Compile(_starter);

        var (rule, _) = Type(matcher, new WordBuffer(50), "хдд ", FallbackLayouts.Ru);

        Assert.Null(rule);
    }

    /// <summary>После пробела, слэша и дефиса сокращение срабатывает.</summary>
    [Theory]
    [InlineData("слово дд ")]
    [InlineData("a/дд ")]
    [InlineData("a-дд ")]
    public void TryMatch_AfterSeparator_Fires(string typed)
    {
        var matcher = Compile(_starter);
        var split = typed.IndexOfAny([' ', '/', '-']) + 1;
        var keys = new List<KeyStroke>();
        keys.AddRange(TextKeys.Type(typed[..split], typed[0] == 'a' ? FallbackLayouts.Us : FallbackLayouts.Ru));
        keys.AddRange(TextKeys.Type(typed[split..], FallbackLayouts.Ru));

        var (rule, _) = Feed(matcher, new WordBuffer(50), [.. keys]);

        Assert.Equal("dd", rule?.Id);
    }

    /// <summary>Точка и запятая в русской раскладке — буквы, поэтому после них начала слова нет.</summary>
    [Theory]
    [InlineData("text.")]
    [InlineData("a,")]
    public void TryMatch_AfterDotOrComma_DoesNotFire(string head)
    {
        var matcher = Compile(_starter);
        var keys = new List<KeyStroke>();
        keys.AddRange(TextKeys.Type(head, FallbackLayouts.Us));
        keys.AddRange(TextKeys.Type("дд ", FallbackLayouts.Ru));

        var (rule, _) = Feed(matcher, new WordBuffer(50), [.. keys]);

        Assert.Null(rule);
    }

    /// <summary>Русские точка и запятая — это клавиши «/» и Shift+«/»: они разделяют слова, английские — нет.</summary>
    [Theory]
    [InlineData("текст.", true)]
    [InlineData("а,", true)]
    public void TryMatch_AfterRussianPunctuation_Fires(string head, bool expected)
    {
        var matcher = Compile(_starter);
        var keys = new List<KeyStroke>();
        keys.AddRange(TextKeys.Type(head, FallbackLayouts.Ru));
        keys.AddRange(TextKeys.Type("дд ", FallbackLayouts.Ru));

        var (rule, _) = Feed(matcher, new WordBuffer(50), [.. keys]);

        Assert.Equal(expected, rule is not null);
    }

    /// <summary>Сразу после сброса буфера сокращение срабатывает, а если старые клавиши вытеснены — нет.</summary>
    [Fact]
    public void TryMatch_BufferBoundary_ClearedYesEvictedNo()
    {
        var matcher = Compile(_starter);
        var small = new WordBuffer(4);

        var (evicted, _) = Type(matcher, small, "хдд ", FallbackLayouts.Ru);
        small.Clear();
        var (cleared, _) = Type(matcher, small, "дд ", FallbackLayouts.Ru);

        Assert.Null(evicted);
        Assert.Equal("dd", cleared?.Id);
    }

    /// <summary>Backspace убирает ошибочную клавишу: набор продолжается с начала слова.</summary>
    [Fact]
    public void TryMatch_BackspaceCorrectsTypo_Fires()
    {
        var matcher = Compile(_starter);
        var buffer = new WordBuffer(50);
        buffer.Push(TextKeys.Type("д", FallbackLayouts.Ru)[0]);
        buffer.Push(TextKeys.Type("я", FallbackLayouts.Ru)[0]);

        buffer.Backspace();
        var (rule, _) = Type(matcher, buffer, "д ", FallbackLayouts.Ru);

        Assert.Equal("dd", rule?.Id);
    }

    /// <summary>Backspace в пустом буфере теряет границу слова: что стёрто, неизвестно.</summary>
    [Fact]
    public void TryMatch_BackspaceOnEmptyBuffer_DoesNotFire()
    {
        var matcher = Compile(_starter);
        var buffer = new WordBuffer(50);

        buffer.Backspace();
        var (rule, _) = Type(matcher, buffer, "дд ", FallbackLayouts.Ru);

        Assert.Null(rule);
    }

    /// <summary>Короткое сокращение срабатывает раньше длинного, начинающегося так же; предупреждение — о недостижимом.</summary>
    [Fact]
    public void Compile_PrefixSnippets_ShortFiresFirstAndWarns()
    {
        var result = SnippetCompiler.Compile(
            [new Snippet { Id = "a", Keys = "дд", Text = "A" }, new Snippet { Id = "b", Keys = "дд ", Text = "B" }], _tables);

        var (rule, firedAt) = Type(result.Matcher, new WordBuffer(50), "дд ", FallbackLayouts.Ru);

        Assert.Equal("a", rule?.Id);
        Assert.Equal(1, firedAt);
        Assert.Contains(result.Warnings, w => w.Contains("never fires", StringComparison.Ordinal));
    }

    /// <summary>Если на одной клавише совпали два сокращения, берётся более длинное.</summary>
    [Fact]
    public void TryMatch_TwoMatchOnSameKey_PrefersLonger()
    {
        var result = SnippetCompiler.Compile(
            [new Snippet { Id = "short", Keys = "б ", Text = "S" }, new Snippet { Id = "long", Keys = "аб ", Text = "L" }], _tables);

        var (rule, _) = Type(result.Matcher, new WordBuffer(50), "аб ", FallbackLayouts.Ru);

        Assert.Equal("long", rule?.Id);
    }

    /// <summary>Дубликат пропускается с предупреждением; неразбираемые, пустые и выключенные записи не попадают в набор.</summary>
    [Fact]
    public void Compile_DuplicatesAndUnusable_AreSkipped()
    {
        var result = SnippetCompiler.Compile(
            [
                new Snippet { Id = "a", Keys = "дд ", Text = "A" },
                new Snippet { Id = "b", Keys = "ll ", Text = "B" },
                new Snippet { Id = "c", Keys = "дд§", Text = "C" },
                new Snippet { Id = "d", Keys = string.Empty, Text = "D" },
                new Snippet { Id = "e", Keys = "xyz ", Text = "E", Enabled = false },
                new Snippet { Id = "f", Keys = "qqq ", Secret = true },
                new Snippet { Id = "g", Keys = "www " },
            ],
            _tables);

        Assert.Equal(1, result.Matcher.Count);
        Assert.Equal(5, result.Warnings.Count);
        Assert.All(result.Warnings, w => Assert.DoesNotContain("дд", w, StringComparison.Ordinal));
    }

    /// <summary>Клавиши без букв (цифра, знак) требуют совпадения Shift.</summary>
    [Fact]
    public void TryMatch_NonLetterKey_RequiresSameShift()
    {
        var result = SnippetCompiler.Compile([new Snippet { Id = "a", Keys = "ab1 ", Text = "A" }], _tables);

        var (plain, _) = Type(result.Matcher, new WordBuffer(50), "ab1 ", FallbackLayouts.Us);
        var (shifted, _) = Type(result.Matcher, new WordBuffer(50), "ab! ", FallbackLayouts.Us);

        Assert.Equal("a", plain?.Id);
        Assert.Null(shifted);
    }

    /// <summary>Нейтральный стартовый файл <c>TestData/starter-settings.json</c> читается без замечаний, сокращения собираются, секреты запечатываются.</summary>
    [Fact]
    public void StarterSettings_ParseCompileAndSeal_WithoutWarnings()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "starter-settings.json"));

        var parsed = SettingsJson.Parse(json);
        var settings = Assert.IsType<AppSettings>(parsed.Settings);
        var counter = 0;
        var sealedResult = SecretSealer.Seal(settings, () => "key" + counter++);
        var compiled = SnippetCompiler.Compile(sealedResult.Settings.Snippets, _tables);

        Assert.Empty(parsed.Warnings);
        Assert.Equal(8, settings.Rules.Count);
        Assert.Equal(14, settings.Snippets.Count);
        Assert.Equal(1, sealedResult.Secrets.Count(s => s.Value == "test-secret-3"));
        Assert.Equal(4, sealedResult.Secrets.Count);
        Assert.Empty(compiled.Warnings);
        Assert.Equal(14, compiled.Matcher.Count);
    }

    /// <summary>Нейтральные правила запуска программ и сна: программы, триггеры и двойное нажатие F17.</summary>
    [Fact]
    public void StarterSettings_LaunchAndSleepRules_AreDefinedAsSpecified()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "starter-settings.json"));
        var rules = Assert.IsType<AppSettings>(SettingsJson.Parse(json).Settings).Rules.ToDictionary(r => r.Id);

        Assert.Equal("notepad.exe", Assert.IsType<LaunchStep>(Assert.Single(rules["monitor2-toggle"].Steps)).File);
        Assert.Equal("calc.exe", Assert.IsType<LaunchStep>(Assert.Single(rules["monitor1-90"].Steps)).File);
        Assert.Equal("calc.exe", Assert.IsType<LaunchStep>(Assert.Single(rules["monitor1-0"].Steps)).File);
        Assert.Equal(string.Empty, Assert.IsType<LaunchStep>(Assert.Single(rules["monitor1-90"].Steps)).Arguments);
        Assert.Null(Assert.IsType<PressTrigger>(rules["monitor1-90"].Trigger).DoubleMs);
        Assert.Equal(400, Assert.IsType<PressTrigger>(rules["monitor1-0"].Trigger).DoubleMs);
        Assert.Equal(PowerAction.Sleep, Assert.IsType<PowerStep>(Assert.Single(rules["sleep"].Steps)).Action);
    }

    /// <summary>Описатель английской раскладки в тестах.</summary>
    private const nint _hklEn = 0x0409;

    /// <summary>Описатель русской раскладки в тестах.</summary>
    private const nint _hklRu = 0x0419;

    /// <summary>Установленные раскладки с описателями.</summary>
    private static readonly InstalledLayout[] _installed =
    [
        new(_hklEn, FallbackLayouts.Us),
        new(_hklRu, FallbackLayouts.Ru),
    ];

    /// <summary>Сокращение <c>дд </c> с учётом раскладки окна.</summary>
    private static SnippetMatcher CompileByLayout() =>
        SnippetCompiler.Compile([new Snippet { Id = "dd", Keys = "дд ", Text = "Добрый день!" }], _installed).Matcher;

    /// <summary>Набирает «голову» в одной раскладке, затем <c>дд </c> физическими клавишами; раскладка окна — <paramref name="window"/>.</summary>
    private static Rule? TypeInWindow(string head, LayoutTable headTable, nint window, int[]? reads = null)
    {
        var keys = new List<KeyStroke>();
        keys.AddRange(TextKeys.Type(head, headTable));
        keys.AddRange(TextKeys.Type("дд ", FallbackLayouts.Ru));
        return Feed(
            CompileByLayout(), new WordBuffer(50), [.. keys],
            () =>
            {
                if (reads is not null)
                {
                    reads[0]++;
                }

                return window;
            }).Rule;
    }

    /// <summary>В окне с английской раскладкой точка, запятая и скобки разделяют слова, как и в русской — клавиша «/».</summary>
    [Theory]
    [InlineData("text.", true)]
    [InlineData("a,", true)]
    [InlineData("a[", true)]
    [InlineData("a;", true)]
    public void TryMatch_EnglishWindow_PunctuationStartsWord(string head, bool expected)
    {
        Assert.Equal(expected, TypeInWindow(head, FallbackLayouts.Us, _hklEn) is not null);
    }

    /// <summary>В окне с русской раскладкой клавиши «ю», «б», «х» — буквы и слово не начинают, а «.» и «,» русской раскладки — начинают.</summary>
    [Theory]
    [InlineData("текст.", true)]
    [InlineData("а,", true)]
    [InlineData("аю", false)]
    [InlineData("аб", false)]
    [InlineData("фх", false)]
    public void TryMatch_RussianWindow_LettersOnPunctuationKeysDoNotStartWord(string head, bool expected)
    {
        Assert.Equal(expected, TypeInWindow(head, FallbackLayouts.Ru, _hklRu) is not null);
    }

    /// <summary>Неизвестная раскладка окна (0 или не из списка установленных): работает общее правило — клавиша-буква хоть в одной раскладке слово не начинает.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0x0407)]
    public void TryMatch_UnknownWindowLayout_UsesGeneralRule(int hkl)
    {
        Assert.Null(TypeInWindow("text.", FallbackLayouts.Us, hkl));
        Assert.NotNull(TypeInWindow("a/", FallbackLayouts.Us, hkl));
    }

    /// <summary>Раскладка окна читается только при полном совпадении шаблона и только один раз.</summary>
    [Fact]
    public void TryMatch_WindowLayout_ReadOnlyOnFullMatchAndOnce()
    {
        var reads = new int[1];

        TypeInWindow("a/", FallbackLayouts.Us, _hklEn, reads);
        var afterMatch = reads[0];
        var noMatch = new int[1];
        Feed(CompileByLayout(), new WordBuffer(50), TextKeys.Type("hello world", FallbackLayouts.Us), () => ++noMatch[0]);

        Assert.Equal(1, afterMatch);
        Assert.Equal(0, noMatch[0]);
    }

    /// <summary>Набирает строку в раскладке, подавая клавиши, как хук.</summary>
    private static (Rule? Rule, int FiredAt) Type(SnippetMatcher matcher, WordBuffer buffer, string text, LayoutTable table) =>
        Feed(matcher, buffer, TextKeys.Type(text, table));

    /// <summary>Подаёт клавиши в буфер по одной, после каждой ищет сокращение; при срабатывании буфер очищается.</summary>
    private static (Rule? Rule, int FiredAt) Feed(
        SnippetMatcher matcher, WordBuffer buffer, KeyStroke[] keys, Func<nint>? layout = null)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            buffer.Push(keys[i]);
            if (matcher.TryMatch(buffer, layout, out var rule))
            {
                buffer.Clear();
                return (rule, i);
            }
        }

        return (null, -1);
    }

    /// <summary>Компилирует набор и проверяет, что стартовый набор собирается без предупреждений.</summary>
    private static SnippetMatcher Compile(IReadOnlyList<Snippet> snippets)
    {
        var result = SnippetCompiler.Compile(snippets, _tables);
        Assert.Empty(result.Warnings);
        return result.Matcher;
    }

    /// <summary>Читает стартовый набор из тестовых данных.</summary>
    private static IReadOnlyList<Snippet> LoadStarter()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "snippets.json"));
        return SettingsJson.Parse(json).Settings!.Snippets;
    }
}
