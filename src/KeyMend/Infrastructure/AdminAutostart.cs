using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace KeyMend.Infrastructure;

/// <summary>Итог операции режима администратора.</summary>
internal enum AdminAutostartResult
{
    /// <summary>Операция выполнена.</summary>
    Success,

    /// <summary>Владелец отказался от запроса UAC.</summary>
    Cancelled,

    /// <summary>Операция не удалась; подробности в логе.</summary>
    Failed,
}

/// <summary>
/// Автозапуск с правами администратора (SPEC 2.6): задача Планировщика, привязанная к одному пользователю, запускает
/// <b>копию</b> программы из <c>Program Files</c>. Копия нужна, чтобы задача с наивысшими правами не указывала на
/// записываемую пользователем папку. Установка и удаление идут через повышенного помощника — собственный exe с
/// аргументом <see cref="HelperArgument"/>, запущенный через <c>runas</c>.
/// </summary>
internal sealed class AdminAutostart
{
    /// <summary>Первый аргумент командной строки повышенного помощника.</summary>
    internal const string HelperArgument = "--admin-autostart";

    /// <summary>Имя exe программы в копии.</summary>
    internal const string ExeName = "KeyMend.exe";

    /// <summary>Код ошибки Windows «операция отменена пользователем» (отказ от UAC).</summary>
    internal const int ErrorCancelled = 1223;

    /// <summary>Код выхода помощника: недопустимые аргументы.</summary>
    internal const int ExitBadArguments = 2;

    /// <summary>Код выхода помощника: не удалось скопировать файлы.</summary>
    internal const int ExitCopyFailed = 3;

    /// <summary>Код выхода помощника: не удалось создать или удалить задачу.</summary>
    internal const int ExitTaskFailed = 4;

    /// <summary>Сколько ждать <c>schtasks.exe</c>, мс.</summary>
    internal const int TaskCommandTimeoutMs = 10_000;

    /// <summary>Журнал.</summary>
    private readonly ILog _log;

    /// <summary>Имя пользователя Windows (без домена).</summary>
    private readonly string _userName;

    /// <summary>Идентификатор безопасности пользователя.</summary>
    private readonly string _sid;

    /// <summary>Создаёт объект для текущего пользователя.</summary>
    /// <param name="log">Журнал.</param>
    public AdminAutostart(ILog log)
    {
        _log = log;
        using var identity = WindowsIdentity.GetCurrent();
        _userName = Environment.UserName;
        _sid = identity.User?.Value ?? string.Empty;
    }

    /// <summary>Имя задачи Планировщика для пользователя: у каждого пользователя на компьютере своя.</summary>
    /// <param name="userName">Имя пользователя.</param>
    /// <returns>Имя задачи вида <c>KeyMend-имя</c>.</returns>
    public static string TaskName(string userName) => "KeyMend-" + SafeName(userName);

