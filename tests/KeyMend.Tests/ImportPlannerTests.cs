using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;
using KeyMend.Storage.Transfer;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="ImportPlanner"/>: как настройки из файла соединяются с текущими (SPEC F8).</summary>
public sealed class ImportPlannerTests
{
    /// <summary>Таблицы раскладок для сравнения сокращений.</summary>
    private static readonly LayoutTable[] _tables = [FallbackLayouts.Us, FallbackLayouts.Ru];

    /// <summary>Выбор «Оставить текущее» везде.</summary>
    private static readonly HashSet<ConflictKey> _keepAll = [];

    /// <summary>Нет секретов в файле.</summary>
    private static readonly Dictionary<string, string> _noSecrets = [];

    /// <summary>Счётчик для предсказуемых «случайных» значений.</summary>
    private int _counter;

    /// <summary>Следующий предсказуемый <see cref="Guid"/>.</summary>
    private Guid NextGuid() => new(++_counter, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Правило на клавише.</summary>
    private static Rule MakeRule(string id, string name, VirtualKey key, bool enabled = true, params string[] processes) => new()
    {
        Id = id,
        Name = name,
        Enabled = enabled,
        Processes = processes,
        Trigger = new PressTrigger { Key = key },
        Steps = [new ServiceStep { Action = ServiceAction.TogglePause }],
    };

    /// <summary>Сокращение.</summary>
    private static Snippet MakeSnippet(string id, string keys, string? text = "x", bool secret = false, string? secretRef = null) => new()
    {
        Id = id,
        Keys = keys,
        Text = text,
        Secret = secret,
        SecretRef = secretRef,
    };

    /// <summary>Применяет объединение с выбором «Оставить текущее».</summary>
    private ImportOutcome Merge(AppSettings current, AppSettings imported, IReadOnlySet<ConflictKey>? take = null, IReadOnlyDictionary<string, string>? secrets = null) =>
        ImportPlanner.Apply(current, imported, ImportMode.Merge, take ?? _keepAll, secrets ?? _noSecrets, _tables, NextGuid);

    /// <summary>Заменить всё: берутся настройки файла целиком, старые ключи секретов больше не нужны.</summary>
    [Fact]
    public void Apply_Replace_TakesImportedSettingsAndDropsOldSecrets()
    {
        var current = new AppSettings { Theme = AppTheme.Dark, Snippets = [MakeSnippet("a-1", "abc ", null, true, "s-old")] };
        var imported = new AppSettings
        {
            Theme = AppTheme.Light,
            UiLanguage = AppLanguage.English,
            Rules = [MakeRule("r-1", "Сон", (VirtualKey)0x7E)],
        };

        var outcome = ImportPlanner.Apply(current, imported, ImportMode.Replace, _keepAll, _noSecrets, _tables, NextGuid);

        Assert.Equal(AppTheme.Light, outcome.Settings.Theme);
        Assert.Equal(AppLanguage.English, outcome.Settings.UiLanguage);
        Assert.Equal("Сон", Assert.Single(outcome.Settings.Rules).Name);
        Assert.Empty(outcome.Settings.Snippets);
        Assert.Equal(["s-old"], outcome.ObsoleteSecretKeys);
    }

    /// <summary>Объединение: общие настройки остаются свои, новое добавляется.</summary>
    [Fact]
    public void Apply_MergeWithoutConflicts_AddsNewAndKeepsOwnSettings()
    {
        var current = new AppSettings { Theme = AppTheme.Dark, UiLanguage = AppLanguage.Russian, Rules = [MakeRule("r-1", "Сон", (VirtualKey)0x7E)] };
        var imported = new AppSettings
        {
            Theme = AppTheme.Light,
            UiLanguage = AppLanguage.English,
            Rules = [MakeRule("r-2", "Монитор", (VirtualKey)0x81)],
            Snippets = [MakeSnippet("a-1", "ll ")],
            Exclusions = [new ExclusionEntry { Process = "mstsc.exe", Correction = false }],
        };

        var outcome = Merge(current, imported);

        Assert.Equal(AppTheme.Dark, outcome.Settings.Theme);
        Assert.Equal(AppLanguage.Russian, outcome.Settings.UiLanguage);
        Assert.Equal(["Сон", "Монитор"], outcome.Settings.Rules.Select(r => r.Name));
        Assert.Single(outcome.Settings.Snippets);
        Assert.Single(outcome.Settings.Exclusions);
        Assert.Equal(new ImportReport(1, 1, 1, 0, 0, 0, 0), outcome.Report);
    }

    /// <summary>Одинаковое правило, сокращение и исключение пропускаются, а не дублируются.</summary>
    [Fact]
    public void Apply_MergeIdenticalItems_AreSkippedEvenWithDifferentIds()
    {
        var current = new AppSettings
        {
            Rules = [MakeRule("r-1", "Сон", (VirtualKey)0x7E)],
            Snippets = [MakeSnippet("a-1", "ll ")],
            Exclusions = [new ExclusionEntry { Process = "mstsc.exe" }],
        };
        var imported = new AppSettings
        {
            Rules = [MakeRule("r-other", "Сон", (VirtualKey)0x7E)],
            Snippets = [MakeSnippet("a-other", "ll ")],
            Exclusions = [new ExclusionEntry { Process = "MSTSC.exe" }],
        };

        Assert.Empty(ImportPlanner.FindConflicts(current, imported, _tables));
        var outcome = Merge(current, imported);

        Assert.Single(outcome.Settings.Rules);
        Assert.Single(outcome.Settings.Snippets);
        Assert.Single(outcome.Settings.Exclusions);
        Assert.Equal(3, outcome.Report.Identical);
    }

    /// <summary>Правила на одних клавишах в одних программах конфликтуют; в разных программах, выключенные — нет.</summary>
    [Fact]
    public void FindConflicts_RuleOnSameKeys_DependsOnProgramsAndEnabled()
    {
        var current = new AppSettings
        {
            Rules = [MakeRule("r-1", "Везде", (VirtualKey)0x7E), MakeRule("r-2", "Хром", (VirtualKey)0x7F, true, "chrome.exe")],
        };
        var imported = new AppSettings
        {
            Rules =
            [
                MakeRule("r-9", "Тоже F15", (VirtualKey)0x7E),
                MakeRule("r-8", "F16 в Word", (VirtualKey)0x7F, true, "winword.exe"),
                MakeRule("r-7", "Выключено F15", (VirtualKey)0x7E, false),
            ],
        };

        var conflict = Assert.Single(ImportPlanner.FindConflicts(current, imported, _tables));

        Assert.Equal(new ConflictKey(ConflictKind.Rule, 0), conflict.Key);
        Assert.Equal(["Везде"], conflict.CurrentNames);
    }

    /// <summary>Сокращения с теми же клавишами в другой раскладке конфликтуют; выключенные — нет.</summary>
    [Fact]
    public void FindConflicts_SnippetOnSameKeysInOtherLayout_Conflicts()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "дд ", "Добрый день!")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "ll ", "Другое"), MakeSnippet("a-3", "ll ", "Третье") with { Enabled = false }] };

        var conflict = Assert.Single(ImportPlanner.FindConflicts(current, imported, _tables));

        Assert.Equal(new ConflictKey(ConflictKind.Snippet, 0), conflict.Key);
    }

    /// <summary>Секретное сокращение с открытым текстом в файле и тем же секретом у нас — не конфликт и пропускается как одинаковое.</summary>
    [Fact]
    public void FindConflicts_SecretSnippetWithSameText_IsIdentical()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "sec1 ", null, true, "s-cur")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "sec1 ", "test-secret-1", true)] };
        var known = new Dictionary<string, string> { ["s-cur"] = "test-secret-1" };

        Assert.Empty(ImportPlanner.FindConflicts(current, imported, _tables, known));
        var outcome = ImportPlanner.Apply(current, imported, ImportMode.Merge, _keepAll, _noSecrets, _tables, NextGuid, known);
        Assert.Single(outcome.Settings.Snippets);
        Assert.Equal(1, outcome.Report.Identical);
        Assert.Empty(outcome.NewSecrets);
    }

    /// <summary>Тот же секрет, пришедший зашифрованным и расшифрованный паролем, при загрузке тоже пропускается.</summary>
    [Fact]
    public void Apply_SecretSnippetFromSealedFileWithSameText_IsIdentical()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "sec1 ", null, true, "s-cur")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "sec1 ", null, true, "s-file")] };
        var known = new Dictionary<string, string> { ["s-cur"] = "test-secret-1" };
        var fileSecrets = new Dictionary<string, string> { ["s-file"] = "test-secret-1" };

        var outcome = ImportPlanner.Apply(current, imported, ImportMode.Merge, _keepAll, fileSecrets, _tables, NextGuid, known);

        Assert.Single(outcome.Settings.Snippets);
        Assert.Equal(1, outcome.Report.Identical);
    }

    /// <summary>Зашифрованный файл: после ввода пароля тот же секрет не конфликт, до пароля — конфликт.</summary>
    [Fact]
    public void FindConflicts_SealedSecretSameText_ConflictOnlyUntilPasswordKnown()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "sec1 ", null, true, "s-cur")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "sec1 ", null, true, "s-file")] };
        var known = new Dictionary<string, string> { ["s-cur"] = "test-secret-1" };
        var fileSecrets = new Dictionary<string, string> { ["s-file"] = "test-secret-1" };

        Assert.Single(ImportPlanner.FindConflicts(current, imported, _tables, known));
        Assert.Empty(ImportPlanner.FindConflicts(current, imported, _tables, known, fileSecrets));
    }

    /// <summary>«Взять из файла» для открытого пароля в обычном settings.json: секрет заменяется, а не теряется.</summary>
    [Fact]
    public void Apply_MergeTakeFromFileWithPlainSecret_ReplacesSecret()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "sec1 ", null, true, "s-cur")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "sec1 ", "test-secret-X", true)] };
        var known = new Dictionary<string, string> { ["s-cur"] = "test-secret-1" };
        var take = new HashSet<ConflictKey> { new(ConflictKind.Snippet, 0) };

        var outcome = ImportPlanner.Apply(current, imported, ImportMode.Merge, take, _noSecrets, _tables, NextGuid, known);

        var snippet = Assert.Single(outcome.Settings.Snippets);
        Assert.True(snippet.Enabled);
        Assert.Null(snippet.Text);
        Assert.Equal("test-secret-X", outcome.NewSecrets[snippet.SecretRef!]);
        Assert.Equal(["s-cur"], outcome.ObsoleteSecretKeys);
        Assert.Equal(0, outcome.Report.SnippetsWithoutSecret);
    }

    /// <summary>«Заменить текущие» файлом с открытыми паролями (стартовый набор): сокращения включены, пароли в хранилище.</summary>
    [Fact]
    public void Apply_ReplaceWithPlainSecrets_KeepsSnippetsEnabled()
    {
        var imported = new AppSettings
        {
            Snippets = [MakeSnippet("a-1", "sec1 ", "test-secret-1", true), MakeSnippet("a-2", "sec2 ", "test-secret-2", true)],
        };

        var outcome = ImportPlanner.Apply(new AppSettings(), imported, ImportMode.Replace, _keepAll, _noSecrets, _tables, NextGuid);

        Assert.All(outcome.Settings.Snippets, s => Assert.True(s.Enabled));
        Assert.Equal(["test-secret-1", "test-secret-2"], outcome.NewSecrets.Values.Order());
        Assert.Equal(0, outcome.Report.SnippetsWithoutSecret);
    }

    /// <summary>Тот же ключ, но другой секрет — по-прежнему конфликт.</summary>
    [Fact]
    public void FindConflicts_SecretSnippetWithOtherText_Conflicts()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "sec1 ", null, true, "s-cur")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "sec1 ", "test-secret-2", true)] };
        var known = new Dictionary<string, string> { ["s-cur"] = "test-secret-1" };

        var conflict = Assert.Single(ImportPlanner.FindConflicts(current, imported, _tables, known));

        Assert.Equal(ConflictKind.Snippet, conflict.Key.Kind);
    }

    /// <summary>Текст секрета файла неизвестен (нет пароля) — сравнить нечего, остаётся конфликт.</summary>
    [Fact]
    public void FindConflicts_SecretSnippetWithUnknownFileText_Conflicts()
    {
        var current = new AppSettings { Snippets = [MakeSnippet("a-1", "sec1 ", null, true, "s-cur")] };
        var imported = new AppSettings { Snippets = [MakeSnippet("a-2", "sec1 ", null, true, "s-file")] };
        var known = new Dictionary<string, string> { ["s-cur"] = "test-secret-1" };

        Assert.Single(ImportPlanner.FindConflicts(current, imported, _tables, known));
    }

    /// <summary>Исключение той же программы с другими галочками — конфликт.</summary>
    [Fact]
    public void FindConflicts_ExclusionWithOtherFlags_Conflicts()
    {
        var current = new AppSettings { Exclusions = [new ExclusionEntry { Process = "mstsc.exe" }] };
        var imported = new AppSettings { Exclusions = [new ExclusionEntry { Process = "mstsc.exe", LayoutSwitch = false }] };

        var conflict = Assert.Single(ImportPlanner.FindConflicts(current, imported, _tables));

        Assert.Equal(ConflictKind.Exclusion, conflict.Key.Kind);
    }

    /// <summary>«Оставить текущее»: правило файла пропускается, своё остаётся.</summary>
    [Fact]
    public void Apply_MergeConflictKeepCurrent_SkipsImported()
    {
        var current = new AppSettings { Rules = [MakeRule("r-1", "Своё", (VirtualKey)0x7E)] };
        var imported = new AppSettings { Rules = [MakeRule("r-2", "Чужое", (VirtualKey)0x7E)] };

        var outcome = Merge(current, imported);

        Assert.Equal("Своё", Assert.Single(outcome.Settings.Rules).Name);
        Assert.Equal(1, outcome.Report.KeptCurrent);
    }

    /// <summary>«Взять из файла»: правило файла встаёт на место текущего, у сокращения освобождается ключ секрета.</summary>
    [Fact]
    public void Apply_MergeConflictTakeFromFile_ReplacesCurrent()
    {
        var current = new AppSettings
        {
            Rules = [MakeRule("r-1", "Своё", (VirtualKey)0x7E)],
            Snippets = [MakeSnippet("a-1", "ll ", null, true, "s-old")],
        };
        var imported = new AppSettings
        {
            Rules = [MakeRule("r-2", "Чужое", (VirtualKey)0x7E)],
            Snippets = [MakeSnippet("a-2", "дд ", "Новое")],
        };
        var take = new HashSet<ConflictKey> { new(ConflictKind.Rule, 0), new(ConflictKind.Snippet, 0) };

        var outcome = Merge(current, imported, take);

        Assert.Equal("Чужое", Assert.Single(outcome.Settings.Rules).Name);
        Assert.Equal("Новое", Assert.Single(outcome.Settings.Snippets).Text);
        Assert.Equal(["s-old"], outcome.ObsoleteSecretKeys);
        Assert.Equal(2, outcome.Report.Replaced);
    }

    /// <summary>Совпавший идентификатор у другого правила заменяется новым, пустой тоже.</summary>
    [Fact]
    public void Apply_MergeIdCollision_AssignsNewIds()
    {
        var current = new AppSettings { Rules = [MakeRule("r-1", "Своё", (VirtualKey)0x7E)] };
        var imported = new AppSettings { Rules = [MakeRule("r-1", "Чужое", (VirtualKey)0x7F), MakeRule(string.Empty, "Без id", (VirtualKey)0x80)] };

        var ids = Merge(current, imported).Settings.Rules.Select(r => r.Id).ToList();

        Assert.Equal(3, ids.Distinct().Count());
        Assert.DoesNotContain(string.Empty, ids);
        Assert.Equal("r-1", ids[0]);
    }

    /// <summary>Секреты из файла получают новые ключи, тексты уходят только в словарь для хранилища.</summary>
    [Fact]
    public void Apply_SecretSnippetWithSecret_GetsNewKey()
    {
        var imported = new AppSettings { Snippets = [MakeSnippet("a-1", "abc ", null, true, "s-file")] };
        var secrets = new Dictionary<string, string> { ["s-file"] = "test-secret-1" };

        var outcome = Merge(new AppSettings(), imported, secrets: secrets);

        var snippet = Assert.Single(outcome.Settings.Snippets);
        Assert.NotEqual("s-file", snippet.SecretRef);
        Assert.Null(snippet.Text);
        Assert.Equal("test-secret-1", outcome.NewSecrets[snippet.SecretRef!]);
        Assert.True(snippet.Enabled);
    }

    /// <summary>Секретное сокращение без текста приходит выключенным и считается в отчёте.</summary>
    [Fact]
    public void Apply_SecretSnippetWithoutSecret_IsDisabled()
    {
        var imported = new AppSettings { Snippets = [MakeSnippet("a-1", "abc ", null, true, "s-file")] };

        var outcome = ImportPlanner.Apply(new AppSettings(), imported, ImportMode.Replace, _keepAll, _noSecrets, _tables, NextGuid);

        var snippet = Assert.Single(outcome.Settings.Snippets);
        Assert.False(snippet.Enabled);
        Assert.Null(snippet.SecretRef);
        Assert.Empty(outcome.NewSecrets);
        Assert.Equal(1, outcome.Report.SnippetsWithoutSecret);
    }
}
