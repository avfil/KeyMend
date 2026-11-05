using System.Xml.Linq;
using KeyMend.Infrastructure;

namespace KeyMend.Tests;

/// <summary>Тесты чистой части <see cref="AdminAutostart"/>: имена, пути, XML задачи Планировщика и проверка аргументов помощника (SPEC 2.6).</summary>
public sealed class AdminAutostartTests
{
    /// <summary>SID для XML задачи.</summary>
    internal const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    /// <summary>Путь к exe копии для XML задачи.</summary>
    internal const string Exe = @"C:\Program Files\KeyMend\tester\KeyMend.exe";

    /// <summary>Пространство имён XML задач Планировщика.</summary>
    private static readonly XNamespace _ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>Из имени пользователя убирается всё, что опасно в пути и имени задачи.</summary>
    [Theory]
    [InlineData("tester", "tester")]
    [InlineData("Иван Петров", "Иван_Петров")]
    [InlineData(@"..\evil", ".._evil")]
    [InlineData("a/b:c*d", "a_b_c_d")]
    [InlineData("", "user")]
    public void SafeName_UnsafeCharacters_AreReplaced(string input, string expected)
    {
        Assert.Equal(expected, AdminAutostart.SafeName(input));
    }

    /// <summary>У каждого пользователя своя задача.</summary>
    [Fact]
    public void TaskName_DifferentUsers_DiffersPerUser()
    {
        Assert.Equal("KeyMend-tester", AdminAutostart.TaskName("tester"));
        Assert.NotEqual(AdminAutostart.TaskName("tester"), AdminAutostart.TaskName("other"));
    }

    /// <summary>Копия лежит в Program Files, в папке пользователя.</summary>
    [Fact]
    public void InstallDirectory_Always_IsUnderProgramFilesPerUser()
    {
        var dir = AdminAutostart.InstallDirectory("tester");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Assert.StartsWith(programFiles, dir, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"KeyMend\tester", dir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Имя пользователя не позволяет выйти из папки программы.</summary>
    [Fact]
    public void InstallDirectory_PathTraversalInName_CannotEscape()
    {
        var dir = AdminAutostart.InstallDirectory(@"..\..\Windows");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Assert.StartsWith(Path.Combine(programFiles, "KeyMend"), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>XML задачи корректен и объявлен как UTF-16.</summary>
    [Fact]
    public void BuildTaskXml_Always_IsValidXmlWithUtf16Declaration()
    {
        var xml = AdminAutostart.BuildTaskXml(Sid, Exe);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-16\"?>", xml, StringComparison.Ordinal);
        Assert.NotNull(XDocument.Parse(xml).Root);
    }

    /// <summary>Задача идёт с наивысшими правами и только для этого пользователя.</summary>
    [Fact]
    public void BuildTaskXml_Always_RunsWithHighestPrivilegesForThisUserOnly()
    {
        var root = RootOf(AdminAutostart.BuildTaskXml(Sid, Exe));

        Assert.Equal("HighestAvailable", root.Descendants(_ns + "RunLevel").Single().Value);
        Assert.Equal("InteractiveToken", root.Descendants(_ns + "LogonType").Single().Value);

        // Триггер входа и субъект задачи привязаны к SID владельца, а не к «всем пользователям».
        Assert.Equal(Sid, root.Descendants(_ns + "LogonTrigger").Single().Element(_ns + "UserId")?.Value);
        Assert.Equal(Sid, root.Descendants(_ns + "Principal").Single().Element(_ns + "UserId")?.Value);
    }

    /// <summary>Задача запускает копию из заданного пути, путь в кавычках.</summary>
    [Fact]
    public void BuildTaskXml_Always_LaunchesQuotedExeFromGivenPath()
    {
        var exec = RootOf(AdminAutostart.BuildTaskXml(Sid, Exe)).Descendants(_ns + "Exec").Single();

        Assert.Equal($"\"{Exe}\"", exec.Element(_ns + "Command")?.Value);
        Assert.Equal(@"C:\Program Files\KeyMend\tester", exec.Element(_ns + "WorkingDirectory")?.Value);
    }

    /// <summary>У задачи нет ограничения по времени, она не останавливается на батарее.</summary>
    [Fact]
    public void BuildTaskXml_Always_HasNoTimeLimitAndIgnoresBattery()
    {
        var settings = RootOf(AdminAutostart.BuildTaskXml(Sid, Exe)).Element(_ns + "Settings");

        Assert.Equal("PT0S", settings?.Element(_ns + "ExecutionTimeLimit")?.Value);
        Assert.Equal("false", settings?.Element(_ns + "StopIfGoingOnBatteries")?.Value);
        Assert.Equal("false", settings?.Element(_ns + "DisallowStartIfOnBatteries")?.Value);
        Assert.Equal("IgnoreNew", settings?.Element(_ns + "MultipleInstancesPolicy")?.Value);
    }

    /// <summary>Неверные аргументы помощника отвергаются до любых действий.</summary>
    [Fact]
    public void RunHelper_BadArguments_ReturnsErrorWithoutTouchingAnything()
    {
        var first = AdminAutostart.HelperArgument;

        Assert.Equal(AdminAutostart.ExitBadArguments, AdminAutostart.RunHelper([first], new TestLog()));
        Assert.Equal(AdminAutostart.ExitBadArguments, AdminAutostart.RunHelper([first, "unknown"], new TestLog()));
        Assert.Equal(AdminAutostart.ExitBadArguments, AdminAutostart.RunHelper([first, "install", "only-one-arg"], new TestLog()));
    }

    /// <summary>Установка из несуществующей папки отвергается.</summary>
    [Fact]
    public void RunHelper_InstallWithMissingSource_ReturnsBadArguments()
    {
        var missing = Path.Combine(Path.GetTempPath(), "KeyMendTests_" + Guid.NewGuid().ToString("N"));

        Assert.Equal(
            AdminAutostart.ExitBadArguments,
            AdminAutostart.RunHelper([AdminAutostart.HelperArgument, "install", missing, "tester", Sid], new TestLog()));
    }

    /// <summary>Установка из папки без exe отвергается.</summary>
    [Fact]
    public void RunHelper_InstallWithSourceWithoutExe_ReturnsBadArguments()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KeyMendTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(
                AdminAutostart.ExitBadArguments,
                AdminAutostart.RunHelper([AdminAutostart.HelperArgument, "install", dir, "tester", Sid], new TestLog()));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Разбирает XML и возвращает корневой элемент.</summary>
    private static XElement RootOf(string xml) => XDocument.Parse(xml).Root ?? throw new InvalidOperationException("no root");
}
