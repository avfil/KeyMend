using KeyMend.Actions;
using KeyMend.Rules;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="LaunchCommandBuilder"/>: пути, интерпретаторы и аргументы шага «Запустить программу» (SPEC F4.1).</summary>
public sealed class LaunchCommandBuilderTests
{
    /// <summary>Папка скриптов теста.</summary>
    private const string _scripts = @"C:\Data\KeyMend\scripts";

    /// <summary>Файлы, которые «существуют» в тесте.</summary>
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Data\KeyMend\scripts\tools\Monitor.exe",
        @"C:\Data\KeyMend\scripts\fix.ps1",
        @"C:\Data\KeyMend\scripts\run.cmd",
        @"C:\Tools\bin\app.exe",
        @"C:\Program Files\PowerShell\7\pwsh.exe",
    };

    /// <summary>Создаёт окружение теста.</summary>
    private LaunchEnvironment Env(params string[] pathDirs) => new(
        _scripts,
        @"C:\Windows\System32",
        @"C:\Program Files",
        pathDirs,
        _files.Contains,
        dir => dir.StartsWith(@"C:\", StringComparison.OrdinalIgnoreCase));

    /// <summary>Собирает команду или проваливает тест с причиной.</summary>
    private LaunchCommand Build(LaunchStep step, params string[] pathDirs)
    {
        Assert.True(LaunchCommandBuilder.TryBuild(step, Env(pathDirs), out var command, out var error), error);
        return command;
    }

    /// <summary>Возвращает причину отказа.</summary>
    private string Reject(LaunchStep step, params string[] pathDirs)
    {
        Assert.False(LaunchCommandBuilder.TryBuild(step, Env(pathDirs), out _, out var error));
        return error;
    }

    /// <summary>Программа из папки скриптов запускается напрямую, рабочая папка — папка файла.</summary>
    [Fact]
    public void TryBuild_RelativeExe_RunsDirectlyInFileFolder()
    {
        var command = Build(new LaunchStep { File = @"tools\Monitor.exe", Arguments = "/SetOrientation 1 90", Window = LaunchWindow.Hidden });

        Assert.Equal(@"C:\Data\KeyMend\scripts\tools\Monitor.exe", command.FileName);
        Assert.Equal("/SetOrientation 1 90", command.Arguments);
        Assert.Equal(@"C:\Data\KeyMend\scripts\tools", command.WorkingDirectory);
        Assert.Equal(LaunchWindow.Hidden, command.Window);
    }

    /// <summary>Голое имя программы ищется в PATH, расширение <c>.exe</c> добавляется.</summary>
    [Fact]
    public void TryBuild_BareName_SearchesPath()
    {
        var command = Build(new LaunchStep { File = "app" }, @"C:\Nowhere", @"C:\Tools\bin");

        Assert.Equal(@"C:\Tools\bin\app.exe", command.FileName);
    }

    /// <summary>Полный путь используется как есть.</summary>
    [Fact]
    public void TryBuild_FullPath_IsUsedAsIs() =>
        Assert.Equal(@"C:\Tools\bin\app.exe", Build(new LaunchStep { File = @"C:\Tools\bin\app.exe" }).FileName);

    /// <summary>Windows PowerShell запускается с ключами из SPEC F4.1.</summary>
    [Fact]
    public void TryBuild_PowerShell_UsesWindowsPowerShellWithBypass()
    {
        var command = Build(new LaunchStep { Type = LaunchType.PowerShell, File = "fix.ps1", Arguments = "-Mode fast" });

        Assert.Equal(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", command.FileName);
        Assert.Equal(@"-NoProfile -ExecutionPolicy Bypass -File ""C:\Data\KeyMend\scripts\fix.ps1"" -Mode fast", command.Arguments);
    }

    /// <summary>PowerShell 7 ищется в PATH, затем в Program Files.</summary>
    [Fact]
    public void TryBuild_Pwsh_FallsBackToProgramFiles()
    {
        var command = Build(new LaunchStep { Type = LaunchType.Pwsh, File = "fix.ps1" });

        Assert.Equal(@"C:\Program Files\PowerShell\7\pwsh.exe", command.FileName);
        Assert.Equal(@"-NoProfile -ExecutionPolicy Bypass -File ""C:\Data\KeyMend\scripts\fix.ps1""", command.Arguments);
    }

    /// <summary>Без установленного PowerShell 7 шаг отклоняется.</summary>
    [Fact]
    public void TryBuild_PwshMissing_IsRejected()
    {
        _files.Remove(@"C:\Program Files\PowerShell\7\pwsh.exe");

        Assert.Equal("pwsh.exe is not found", Reject(new LaunchStep { Type = LaunchType.Pwsh, File = "fix.ps1" }));
    }

    /// <summary>Пакетный файл идёт через <c>cmd.exe /c</c> с внешними кавычками.</summary>
    [Fact]
    public void TryBuild_Cmd_UsesCmdSlashC()
    {
        var command = Build(new LaunchStep { Type = LaunchType.Cmd, File = "run.cmd", Arguments = "a b" });

        Assert.Equal(@"C:\Windows\System32\cmd.exe", command.FileName);
        Assert.Equal(@"/c """"C:\Data\KeyMend\scripts\run.cmd"" a b""", command.Arguments);
    }

    /// <summary>Относительный путь не может выйти из папки скриптов.</summary>
    [Theory]
    [InlineData(@"..\evil.exe")]
    [InlineData(@"tools\..\..\evil.exe")]
    [InlineData(@"\Windows\notepad.exe")]
    public void TryBuild_PathEscapesScripts_IsRejected(string file) =>
        Assert.Equal("path is outside the scripts folder", Reject(new LaunchStep { File = file }));

    /// <summary>Несуществующий файл, пустое имя и кавычки в имени отклоняются.</summary>
    [Fact]
    public void TryBuild_InvalidFile_IsRejected()
    {
        Assert.Equal("file is not found", Reject(new LaunchStep { File = "missing.exe" }));
        Assert.Equal("file is not found", Reject(new LaunchStep { Type = LaunchType.Cmd, File = "missing.cmd" }));
        Assert.Equal("file is not set", Reject(new LaunchStep { File = "  " }));
        Assert.Equal("file name has invalid characters", Reject(new LaunchStep { File = "a\".exe" }));
    }

    /// <summary>Рабочая папка из шага: относительная считается от папки скриптов, несуществующая отклоняется.</summary>
    [Fact]
    public void TryBuild_WorkingDirectory_IsResolved()
    {
        var command = Build(new LaunchStep { File = "fix.ps1", Type = LaunchType.PowerShell, WorkingDirectory = "tools" });
        Assert.Equal(@"C:\Data\KeyMend\scripts\tools", command.WorkingDirectory);

        Assert.Equal("working directory is not found", Reject(new LaunchStep { File = "fix.ps1", WorkingDirectory = @"D:\none" }));
    }
}