    /// <summary>Каталог копии: <c>%ProgramFiles%\KeyMend\&lt;пользователь&gt;</c>.</summary>
    /// <param name="userName">Имя пользователя.</param>
    /// <returns>Полный путь к каталогу.</returns>
    public static string InstallDirectory(string userName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KeyMend", SafeName(userName));

    /// <summary>Заменяет в имени всё, кроме букв, цифр, точки, дефиса и подчёркивания: имя идёт в путь и в имя задачи.</summary>
    /// <param name="userName">Имя пользователя.</param>
    /// <returns>Безопасное имя; «user», если имя пустое.</returns>
    public static string SafeName(string userName)
    {
        var chars = userName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray();
        return chars.Length == 0 ? "user" : new string(chars);
    }

    /// <summary>
    /// Собирает XML задачи Планировщика: вход только этого пользователя, наивысшие права, без ограничения времени
    /// и без остановки при работе от батареи.
    /// </summary>
    /// <param name="sid">SID пользователя: и триггер, и субъект задачи привязаны к нему.</param>
    /// <param name="exePath">Полный путь к exe копии в <c>Program Files</c>.</param>
    /// <returns>Текст XML с объявлением UTF-16.</returns>
    public static string BuildTaskXml(string sid, string exePath)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var task = new XElement(
            ns + "Task",
            new XAttribute("version", "1.2"),
            new XElement(ns + "RegistrationInfo", new XElement(ns + "Description", "KeyMend: autostart with administrator rights")),
            new XElement(
                ns + "Triggers",
                new XElement(ns + "LogonTrigger", new XElement(ns + "Enabled", "true"), new XElement(ns + "UserId", sid))),
            new XElement(
                ns + "Principals",
                new XElement(
                    ns + "Principal",
                    new XAttribute("id", "Author"),
                    new XElement(ns + "UserId", sid),
                    new XElement(ns + "LogonType", "InteractiveToken"),
                    new XElement(ns + "RunLevel", "HighestAvailable"))),
            new XElement(
                ns + "Settings",
                new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(ns + "StopIfGoingOnBatteries", "false"),
                new XElement(ns + "AllowHardTerminate", "true"),
                new XElement(ns + "StartWhenAvailable", "false"),
                new XElement(ns + "AllowStartOnDemand", "true"),
                new XElement(ns + "Enabled", "true"),
                new XElement(ns + "Hidden", "false"),
                new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                new XElement(ns + "Priority", "7")),
            new XElement(
                ns + "Actions",
                new XAttribute("Context", "Author"),
                new XElement(
                    ns + "Exec",
                    new XElement(ns + "Command", $"\"{exePath}\""),
                    new XElement(ns + "WorkingDirectory", Path.GetDirectoryName(exePath)))));

        // XDocument.ToString() опускает объявление, а Планировщик ждёт его (кодировка UTF-16, как у файла).
        return new XDeclaration("1.0", "UTF-16", null) + Environment.NewLine + task;
    }

