using System.Diagnostics.CodeAnalysis;
using System.Windows;
using KeyMend.Infrastructure;
using KeyMend.Input;
using KeyMend.Layouts;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;
using KeyMend.Text;
using KeyMend.UI.Settings;
using KeyMend.UI.Themes;

namespace KeyMend.Tests;

/// <summary>
/// Дымовая проверка страниц окна настроек: они создаются, раскладываются и не падают на ресурсах и разметке. Окна не
/// показываются. Всё в одном тесте и в одном потоке STA: <see cref="Application"/> на процесс бывает только одно.
/// </summary>
public sealed class SettingsPagesSmokeTests
{
    /// <summary>Запись настроек в никуда.</summary>
    private sealed class NullWriter : ISettingsWriter
    {
        /// <inheritdoc />
        public void Save(AppSettings settings)
        {
        }
    }

    /// <summary>Запись клавиш, которая ничего не делает.</summary>
    private sealed class NoRecording : ITriggerRecording
    {
        /// <inheritdoc />
        public bool IsRecording => false;

        /// <inheritdoc />
        public void Start(Action<RecordedGesture> onFinished)
        {
        }

        /// <inheritdoc />
        public void Cancel()
        {
        }
    }

    /// <summary>Сочетания Windows: смена языка ни на чём не стоит.</summary>
    private sealed class NoHotkeys : ILanguageHotkeysSource
    {
        /// <inheritdoc />
        public LanguageHotkeys Read() => LanguageHotkeys.None;
    }

    /// <summary>Два языка вместо чтения раскладок системы.</summary>
    private sealed class FakeLanguages : IInstalledLanguages
    {
        /// <inheritdoc />
        public IReadOnlyList<string> Tags() => ["ru-RU", "en-US"];
    }

    /// <summary>Программа без действий: пример подсказки никуда не показывается.</summary>
    private sealed class NoApp : IAppControl
    {
        /// <inheritdoc />
        public bool IsEnabled => true;

        /// <inheritdoc />
        public bool IsAutostartOn => false;

        /// <inheritdoc />
        public bool IsAdminAutostartOn => false;

        /// <inheritdoc />
        public void SetEnabled(bool enabled)
        {
        }

        /// <inheritdoc />
        public bool TrySetAutostart(bool on) => true;

        /// <inheritdoc />
        public Task ToggleAdminAutostartAsync() => Task.CompletedTask;

        /// <inheritdoc />
        public void ShowIndicatorSample(Rect anchor)
        {
        }

        /// <inheritdoc />
        public void OpenSettingsFile()
        {
        }

        /// <inheritdoc />
        public void CopyText(string text)
        {
        }

        /// <inheritdoc />
        public void OpenFolder(string path)
        {
        }
    }

