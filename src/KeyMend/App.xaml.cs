using System.Globalization;
using System.IO;
using System.Windows;
using KeyMend.Actions;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Native;
using KeyMend.Resources;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;
using KeyMend.Text;
using KeyMend.UI;
using KeyMend.UI.Settings;
using KeyMend.UI.Themes;

namespace KeyMend;

/// <summary>
/// Точка входа и composition root (SPEC 2.8): все объекты собираются здесь при старте и освобождаются в обратном порядке.
/// Окон нет — жизнь программы это значок в трее.
/// </summary>
public sealed partial class App : Application, IDisposable, IAppCommands, IAppControl
{
    /// <summary>Сколько обычный экземпляр, завершающийся при включении режима администратора, может держать мьютекс.</summary>
    private static readonly TimeSpan _elevatedStartWait = TimeSpan.FromSeconds(5);

    /// <summary>Пути данных.</summary>
    private AppPaths? _paths;

    /// <summary>Журнал.</summary>
    private FileLog? _log;

    /// <summary>Защита от второго экземпляра.</summary>
    private SingleInstance? _singleInstance;

    /// <summary>Значок в трее.</summary>
    private TrayIcon? _tray;

    /// <summary>Обычный автозапуск.</summary>
    private Autostart? _autostart;

    /// <summary>Поток функций: выполняет цепочки правил.</summary>
    private ChainRunner? _runner;

    /// <summary>Файл настроек: сюда записываются настройки после переноса секретов в хранилище.</summary>
    private SettingsStore? _settingsStore;

    /// <summary>Хранилище секретных текстов автозамены (DPAPI).</summary>
    private SecretStore? _secrets;

    /// <summary>Таблицы раскладок: по ним сокращения автозамены разбираются в клавиши.</summary>
    private KeyMap? _keyMap;

    /// <summary>Обработчик ввода: триггеры и буфер набранного для автозамены.</summary>
    private InputRouter? _router;

    /// <summary>Глобальные хуки клавиатуры и мыши.</summary>
    private KeyboardHook? _hook;

    /// <summary>Запись триггера для окна настроек.</summary>
    private TriggerRecording? _recording;

    /// <summary>Автозапуск от администратора: диалоги и состояние пункта меню.</summary>
    private AdminAutostartController? _adminController;

    /// <summary>Слежение за <c>settings.json</c>: применяет правки без перезапуска.</summary>
    private SettingsWatcher? _settingsWatcher;

    /// <summary>Шаг «Питание» и таймер питания.</summary>
    private PowerService? _power;

    /// <summary>Подсказка EN/RU при смене раскладки, в том числе извне.</summary>
    private LayoutWatcher? _watcher;

    /// <summary>Окно подсказки; принадлежит потоку UI.</summary>
    private LayoutIndicator? _indicatorWindow;

    /// <summary>Чтение раскладки окна с фокусом: код языка для примера подсказки.</summary>
    private LayoutService? _layouts;

    /// <summary>Светлая и тёмная тема (SPEC F1).</summary>
    private ThemeManager? _themes;

    /// <summary>Текущие настройки и их запись из окна настроек.</summary>
    private SettingsService? _settingsService;

    /// <summary>Окно настроек; <c>null</c>, пока закрыто (в памяти не держится).</summary>
    private SettingsWindow? _settingsWindow;

    /// <summary>Язык интерфейса Windows на момент старта: по нему решается «Как в Windows» (SPEC F9).</summary>
    private readonly CultureInfo _windowsUi = CultureInfo.CurrentUICulture;

    /// <summary>Текущие настройки; заменяются целиком при перечитывании файла.</summary>
    private volatile AppSettings _settings = new();

    /// <summary>KeyMend включён (не на паузе).</summary>
    private bool _enabled = true;

    /// <summary>Запускает приложение: один экземпляр, журнал, настройки, трей.</summary>
    /// <param name="e">Аргументы запуска.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Окон нет: процесс живёт, пока его не завершит «Выход».
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _paths = AppPaths.ForCurrentUser();
        var log = new FileLog(_paths.LogDir, TimeProvider.System);
        _log = log;

        // Повышенный помощник режима администратора: делает своё дело и сразу выходит, без трея и защиты от второго экземпляра.
        if (e.Args is [var first, ..] && first == AdminAutostart.HelperArgument)
        {
            log.SetEnabled(ReadLogEnabled(_paths.SettingsFile, log));
            Shutdown(AdminAutostart.RunHelper(e.Args, log));
            return;
        }