    /// <summary>
    /// Выполняет работу повышенного помощника. Вызывается из <c>App.OnStartup</c>, когда первый аргумент —
    /// <see cref="HelperArgument"/>. Возвращает код выхода процесса: 0 — успех.
    /// </summary>
    /// <param name="args">Аргументы командной строки: <c>install &lt;каталог&gt; &lt;пользователь&gt; &lt;sid&gt;</c> или <c>remove &lt;пользователь&gt;</c>.</param>
    /// <param name="log">Журнал.</param>
    /// <returns>Код выхода: 0, <see cref="ExitBadArguments"/>, <see cref="ExitCopyFailed"/> или <see cref="ExitTaskFailed"/>.</returns>
    public static int RunHelper(string[] args, ILog log)
    {
        try
        {
            return args switch
            {
                [_, "install", var source, var user, var sid] => Install(source, user, sid, log),
                [_, "remove", var user] => Remove(user, log),
                _ => ExitBadArguments,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or Win32Exception)
        {
            log.Error("admin autostart helper failed", ex);
            return ExitCopyFailed;
        }
    }

    /// <summary>Есть ли задача автозапуска этого пользователя. Запрос не требует повышения прав.</summary>
    /// <returns><c>true</c>, если задача существует.</returns>
    public Task<bool> ExistsAsync() => Task.Run(() => RunSchtasks($"/Query /TN \"{TaskName(_userName)}\"", _log) == 0);

    /// <summary>Просит Планировщик запустить задачу сейчас (в повышенном режиме, без запроса UAC).</summary>
    /// <returns><c>true</c>, если задача запущена.</returns>
    public Task<bool> StartNowAsync() => Task.Run(() => RunSchtasks($"/Run /TN \"{TaskName(_userName)}\"", _log) == 0);

    /// <summary>Запускает повышенного помощника, который копирует программу в Program Files и создаёт задачу (запрос UAC).</summary>
    /// <returns>Итог операции.</returns>
    public Task<AdminAutostartResult> InstallAsync() =>
        RunHelperAsync($"{HelperArgument} install {Quote(AppContext.BaseDirectory.TrimEnd('\\'))} {Quote(_userName)} {Quote(_sid)}");

    /// <summary>Запускает повышенного помощника, который удаляет задачу и копию (запрос UAC).</summary>
    /// <returns>Итог операции.</returns>
    public Task<AdminAutostartResult> RemoveAsync() => RunHelperAsync($"{HelperArgument} remove {Quote(_userName)}");

    /// <summary>Копирует программу в <c>Program Files</c> и создаёт задачу. Выполняется только повышенным помощником.</summary>
    private static int Install(string sourceDirectory, string userName, string sid, ILog log)
    {
        if (!Directory.Exists(sourceDirectory) || !File.Exists(Path.Combine(sourceDirectory, ExeName)) || sid.Length == 0)
        {
            log.Error("admin autostart: invalid install arguments");
            return ExitBadArguments;
        }

        var target = InstallDirectory(userName);
        CopyDirectory(sourceDirectory, target);

        var xmlPath = Path.Combine(Path.GetTempPath(), $"keymend-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, BuildTaskXml(sid, Path.Combine(target, ExeName)), Encoding.Unicode);
            var code = RunSchtasks($"/Create /TN \"{TaskName(userName)}\" /XML \"{xmlPath}\" /F", log);
            log.Info(code == 0 ? "admin autostart installed" : $"admin autostart: schtasks create failed ({code})");
            return code == 0 ? 0 : ExitTaskFailed;
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    /// <summary>Удаляет задачу и копию. Отсутствие задачи не считается ошибкой.</summary>
    private static int Remove(string userName, ILog log)
    {
        var name = TaskName(userName);
        if (RunSchtasks($"/Query /TN \"{name}\"", log) == 0 && RunSchtasks($"/Delete /TN \"{name}\" /F", log) != 0)
        {
            log.Error("admin autostart: schtasks delete failed");
            return ExitTaskFailed;
        }

        try
        {
            var target = InstallDirectory(userName);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            // Общая папка KeyMend в Program Files остаётся пустой после копии одного пользователя: убираем её, если в ней больше ничего нет.
            if (Path.GetDirectoryName(target) is { } parent && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                Directory.Delete(parent);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Файлы занимает запущенный экземпляр: задача уже удалена, остаток копии не мешает.
            log.Error("admin autostart: copy not fully removed", ex);
        }

        log.Info("admin autostart removed");
        return 0;
    }

    /// <summary>Рекурсивно копирует каталог с перезаписью файлов.</summary>
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    /// <summary>Запускает <c>schtasks.exe</c> без окна и возвращает код выхода (−1 при таймауте или ошибке запуска).</summary>
    private static int RunSchtasks(string arguments, ILog log)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                return -1;
            }

            // Вывод не нужен, но его надо вычитывать, иначе процесс может зависнуть на заполненном канале.
            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (process.WaitForExit(TaskCommandTimeoutMs))
            {
                return process.ExitCode;
            }

            process.Kill();
            return -1;
        }
        catch (Win32Exception ex)
        {
            log.Error("schtasks did not start", ex);
            return -1;
        }
    }

    /// <summary>Берёт аргумент в кавычки для командной строки.</summary>
    private static string Quote(string value) => "\"" + value + "\"";

    /// <summary>Запускает свой exe через <c>runas</c> и ждёт завершения; отказ от UAC — <see cref="AdminAutostartResult.Cancelled"/>.</summary>
    private async Task<AdminAutostartResult> RunHelperAsync(string arguments)
    {
        if (Environment.ProcessPath is not { } exe)
        {
            _log.Error("admin autostart: process path is unknown");
            return AdminAutostartResult.Failed;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(exe, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
            {
                return AdminAutostartResult.Failed;
            }

            await process.WaitForExitAsync();
            _log.Info($"admin autostart helper exited with {process.ExitCode}");
            return process.ExitCode == 0 ? AdminAutostartResult.Success : AdminAutostartResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return AdminAutostartResult.Cancelled;
        }
        catch (Win32Exception ex)
        {
            _log.Error("admin autostart helper did not start", ex);
            return AdminAutostartResult.Failed;
        }
    }
}
