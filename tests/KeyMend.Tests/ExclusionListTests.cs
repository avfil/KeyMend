using KeyMend.Infrastructure;
using KeyMend.Text;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Правка списка исключений и форма слова «программа» в счётчике.</summary>
public sealed class ExclusionListTests
{
    /// <summary>Новая программа добавляется со всеми включёнными функциями.</summary>
    [Fact]
    public void Add_NewName_AddsEntryWithEverythingWorking()
    {
        var result = ExclusionList.Add([], ["notepad"]);

        var entry = Assert.Single(result.List);
        Assert.Equal("notepad.exe", entry.Process);
        Assert.True(entry.LayoutSwitch && entry.Correction && entry.Snippets && entry.Rules);
        Assert.Equal(["notepad.exe"], result.Added);
    }

    /// <summary>Повтор без учёта регистра не добавляется и попадает в список повторов.</summary>
    [Fact]
    public void Add_SameNameOtherCase_ReportsDuplicate()
    {
        var list = ExclusionList.Add([], ["chrome.exe"]).List;

        var result = ExclusionList.Add(list, ["CHROME.EXE", @"C:\Apps\Chrome.exe"]);

        Assert.Single(result.List);
        Assert.Empty(result.Added);
        Assert.Equal(2, result.Duplicates.Count);
    }

    /// <summary>Недопустимое имя молча пропускается: проверку и сообщение делает страница.</summary>
    [Fact]
    public void Add_InvalidName_Skipped()
    {
        var result = ExclusionList.Add([], ["a*b", "  "]);

        Assert.Empty(result.List);
        Assert.Empty(result.Duplicates);
    }

    /// <summary>Удаление не зависит от регистра и не трогает остальных.</summary>
    [Fact]
    public void Remove_ExistingProcess_RemovesOnlyIt()
    {
        var list = ExclusionList.Add([], ["a.exe", "b.exe"]).List;

        var result = ExclusionList.Remove(list, "A.EXE");

        Assert.Equal("b.exe", Assert.Single(result).Process);
    }

    /// <summary>Галочка меняет одну функцию одной программы.</summary>
    [Fact]
    public void Set_OneFeature_ChangesOnlyIt()
    {
        var list = ExclusionList.Add([], ["a.exe", "b.exe"]).List;

        var result = ExclusionList.Set(list, "a.exe", ExclusionFeature.Correction, works: false);

        Assert.False(result[0].Correction);
        Assert.True(result[0].LayoutSwitch && result[0].Snippets && result[0].Rules);
        Assert.True(result[1].Correction);
    }

    /// <summary>Каждая функция читается из своего поля.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Works_AfterSetFalse_ReadsSameFeature(int featureIndex)
    {
        var feature = (ExclusionFeature)featureIndex;
        var list = ExclusionList.Set(ExclusionList.Add([], ["a.exe"]).List, "a.exe", feature, works: false);

        Assert.False(ExclusionList.Works(list[0], feature));
        foreach (var other in Enum.GetValues<ExclusionFeature>().Where(f => f != feature))
        {
            Assert.True(ExclusionList.Works(list[0], other));
        }
    }

    /// <summary>Кандидаты: без уже добавленных и самого KeyMend, по алфавиту, с отбором по тексту.</summary>
    [Fact]
    public void Candidates_Filtered_SkipsExistingAndOwnAndSorts()
    {
        RunningApp[] running =
        [
            new("devenv.exe", "Microsoft Visual Studio"), new("chrome.exe", "Google Chrome"), new("KeyMend.exe", null),
            new("notepad.exe", "Блокнот"), new("Telegram.exe", null),
        ];
        var existing = ExclusionList.Add([], ["notepad.exe"]).List;

        var all = ExclusionList.Candidates(running, existing, "keymend.exe", null);
        var byDescription = ExclusionList.Candidates(running, existing, "keymend.exe", "visual");

        Assert.Equal(["chrome.exe", "devenv.exe", "Telegram.exe"], all.Select(a => a.Exe));
        Assert.Equal("devenv.exe", Assert.Single(byDescription).Exe);
    }

    /// <summary>Формы слова по числу.</summary>
    [Theory]
    [InlineData(0, "программ")]
    [InlineData(1, "программа")]
    [InlineData(2, "программы")]
    [InlineData(4, "программы")]
    [InlineData(5, "программ")]
    [InlineData(11, "программ")]
    [InlineData(14, "программ")]
    [InlineData(21, "программа")]
    [InlineData(22, "программы")]
    [InlineData(25, "программ")]
    [InlineData(111, "программ")]
    [InlineData(101, "программа")]
    public void Pick_Count_ReturnsRussianForm(int count, string expected) =>
        Assert.Equal(expected, RussianPlural.Pick(count, "программа", "программы", "программ"));
}