        _singleInstance = SingleInstance.TryAcquire(
            () => _tray?.Post(OpenSettings),
            out var acquired,
            Elevation.IsElevated ? _elevatedStartWait : TimeSpan.Zero);
        if (_singleInstance is null)
        {
            if (acquired == AcquireResult.ElevatedInstanceRunning)
            {
                ApplyUiLanguage(ReadUiLanguage(_paths.SettingsFile, log));
                MessageBox.Show(
                    Strings.AppAdminRunning,
                    Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        HookUnhandledExceptions(log);
        log.Info($"started, elevated={Elevation.IsElevated}");

        var store = new SettingsStore(_paths.SettingsFile, log);
        _settingsStore = store;
        var secrets = new SecretStore(_paths.SecretsFile, new DpapiProtector(), log);
        _secrets = secrets;

        // Язык нужен до загрузки: стартовый набор правил берёт названия из ресурсов, а на Windows с другим языком (например, немецком)
        // без этого они остались бы русскими.
        ApplyUiLanguage(ReadUiLanguage(_paths.SettingsFile, log));
        var loaded = store.Load();
        log.SetEnabled(loaded.Settings.LogEnabled);
        ApplyUiLanguage(loaded.Settings.UiLanguage);
        _themes = new ThemeManager(this, new RegistrySystemTheme());
        _themes.Apply(loaded.Settings.Theme);
        if (loaded.IsFirstRun)
        {
            store.Save(loaded.Settings);
        }

        _autostart = new Autostart();
        _adminController = new AdminAutostartController(new AdminAutostart(log), _autostart, log, Quit);
        _ = GuardAsync(_adminController.RefreshAsync, "admin autostart refresh");
        _tray = new TrayIcon(log, OnTrayCommand, () => new TrayMenuState(_enabled, IsAutostartOn(), _adminController?.IsOn == true, _power?.TimerText));
        _tray.SystemThemeChanged += () => _themes?.OnSystemThemeChanged();
        RefreshTrayStatus();
        StartInput(log, SealSecrets(loaded.Settings), secrets, _tray, _paths);
        TrimMemory();
        if (loaded.IsFirstRun)
        {
            OpenSettings();
        }
    }

    /// <inheritdoc />
    void IAppCommands.TogglePause() => _tray?.Post(() => SetEnabled(!_enabled));

    /// <inheritdoc />
    void IAppCommands.OpenSettings() => _tray?.Post(OpenSettings);

    /// <summary>Читает настройку журнала для помощника, которому остальные настройки не нужны. Нет файла или он не читается — значение по умолчанию.</summary>
    /// <param name="settingsFile">Путь к <c>settings.json</c>.</param>
    /// <param name="log">Журнал (пока в режиме ожидания).</param>
    /// <returns>Вести ли журнал.</returns>
    private static bool ReadLogEnabled(string settingsFile, FileLog log) =>
        File.Exists(settingsFile) && new SettingsStore(settingsFile, log).TryRead().Settings is { } settings
            ? settings.LogEnabled
            : AppSettings.DefaultLogEnabled;

    /// <summary>Читает язык интерфейса из файла настроек без их применения; нет файла или он битый — «Как в Windows».</summary>
    /// <param name="settingsFile">Путь к <c>settings.json</c>.</param>
    /// <param name="log">Журнал для сообщений чтения.</param>
    /// <returns>Настройка языка.</returns>
    private static AppLanguage ReadUiLanguage(string settingsFile, FileLog log) =>
        File.Exists(settingsFile) && new SettingsStore(settingsFile, log).TryRead().Settings is { } settings
            ? settings.UiLanguage
            : AppLanguage.System;

    /// <summary>
    /// Выбирает язык строк интерфейса. <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> действует на все потоки, в том числе
    /// на поток цепочек и пул, откуда приходят уведомления; поток UI создан раньше и берёт язык отдельным присвоением.
    /// </summary>
    /// <param name="setting">Настройка пользователя.</param>
    /// <returns><c>true</c>, если язык изменился.</returns>
    private bool ApplyUiLanguage(AppLanguage setting)
    {
        var culture = UiLanguageResolver.Resolve(setting, _windowsUi);
        if (CultureInfo.DefaultThreadCurrentUICulture?.Name == culture.Name)
        {
            return false;
        }

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return true;
    }

    /// <inheritdoc />
    bool IAppControl.IsEnabled => _enabled;

    /// <inheritdoc />
    bool IAppControl.IsAutostartOn => IsAutostartOn();

    /// <inheritdoc />
    bool IAppControl.IsAdminAutostartOn => _adminController?.IsOn == true;

    /// <inheritdoc />
    void IAppControl.SetEnabled(bool enabled) => SetEnabled(enabled);

    /// <inheritdoc />
    bool IAppControl.TrySetAutostart(bool on)
    {
        if (IsAutostartOn() != on)
        {
            ToggleAutostart();
        }

        return IsAutostartOn() == on;
    }

    /// <inheritdoc />
    Task IAppControl.ToggleAdminAutostartAsync() =>
        _adminController is null ? Task.CompletedTask : GuardAsync(_adminController.ToggleAsync, "admin autostart toggle");

    /// <inheritdoc />
    void IAppControl.OpenSettingsFile() => OpenSettingsFile();

    /// <inheritdoc />
    void IAppControl.CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _log?.Error("could not copy text to the clipboard", ex);
            _tray?.Notify(Strings.AppName, Strings.ErrClipboard, NotifyLevel.Error);
        }
    }

