using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Чистая логика шагов в редакторе правила: каталог, порядок, проверка, выбор файла, правка сочетания.</summary>
public sealed class StepEditingTests
{
    /// <summary>Папка скриптов для проверок.</summary>
    private const string _scripts = @"C:\Test\scripts";

    /// <summary>Каждый вид создаётся и узнаётся обратно.</summary>
    [Fact]
    public void Create_EveryKind_RoundTripsThroughKindOf()
    {
        foreach (var kind in StepCatalog.All)
        {
            Assert.Equal(kind, StepCatalog.KindOf(StepCatalog.Create(kind)));
        }
    }

    /// <summary>Первый шаг группы соответствует группе.</summary>
    [Theory]
    [InlineData("Language", "SwitchLayout")]
    [InlineData("Correction", "Correct")]
    [InlineData("Launch", "Launch")]
    [InlineData("Power", "Power")]
    public void FirstStep_Group_MatchesKind(string group, string kind) =>
        Assert.Equal(Enum.Parse<StepKind>(kind), StepCatalog.KindOf(StepCatalog.FirstStep(Enum.Parse<RuleGroup>(group))));

    /// <summary>Новые шаги без обязательных полей не проходят проверку.</summary>
    [Theory]
    [InlineData("Launch", "NoFile")]
    [InlineData("Press", "NoKeys")]
    [InlineData("InsertText", "NoText")]
    [InlineData("Power", "None")]
    [InlineData("Delay", "None")]
    public void Check_NewStep_ReportsMissingField(string kind, string expected) =>
        Assert.Equal(Enum.Parse<StepProblem>(expected), StepValidation.Check(StepCatalog.Create(Enum.Parse<StepKind>(kind)), _scripts));

    /// <summary>Пара из одинаковых языков — ошибка и у «Переключить язык», и у «Исправить»; режим «один язык» пары не проверяет.</summary>
    [Fact]
    public void Check_SameLanguages_OnlyForPairs()
    {
        Assert.Equal(StepProblem.SameLanguages, StepValidation.Check(new SwitchLayoutStep { Pair = ["ru-RU", "RU-ru"] }, _scripts));
        Assert.Equal(StepProblem.SameLanguages, StepValidation.Check(new CorrectStep { Scope = CorrectScope.Line, Pair = ["en-US", "en-US"] }, _scripts));
        Assert.Equal(StepProblem.None, StepValidation.Check(new SwitchLayoutStep { Mode = SwitchMode.Specific, Pair = ["ru-RU", "ru-RU"] }, _scripts));
    }

    /// <summary>Файл: кавычки и выход из папки скриптов — ошибка, имя из PATH и полный путь — нет.</summary>
    [Theory]
    [InlineData("notepad.exe", "None")]
    [InlineData(@"C:\Tools\a.exe", "None")]
    [InlineData(@"tools\a.ps1", "None")]
    [InlineData(@"..\a.ps1", "BadFile")]
    [InlineData("a\".exe", "BadFile")]
    [InlineData("   ", "NoFile")]
    public void Check_LaunchFile_Validated(string file, string expected) =>
        Assert.Equal(Enum.Parse<StepProblem>(expected), StepValidation.Check(new LaunchStep { File = file }, _scripts));

    /// <summary>Секретный шаг без текста допустим: текст лежит в хранилище.</summary>
    [Fact]
    public void Check_SecretInsert_NoTextIsFine() =>
        Assert.Equal(StepProblem.None, StepValidation.Check(new InsertTextStep { SecretRef = "k" }, _scripts));

    /// <summary>Сдвиг меняет соседей местами и не выходит за края.</summary>
    [Fact]
    public void Move_WithinBounds_SwapsAndIgnoresEdges()
    {
        Step a = new DelayStep { Ms = 1 };
        Step b = new DelayStep { Ms = 2 };
        Step c = new DelayStep { Ms = 3 };
        IReadOnlyList<Step> list = [a, b, c];

        Assert.Equal([b, a, c], StepListOps.Move(list, 0, 1));
        Assert.Equal([a, c, b], StepListOps.Move(list, 2, -1));
        Assert.Same(list, StepListOps.Move(list, 0, -1));
        Assert.Same(list, StepListOps.Move(list, 2, 1));
        Assert.Same(list, StepListOps.Move(list, 5, -1));
    }