    /// <summary>Запущенные программы из двух записей вместо системного списка.</summary>
    private sealed class FakeApps : IRunningApps
    {
        /// <inheritdoc />
        public IReadOnlyList<RunningApp> List() => [new("chrome.exe", "Google Chrome"), new("notepad.exe", null)];

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string?> Describe(IEnumerable<string> exes) =>
            exes.ToDictionary(e => e, e => (string?)"Описание", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Тема Windows: светлая.</summary>
    private sealed class LightTheme : ISystemTheme
    {
        /// <inheritdoc />
        public bool IsLight() => true;
    }

    /// <summary>Таблица правил, редактор правила и окно настроек создаются на стартовом наборе.</summary>
    [Fact]
    public void Pages_StarterSettings_BuildWithoutErrors()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    /// <summary>Тело проверки: ресурсы приложения, затем страницы.</summary>
    private static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var source in new[]
        {
            "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml",
            "pack://application:,,,/KeyMend;component/UI/Themes/Palette.Light.xaml",
            "pack://application:,,,/KeyMend;component/UI/Themes/Controls.xaml",
        })
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Absolute) });
        }

        var settings = new SettingsService(DefaultSettings.Create(), new NullWriter(), new TestLog());
        var rules = new RulesPage(settings, new NoHotkeys(), new Dialogs(), _ => { }, _ => { });
        Layout(rules);

        var rule = settings.Current.Rules[0];
        foreach (var step in new Step?[] { null, new CorrectStep { Scope = CorrectScope.Line }, new SwitchLayoutStep { Mode = SwitchMode.Specific, Layout = "de-DE" }, StepCatalog.Create(StepKind.Launch), StepCatalog.Create(StepKind.Power), StepCatalog.Create(StepKind.Press), StepCatalog.Create(StepKind.Delay), StepCatalog.Create(StepKind.InsertText), StepCatalog.Create(StepKind.Service) })
        {
            var editor = new RuleEditorPage(
                settings, new NoRecording(), new NoHotkeys(), new StepContext(["ru-RU", "en-US"], @"C:Testscripts", new Dialogs(), true, () => { }), new Dialogs(), step is null ? rule : null, step, () => { }, _ => { });
            Layout(editor);
            Layout(editor.ActionBar);
            Assert.False(editor.HasChanges);
        }

        var secrets = new FakeSecrets();
        secrets.Items["s-test"] = "test-secret-1";
        var services = new SnippetServices(new SnippetSaver(settings, secrets, Guid.NewGuid, new TestLog()), secrets, new FakeTables());
        Layout(new SnippetsPage(settings, services.Tables, _ => { }, () => { }));
        Snippet?[] snippets =
        [
            null,
            new Snippet { Id = "a-1", Keys = "дд ", Text = "Добрый день!", After = [new PressStep { Keys = new KeyChord(ModifierSet.AnyCtrl, VirtualKey.Return) }] },
            new Snippet { Id = "a-2", Keys = "sec2 ", Secret = true, SecretRef = "s-test" },
            new Snippet { Id = "a-3", Keys = "ддд ", Secret = true, SecretRef = "s-missing" },
        ];
        foreach (var snippet in snippets)
        {
            var editor = new SnippetEditorPage(
                settings, services, new StepContext(["ru-RU", "en-US"], @"C:Testscripts", new Dialogs(), true, () => { }), new Dialogs(),
                snippet, () => { }, _ => { });
            Layout(editor);
            Layout(editor.ActionBar);
            Assert.False(editor.HasChanges);
        }

        var testPaths = new AppPaths(@"C:Test", @"C:Testlogs");
        var transferBackups = new BackupStore(testPaths, TimeProvider.System, new TestLog());
        var transfer = new TransferServices(
            new SettingsTransfer(testPaths, settings, new SecretStore(testPaths.SecretsFile, new NullProtector(), new TestLog()), new NullProtector(), transferBackups, new FakeTables(), Guid.NewGuid, new TestLog()),
            transferBackups);
        var window = new SettingsWindow(
            settings, new ThemeManager(app, new LightTheme()), elevated: false, enabled: true,
            new NoRecording(), new NoHotkeys(), new FakeLanguages(), new AppPaths(@"C:Test", @"C:Testlogs"), true, services, new NoApp(), new FakeApps(), transfer);
        Layout(new TransferPage(settings, transfer.Transfer, transfer.Backups, new Dialogs(), new Dialogs(), new NoApp(), @"C:Test"));
        Layout(new HintPage(settings, new NoApp(), () => { }));
        Layout(new GeneralPage(settings, new NoApp(), @"C:Test"));
        Layout(new AboutPage(settings, new NoApp(), AboutInfo.Create(new AppPaths(@"C:Test", @"C:Testlogs"), elevated: true)));
        var exclusions = new ExclusionsPage(settings, new FakeApps());
        Layout(exclusions);
        window.Select(SettingsSection.Exclusions);
        window.Select(SettingsSection.Hint);
        window.Select(SettingsSection.Transfer);
        window.Select(SettingsSection.About);
        window.ApplyLanguage();
        window.Select(SettingsSection.General);
        window.Select(SettingsSection.Rules, RuleGroup.Language);
        window.Select(SettingsSection.Rules);
        window.ShowRuleEditor(rule);
        window.ShowNewRule(RuleGroup.Correction);
        window.Select(SettingsSection.Snippets);
        window.ShowSnippetEditor(snippets[1]!);
        window.ShowNewSnippet();
        app.Shutdown();
    }

    /// <summary>Шифрование, которое ничего не делает: хранилище секретов в этих проверках не пишет на диск.</summary>
    private sealed class NullProtector : ISecretProtector
    {
        /// <inheritdoc />
        public byte[] Protect(byte[] data) => data;

        /// <inheritdoc />
        public byte[] Unprotect(byte[] data) => data;
    }

    /// <summary>Хранилище секретов в памяти.</summary>
    private sealed class FakeSecrets : ISecretStore
    {
        /// <summary>Секреты по ключам.</summary>
        public Dictionary<string, string> Items { get; } = [];

        /// <inheritdoc />
        public bool TryGet(string key, [NotNullWhen(true)] out string? text) => Items.TryGetValue(key, out text);

        /// <inheritdoc />
        public bool SetMany(IReadOnlyDictionary<string, string> secrets) => true;

        /// <inheritdoc />
        public bool Remove(IReadOnlyCollection<string> keys) => true;
    }

    /// <summary>Запасные таблицы EN и RU вместо системных.</summary>
    private sealed class FakeTables : IInstalledTables
    {
        /// <inheritdoc />
        public IReadOnlyList<LayoutTable> Tables() => [FallbackLayouts.Us, FallbackLayouts.Ru];
    }

    /// <summary>Просит элемент измериться и разложиться: ошибки шаблонов и ресурсов всплывают здесь.</summary>
    private static void Layout(UIElement element)
    {
        element.Measure(new Size(800, 2000));
        element.Arrange(new Rect(0, 0, 800, 2000));
        element.UpdateLayout();
    }

    /// <summary>Вопросы без окна: на любой вопрос отвечают отказом.</summary>
    private sealed class Dialogs : IDialogs, IFilePicker
    {
        /// <inheritdoc />
        public int Ask(string title, string text, params string[] buttons) => -1;

        /// <inheritdoc />
        public string? PickFile(string initialDirectory) => null;

        /// <inheritdoc />
        public string? PickFolder(string initialDirectory) => null;

        /// <inheritdoc />
        public string? PickSaveFile(string suggestedName) => null;

        /// <inheritdoc />
        public string? PickTransferFile() => null;
    }
}