    /// <inheritdoc />
    void IAppControl.OpenFolder(string path)
    {
        if (!ShellLauncher.OpenFolder(path))
        {
            _tray?.Notify(Strings.AppName, Strings.ErrOpenFolder, NotifyLevel.Error);
        }
    }

    /// <inheritdoc />
    void IAppControl.ShowIndicatorSample(System.Windows.Rect anchor)
    {
        var code = _layouts?.GetCurrent()?.Code ?? "EN";
        var point = new Anchor((int)anchor.Left, (int)anchor.Top, (int)anchor.Bottom, AnchorSource.Uia);
        _indicatorWindow?.ShowAt(code, point, _settings.Indicator.DurationMs);
    }

    /// <summary>Собирает цепочку ввода: сервис раскладок, исполнитель шагов, поток функций и хук клавиатуры.</summary>
    /// <param name="log">Журнал.</param>
    /// <param name="settings">Загруженные настройки.</param>
    /// <param name="secrets">Хранилище секретных текстов автозамены.</param>
    /// <param name="tray">Значок трея: уведомления об ошибках запуска.</param>
    /// <param name="paths">Пути данных: папка скриптов.</param>
    private void StartInput(FileLog log, AppSettings settings, ISecretStore secrets, TrayIcon tray, AppPaths paths)
    {
        _settings = settings;
        var settingsService = new SettingsService(settings, new SettingsStore(paths.SettingsFile, log), log);
        settingsService.Changed += OnSettingsChanged;
        _settingsService = settingsService;
        CaretLocator.WarmUp();
        _indicatorWindow = new LayoutIndicator();
        var layouts = new LayoutService();
        _layouts = layouts;
        var presenter = new IndicatorPresenter(() => _settings.Indicator, _indicatorWindow, action => _tray?.Post(action), log);
        _watcher = new LayoutWatcher(layouts, presenter, () => _settings.Indicator, TimeProvider.System, log);
        var sender = new InputSender(log);
        var tables = new KeyMap();
        _keyMap = tables;
        var waiter = new ModifierWaiter(PhysicalKeyboard.IsDown, TimeProvider.System);
        var clipboard = new Win32Clipboard(_tray?.WindowHandle ?? 0, TimeProvider.System, log);
        var guard = new ClipboardGuard(clipboard, sender, TimeProvider.System, log);
        var corrector = new TextCorrector(layouts, tables, guard, new UiaTextFocus(log), sender, waiter, _watcher, log);
        var inputSteps = new InputStepRunner(sender, guard, secrets, waiter, log);
        // Папка скриптов нужна с первого запуска: в неё владелец кладёт свои файлы, а относительные пути шагов считаются от неё.
        Directory.CreateDirectory(paths.ScriptsDir);
        var launcher = new LaunchStepRunner(LaunchEnvironment.Current(paths.ScriptsDir), new UnelevatedLauncher(Elevation.IsElevated), tray, log);
        var power = new PowerService(new Win32SystemPower(), new WpfCountdownPrompt(tray.Post), tray, TimeProvider.System, log);
        power.Changed += () => tray.Post(RefreshTrayStatus);
        _power = power;
        var executor = new StepExecutor(layouts, _watcher, this, corrector, inputSteps, launcher, power, TimeProvider.System, log);
        _runner = new ChainRunner(executor, sender, _watcher.RequestCheck, gesture => _recording?.Deliver(gesture), log);
        _router = new InputRouter(
            new HotkeyEngine(PhysicalKeyboard.IsDown), settings.WordBufferLimit, PhysicalKeyboard.IsDown, ForegroundLayout.ReadHkl);
        _hook = new KeyboardHook(_router, _runner.Writer, log);
        _recording = new TriggerRecording(
            () => _hook?.StartRecording(), () => _hook?.CancelRecording(), action => tray.Post(action), TimeProvider.System, log);
        _hook.SetTables(BindingTables.Build(settings.Rules, settings.Exclusions, CompileSnippets(settings, log), OwnProcess));
        try
        {
            _hook.Start();
        }
        catch (InvalidOperationException ex)
        {
            log.Error("keyboard hook was not started", ex);
            _tray?.Notify(Strings.AppName, Strings.ErrHook, NotifyLevel.Error);
        }

        if (_tray is not null && _paths is not null)
        {
            var store = new SettingsStore(_paths.SettingsFile, log);
            _settingsWatcher = new SettingsWatcher(
                store.TryRead, ApplySettings, _tray, TimeProvider.System, log, _paths.SettingsFile);
        }
    }