    /// <summary>Удаление убирает один шаг; неверный номер ничего не меняет.</summary>
    [Fact]
    public void RemoveAt_Index_RemovesOne()
    {
        Step a = new DelayStep { Ms = 1 };
        Step b = new DelayStep { Ms = 2 };
        IReadOnlyList<Step> list = [a, b];

        Assert.Equal([b], StepListOps.RemoveAt(list, 0));
        Assert.Same(list, StepListOps.RemoveAt(list, 2));
        Assert.Empty(StepListOps.RemoveAt([a], 0));
    }

    /// <summary>Файл внутри папки скриптов хранится относительным путём, снаружи — полным.</summary>
    [Fact]
    public void ToStored_InsideScripts_IsRelative()
    {
        Assert.Equal(@"tools\a.ps1", LaunchFileChoice.ToStored(@"C:\Test\scripts\tools\a.ps1", _scripts));
        Assert.Equal(@"C:\Other\a.ps1", LaunchFileChoice.ToStored(@"C:\Other\a.ps1", _scripts));
        Assert.Equal(@"C:\Test\scripts2\a.ps1", LaunchFileChoice.ToStored(@"C:\Test\scripts2\a.ps1", _scripts));
    }

    /// <summary>Диалог открывается в рабочей папке, а если её нет — в папке файла, иначе в папке скриптов.</summary>
    [Fact]
    public void StartDirectory_Candidates_PicksFirstExisting()
    {
        Func<string, bool> all = _ => true;
        Func<string, bool> none = _ => false;

        Assert.Equal(@"D:\Work", LaunchFileChoice.StartDirectory(_scripts, all, (@"D:\Work", false), (@"C:\Git\a.exe", true)));
        Assert.Equal(@"C:\Git", LaunchFileChoice.StartDirectory(_scripts, all, (string.Empty, false), (@"C:\Git\a.exe", true)));
        Assert.Equal(@"C:\Git", LaunchFileChoice.StartDirectory(_scripts, d => d == @"C:\Git", (@"D:\Gone", false), (@"C:\Git\a.exe", true)));
        Assert.Equal(_scripts, LaunchFileChoice.StartDirectory(_scripts, all, (string.Empty, false), ("a.ps1", true)));
        Assert.Equal(_scripts, LaunchFileChoice.StartDirectory(_scripts, none, (@"D:\Work", false), (@"C:\Git\a.exe", true)));
    }

    /// <summary>Тип запуска по расширению; для .ps1 выбранный PowerShell сохраняется.</summary>
    [Theory]
    [InlineData(".EXE", "Cmd", "Exe")]
    [InlineData(".ps1", "Exe", "Pwsh")]
    [InlineData(".ps1", "PowerShell", "PowerShell")]
    [InlineData(".bat", "Exe", "Cmd")]
    [InlineData(".cmd", "Exe", "Cmd")]
    [InlineData(".txt", "Pwsh", "Pwsh")]
    public void TypeFor_Extension_PicksInterpreter(string extension, string current, string expected) =>
        Assert.Equal(Enum.Parse<LaunchType>(expected), LaunchFileChoice.TypeFor(extension, Enum.Parse<LaunchType>(current)));

    /// <summary>Модификатор включается как «любой» и снимается целиком, даже если был левым.</summary>
    [Fact]
    public void ToggleModifier_OnAndOff_KeepsKey()
    {
        var chord = new KeyChord(ModifierSet.LCtrl, VirtualKey.Return);

        Assert.True(ChordEdit.HasModifier(chord, ModifierSet.AnyCtrl));
        Assert.False(ChordEdit.HasModifier(chord, ModifierSet.AnyShift));
        Assert.Equal(new KeyChord(ModifierSet.None, VirtualKey.Return), ChordEdit.ToggleModifier(chord, ModifierSet.AnyCtrl));
        Assert.Equal(new KeyChord(ModifierSet.LCtrl | ModifierSet.AnyShift, VirtualKey.Return), ChordEdit.ToggleModifier(chord, ModifierSet.AnyShift));
    }

    /// <summary>Клавиша меняется, модификаторы остаются; запись читается обратно разбором.</summary>
    [Fact]
    public void SetKey_KeepsModifiers()
    {
        var chord = ChordEdit.SetKey(ChordEdit.ToggleModifier(default, ModifierSet.AnyCtrl), VirtualKey.Return);

        Assert.Equal("Ctrl+Enter", KeyChordParser.Format(chord));
        Assert.True(KeyChordParser.TryParse("Ctrl+Enter", out var parsed));
        Assert.Equal(chord, parsed);
    }
}
