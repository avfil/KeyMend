using KeyMend.Actions;
using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage.Transfer;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="ScriptMerge"/>: файлы скриптов при загрузке и поиск ненайденных программ.</summary>
public sealed class ScriptMergeTests
{
    /// <summary>Файл скриптов из архива.</summary>
    private static ScriptFile File(string path, params byte[] content) => new(path, content);

    /// <summary>«Заменить всё»: все файлы записываются, даже если у владельца другие.</summary>
    [Fact]
    public void Plan_Replace_WritesEverything()
    {
        var plan = ScriptMerge.Plan([File("a.cmd", 1), File("b.cmd", 2)], _ => [9], ImportMode.Replace);

        Assert.Equal(2, plan.ToWrite.Count);
        Assert.Empty(plan.Skipped);
    }

    /// <summary>«Добавить»: новые пишутся, одинаковые не трогаются, отличающиеся остаются у владельца и попадают в список.</summary>
    [Fact]
    public void Plan_Merge_WritesNewKeepsDifferentAndSkipsSame()
    {
        var existing = new Dictionary<string, byte[]> { ["same.cmd"] = [1], ["other.cmd"] = [2] };

        var plan = ScriptMerge.Plan(
            [File("new.cmd", 3), File("same.cmd", 1), File("other.cmd", 9)],
            path => existing.GetValueOrDefault(path),
            ImportMode.Merge);

        Assert.Equal(["new.cmd"], plan.ToWrite.Select(f => f.RelativePath));
        Assert.Equal(["other.cmd"], plan.Skipped);
    }

    /// <summary>Правило с запуском файла, которого нет, попадает в список; остальные нет.</summary>
    [Fact]
    public void FindMissingPrograms_FileAbsent_ListsRuleName()
    {
        var env = new LaunchEnvironment(
            @"C:\Data\scripts", @"C:\Windows\System32", @"C:\Program Files", [], path => path == @"C:\Data\scripts\ok.cmd", _ => true);
        Rule Make(string name, string file) => new()
        {
            Name = name,
            Trigger = new PressTrigger { Key = (VirtualKey)0x7E },
            Steps = [new LaunchStep { Type = LaunchType.Cmd, File = file }],
        };

        var missing = ScriptMerge.FindMissingPrograms([Make("Есть", "ok.cmd"), Make("Нет", "gone.cmd")], env);

        Assert.Equal(["Нет"], missing);
    }
}