    /// <summary>
    /// Принимает перечитанные настройки (вызывается из потока пула): переносит секреты в хранилище и передаёт результат
    /// в поток UI, где его применяют все подписчики <see cref="SettingsService.Changed"/>.
    /// </summary>
    /// <param name="loaded">Проверенные настройки, как они прочитаны из файла.</param>
    private void ApplySettings(AppSettings loaded)
    {
        var settings = SealSecrets(loaded);
        _tray?.Post(() => _settingsService?.Replace(settings));
    }

    /// <summary>Применяет изменившиеся настройки: правила и исключения — в хук, тема — в словари, подсказка берёт настройки при показе. Поток UI.</summary>
    /// <param name="settings">Действующие настройки.</param>
    private void OnSettingsChanged(AppSettings settings)
    {
        _settings = settings;
        _themes?.Apply(settings.Theme);
        if (ApplyUiLanguage(settings.UiLanguage))
        {
            RefreshTrayStatus();

            // Окно пересобирается позже: сейчас идёт обработчик списка языка на самой странице.
            _tray?.Post(() => _settingsWindow?.ApplyLanguage());
        }

        _hook?.SetTables(BindingTables.Build(settings.Rules, settings.Exclusions, CompileSnippets(settings, _log), OwnProcess));
        _router?.SetBufferLimit(settings.WordBufferLimit);
        _log?.SetEnabled(settings.LogEnabled);
    }

    /// <summary>Имя exe самого KeyMend: в его окне автозамена отключена, чтобы не срабатывать в полях редактора сокращения.</summary>
    private static string? OwnProcess => Path.GetFileName(Environment.ProcessPath);

    /// <summary>
    /// Переносит открытые тексты секретных сокращений в <c>secrets.dat</c> и перезаписывает <c>settings.json</c> без них
    /// (SPEC F5.4). Сначала записываются секреты: если это не вышло, файл настроек не трогается.
    /// </summary>
    /// <param name="settings">Настройки, как они прочитаны из файла.</param>
    /// <returns>Настройки, пригодные для работы: без открытых текстов секретов.</returns>
    private AppSettings SealSecrets(AppSettings settings)
    {
        if (_secrets is null || _settingsStore is null)
        {
            return settings;
        }

        var result = SecretSealer.Seal(settings, static () => "s-" + Guid.NewGuid().ToString("N"));
        if (result.Secrets.Count == 0)
        {
            return settings;
        }

        if (!_secrets.SetMany(result.Secrets))
        {
            _tray?.Notify(Strings.AppName, Strings.ErrSecretsSave, NotifyLevel.Error);
            return settings;
        }

        try
        {
            _settingsStore.Save(result.Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Error("could not rewrite settings.json after sealing secrets", ex);
            _tray?.Notify(Strings.AppName, Strings.ErrSecretsPlain, NotifyLevel.Error);
            return result.Settings;
        }

        _log?.Info($"sealed {result.Secrets.Count} secret(s) into secrets.dat");
        return result.Settings;
    }

    /// <summary>Собирает сокращения автозамены по таблицам установленных раскладок; замечания пишет в журнал без содержимого.</summary>
    /// <param name="settings">Настройки.</param>
    /// <param name="log">Журнал или <c>null</c>.</param>
    /// <returns>Набор сокращений для хука.</returns>
    private SnippetMatcher CompileSnippets(AppSettings settings, FileLog? log)
    {
        if (_keyMap is null || settings.Snippets.Count == 0)
        {
            return SnippetMatcher.Empty;
        }

        var installed = LayoutService.GetInstalledLayouts().Select(hkl => new InstalledLayout(hkl, _keyMap.GetTable(hkl))).ToList();
        var result = SnippetCompiler.Compile(settings.Snippets, installed);
        foreach (var warning in result.Warnings)
        {
            log?.Info(warning);
        }

        log?.Info($"snippets compiled: {result.Matcher.Count} of {settings.Snippets.Count}");
        return result.Matcher;
    }

    /// <summary>
    /// Запускает асинхронное действие без ожидания и записывает в журнал любое исключение, которое иначе осталось бы
    /// незамеченным (ожидать результат в обработчиках трея нельзя — блокировался бы поток UI).
    /// </summary>
    /// <param name="action">Действие.</param>
    /// <param name="what">Название действия для журнала.</param>
    private async Task GuardAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.Error($"{what} failed", ex);
        }
    }

    /// <summary>Включает или выключает KeyMend (пауза): хук, значок и подсказка значка.</summary>
    /// <param name="enabled"><c>true</c> — работать, <c>false</c> — пауза.</param>
    private void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (_hook is not null)
        {
            _hook.IsPaused = !enabled;
        }

        RefreshTrayStatus();
        _settingsWindow?.SetEnabled(enabled);
        _log?.Info(enabled ? "resumed" : "paused");
    }

    /// <summary>Обновляет подсказку значка: пауза, режим администратора и таймер питания.</summary>
    private void RefreshTrayStatus() => _tray?.UpdateStatus(paused: !_enabled, Elevation.IsElevated, _power?.TimerText);

    /// <summary>Освобождает ресурсы в порядке, обратном созданию.</summary>
    /// <param name="e">Данные выхода.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        _log?.Info("stopped");
        base.OnExit(e);
    }

    /// <summary>Освобождает трей и защиту от второго экземпляра; повторный вызов безопасен.</summary>
    public void Dispose()
    {
        _settingsWatcher?.Dispose();
        _settingsWatcher = null;
        _hook?.Dispose();
        _hook = null;
        _router = null;
        _runner?.Dispose();
        _runner = null;
        _watcher?.Dispose();
        _watcher = null;
        _indicatorWindow?.Close();
        _indicatorWindow = null;
        _settingsWindow?.Close();
        _settingsWindow = null;
        _tray?.Dispose();
        _tray = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    /// <summary>Пишет в журнал исключения, которые иначе пропали бы молча.</summary>
    /// <param name="log">Журнал.</param>
    private void HookUnhandledExceptions(FileLog log)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            log.Error("unhandled UI exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            log.Error("unhandled exception", args.ExceptionObject as Exception);
            _tray?.RemoveIcon();
        };

        // Любой выход, кроме принудительного завершения процесса: значок не должен оставаться призраком в трее.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _tray?.RemoveIcon();
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.Error("unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>Завершает программу. Окно настроек предупреждается, чтобы не спрашивать о несохранённых изменениях и не задержать выход.</summary>
    private void Quit()
    {
        if (_settingsWindow is { } window)
        {
            window.Quitting = true;
        }

        Shutdown();
    }

    /// <summary>Выполняет команду меню трея.</summary>
    /// <param name="command">Выбранная команда.</param>
    private void OnTrayCommand(TrayCommand command)
    {
        switch (command)
        {
            case TrayCommand.ToggleEnabled:
                SetEnabled(!_enabled);
                break;
            case TrayCommand.CancelPowerTimer:
                _ = _power?.CancelTimer();
                break;
            case TrayCommand.OpenSettings:
                OpenSettings();
                break;
            case TrayCommand.ToggleAutostart:
                ToggleAutostart();
                _settingsWindow?.RefreshAppState();
                break;
            case TrayCommand.ToggleAdminAutostart:
                if (_adminController is { } admin)
                {
                    _ = GuardAsync(
                        async () =>
                        {
                            await admin.ToggleAsync();
                            _settingsWindow?.RefreshAppState();
                        },
                        "admin autostart toggle");
                }

                break;
            case TrayCommand.OpenDataFolder:
                if (_paths is not null && !ShellLauncher.OpenFolder(_paths.DataDir))
                {
                    _tray?.Notify(Strings.AppName, Strings.ErrOpenDataFolder, NotifyLevel.Error);
                }

                break;
            case TrayCommand.About:
                OpenSettings(SettingsSection.About);
                break;
            case TrayCommand.Exit:
                Quit();
                break;
        }
    }

    /// <summary>Открывает окно настроек на разделе «Общие»; если оно уже есть, выводит его на передний план.</summary>
    private void OpenSettings() => OpenSettings(null);

    /// <summary>Открывает окно настроек; если оно уже есть, выводит его на передний план.</summary>
    /// <param name="section">Раздел, который нужно показать; <c>null</c> — оставить как есть (у нового окна «Общие»).</param>
    private void OpenSettings(SettingsSection? section)
    {
        if (_settingsWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            _ = existing.Activate();
            if (section is { } target)
            {
                _ = existing.Navigate(target);
            }

            return;
        }

        if (_settingsService is null || _themes is null || _recording is null || _paths is null || _secrets is null || _log is null)
        {
            return;
        }

        var tables = new SystemInstalledTables(_keyMap);
        var snippets = new SnippetServices(new SnippetSaver(_settingsService, _secrets, Guid.NewGuid, _log), _secrets, tables);
        var backups = new BackupStore(_paths, TimeProvider.System, _log);
        var transfer = new TransferServices(
            new SettingsTransfer(_paths, _settingsService, _secrets, new DpapiProtector(), backups, tables, Guid.NewGuid, _log), backups);
        var window = new SettingsWindow(
            _settingsService, _themes, Elevation.IsElevated, _enabled, _recording, new RegistryLanguageHotkeys(), new WindowsInstalledLanguages(),
            _paths, new Win32SystemPower().IsHibernateEnabled, snippets, this, new WindowsRunningApps(), transfer);
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            TrimMemory();
        };
        _settingsWindow = window;
        if (section is { } first)
        {
            window.Select(first);
        }

        window.Show();
        _ = window.Activate();
    }

    /// <summary>
    /// Возвращает память системе после закрытия окна: сборка мусора и сброс рабочего набора (SPEC 6: ≤ 60 МБ при закрытом окне).
    /// Откладывается на секунду, чтобы окно успело освободить ресурсы.
    /// </summary>
    private static void TrimMemory() => _ = TrimMemoryAsync();

    /// <summary>Ждёт секунду и освобождает память; работает в пуле потоков, UI не блокирует.</summary>
    /// <returns>Задача, завершающаяся после сброса.</returns>
    private static async Task TrimMemoryAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        _ = Kernel32.SetProcessWorkingSetSize(Kernel32.GetCurrentProcess(), -1, -1);
    }

    /// <summary>Открывает <c>settings.json</c> в редакторе; временная замена разделам окна, которых ещё нет.</summary>
    private void OpenSettingsFile()
    {
        if (_paths is not null && !ShellLauncher.OpenFile(_paths.SettingsFile))
        {
            _tray?.Notify(Strings.AppName, Strings.ErrOpenSettingsFile, NotifyLevel.Error);
        }
    }

    /// <summary>Включён ли автозапуск; сбой чтения реестра считается «выключен».</summary>
    /// <returns><c>true</c>, если запись Run есть и указывает на этот exe.</returns>
    private bool IsAutostartOn()
    {
        try
        {
            return _autostart?.IsEnabled() == true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _log?.Error("could not read autostart state", ex);
            return false;
        }
    }

    /// <summary>Переключает обычный автозапуск; ошибка реестра показывается уведомлением.</summary>
    private void ToggleAutostart()
    {
        try
        {
            if (IsAutostartOn())
            {
                _autostart?.Disable();
            }
            else
            {
                _autostart?.Enable();
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _log?.Error("could not change autostart", ex);
            _tray?.Notify(Strings.AppName, Strings.ErrAutostart, NotifyLevel.Error);
        }
    }
}
