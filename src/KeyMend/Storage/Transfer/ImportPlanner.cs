using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.Storage.Transfer;

/// <summary>Как загруженные настройки соединяются с текущими (SPEC F8).</summary>
internal enum ImportMode
{
    /// <summary>Настройки из файла целиком вместо текущих.</summary>
    Replace,

    /// <summary>Новое из файла добавляется к текущим.</summary>
    Merge,
}

/// <summary>Что конфликтует при объединении.</summary>
internal enum ConflictKind
{
    /// <summary>Правило: те же клавиши в пересекающемся наборе программ.</summary>
    Rule,

    /// <summary>Сокращение: те же клавиши.</summary>
    Snippet,

    /// <summary>Исключение: та же программа с другими галочками.</summary>
    Exclusion,
}

/// <summary>Какой именно элемент файла конфликтует: вид и номер в списке файла.</summary>
/// <param name="Kind">Вид.</param>
/// <param name="Index">Номер в списке файла.</param>
internal readonly record struct ConflictKey(ConflictKind Kind, int Index);

/// <summary>Конфликт одного элемента файла с текущими настройками.</summary>
/// <param name="Key">Элемент файла.</param>
/// <param name="ImportedName">Название правила, клавиши сокращения или имя программы из файла (только для показа на экране).</param>
/// <param name="CurrentNames">Названия текущих элементов, с которыми конфликт.</param>
internal sealed record ImportConflict(ConflictKey Key, string ImportedName, IReadOnlyList<string> CurrentNames);

/// <summary>Итоговые числа загрузки.</summary>
/// <param name="Rules">Добавлено правил.</param>
/// <param name="Snippets">Добавлено сокращений.</param>
/// <param name="Exclusions">Добавлено исключений.</param>
/// <param name="Identical">Пропущено, потому что такое уже есть.</param>
/// <param name="Replaced">Текущих элементов заменено по выбору «Взять из файла».</param>
/// <param name="KeptCurrent">Элементов файла пропущено по выбору «Оставить текущее».</param>
/// <param name="SnippetsWithoutSecret">Секретных сокращений, пришедших без текста: они выключены.</param>
internal sealed record ImportReport(
    int Rules, int Snippets, int Exclusions, int Identical, int Replaced, int KeptCurrent, int SnippetsWithoutSecret);

/// <summary>Результат соединения настроек.</summary>
/// <param name="Settings">Новые настройки; открытых текстов секретов в них нет.</param>
/// <param name="NewSecrets">Секреты для записи в хранилище под новыми ключами.</param>
/// <param name="ObsoleteSecretKeys">Ключи, которые больше никому не нужны и убираются из хранилища.</param>
/// <param name="Report">Что сделано.</param>
internal sealed record ImportOutcome(
    AppSettings Settings,
    IReadOnlyDictionary<string, string> NewSecrets,
    IReadOnlyList<string> ObsoleteSecretKeys,
    ImportReport Report);

/// <summary>
/// Соединение настроек из файла с текущими (SPEC F8): поиск конфликтов и сам перенос. Правила проверяются теми же
/// проверками, что и редактор правила, сокращения — теми же шаблонами клавиш. Чистая логика без диска.
/// </summary>
internal static class ImportPlanner
{
    /// <summary>Находит конфликты, о которых надо спросить при объединении. Одинаковые элементы конфликтом не считаются.</summary>
    /// <param name="current">Текущие настройки.</param>
    /// <param name="imported">Настройки из файла.</param>
    /// <param name="tables">Таблицы установленных раскладок для сравнения сокращений.</param>
    /// <param name="currentSecrets">
    /// Текущие секреты по ключам: нужны, чтобы секретное сокращение с тем же текстом не считалось конфликтом. Текст секрета
    /// из файла известен только если он лежит в файле открыто (обычный <c>settings.json</c>); из зашифрованного файла — после ввода пароля.
    /// </param>
    /// <returns>Конфликты в порядке файла.</returns>
    /// <param name="fileSecrets">Расшифрованные секреты файла, если пароль уже введён; иначе <c>null</c>.</param>
    public static IReadOnlyList<ImportConflict> FindConflicts(
        AppSettings current,
        AppSettings imported,
        IReadOnlyList<LayoutTable> tables,
        IReadOnlyDictionary<string, string>? currentSecrets = null,
        IReadOnlyDictionary<string, string>? fileSecrets = null)
    {
        var known = new SecretTexts(currentSecrets ?? _emptySecrets, fileSecrets ?? _emptySecrets);
        var conflicts = new List<ImportConflict>();
        for (var i = 0; i < imported.Rules.Count; i++)
        {
            var rule = imported.Rules[i];
            if (!IsIdenticalToAny(rule, current.Rules) && RuleClashes(rule, current.Rules) is { Count: > 0 } clashes)
            {
                conflicts.Add(new ImportConflict(new ConflictKey(ConflictKind.Rule, i), rule.Name, [.. clashes.Select(c => current.Rules[c].Name)]));
            }
        }

        var snippetContext = SnippetContext.Create(tables);
        for (var i = 0; i < imported.Snippets.Count; i++)
        {
            var snippet = imported.Snippets[i];
            if (!IsIdenticalToAny(snippet, current.Snippets, known) && SnippetClashes(snippet, current.Snippets, snippetContext) is { Count: > 0 } clashes)
            {
                conflicts.Add(new ImportConflict(
                    new ConflictKey(ConflictKind.Snippet, i), snippet.Keys, [.. clashes.Select(c => current.Snippets[c].Keys)]));
            }
        }

        for (var i = 0; i < imported.Exclusions.Count; i++)
        {
            var exclusion = imported.Exclusions[i];
            var same = current.Exclusions.FirstOrDefault(e => SameProgram(e, exclusion));
            if (same is not null && !SameFlags(same, exclusion))
            {
                conflicts.Add(new ImportConflict(new ConflictKey(ConflictKind.Exclusion, i), exclusion.Process, [same.Process]));
            }
        }

        return conflicts;
    }

    /// <summary>Соединяет настройки.</summary>
    /// <param name="current">Текущие настройки.</param>
    /// <param name="imported">Настройки из файла.</param>
    /// <param name="mode">Заменить всё или добавить.</param>
    /// <param name="takeFromFile">Конфликты, в которых выбрано «Взять из файла»; остальные — «Оставить текущее».</param>
    /// <param name="secrets">Расшифрованные секреты файла по их ключам; пусто, если секретов в файле нет.</param>
    /// <param name="tables">Таблицы установленных раскладок.</param>
    /// <param name="newGuid">Источник случайных значений для идентификаторов и ключей секретов.</param>
    /// <param name="currentSecrets">Текущие секреты по ключам: для сравнения секретных сокращений по тексту.</param>
    /// <returns>Новые настройки и что надо сделать с хранилищем секретов.</returns>
    public static ImportOutcome Apply(
        AppSettings current,
        AppSettings imported,
        ImportMode mode,
        IReadOnlySet<ConflictKey> takeFromFile,
        IReadOnlyDictionary<string, string> secrets,
        IReadOnlyList<LayoutTable> tables,
        Func<Guid> newGuid,
        IReadOnlyDictionary<string, string>? currentSecrets = null)
    {
        var context = new ApplyContext(secrets, newGuid);
        return mode == ImportMode.Replace
            ? ApplyReplace(current, imported, context)
            : ApplyMerge(current, imported, takeFromFile, tables, context, new SecretTexts(currentSecrets ?? _emptySecrets, secrets));
    }

    /// <summary>Пустой набор секретов.</summary>
    private static readonly Dictionary<string, string> _emptySecrets = [];

    /// <summary>Настройки из файла целиком; прежние секреты больше не нужны.</summary>
    private static ImportOutcome ApplyReplace(AppSettings current, AppSettings imported, ApplyContext context)
    {
        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        var rules = imported.Rules.Select(r => r with { Id = context.UniqueId(r.Id, ruleIds, "r-") }).ToList();
        var snippetIds = new HashSet<string>(StringComparer.Ordinal);
        var snippets = imported.Snippets.Select(s => context.Import(s with { Id = context.UniqueId(s.Id, snippetIds, "a-") })).ToList();
        var settings = imported with { Rules = rules, Snippets = snippets };
        var obsolete = current.Snippets.Select(s => s.SecretRef).OfType<string>().ToList();
        var report = new ImportReport(rules.Count, snippets.Count, settings.Exclusions.Count, 0, 0, 0, context.WithoutSecret);
        return new ImportOutcome(settings, context.NewSecrets, obsolete, report);
    }

    /// <summary>Добавляет новое из файла к текущему, спрашивая о конфликтах заранее выбранными ответами.</summary>
    private static ImportOutcome ApplyMerge(
        AppSettings current,
        AppSettings imported,
        IReadOnlySet<ConflictKey> takeFromFile,
        IReadOnlyList<LayoutTable> tables,
        ApplyContext context,
        SecretTexts known)
    {
        int addedRules = 0, addedSnippets = 0, addedExclusions = 0, identical = 0, replaced = 0, kept = 0;
        var obsolete = new List<string>();

        var removedRules = new HashSet<int>();
        var newRules = new List<Rule>();
        for (var i = 0; i < imported.Rules.Count; i++)
        {
            var rule = imported.Rules[i];
            if (IsIdenticalToAny(rule, current.Rules))
            {
                identical++;
                continue;
            }

            var clashes = RuleClashes(rule, current.Rules);
            if (clashes.Count > 0)
            {
                if (!takeFromFile.Contains(new ConflictKey(ConflictKind.Rule, i)))
                {
                    kept++;
                    continue;
                }

                removedRules.UnionWith(clashes);
                replaced += clashes.Count;
            }

            newRules.Add(rule);
            addedRules++;
        }

        var ruleIds = new HashSet<string>(
            current.Rules.Where((_, index) => !removedRules.Contains(index)).Select(r => r.Id), StringComparer.Ordinal);
        var rules = current.Rules.Where((_, index) => !removedRules.Contains(index)).ToList();
        rules.AddRange(newRules.Select(r => r with { Id = context.UniqueId(r.Id, ruleIds, "r-") }));

        var snippetContext = SnippetContext.Create(tables);
        var removedSnippets = new HashSet<int>();
        var newSnippets = new List<Snippet>();
        for (var i = 0; i < imported.Snippets.Count; i++)
        {
            var snippet = imported.Snippets[i];
            if (IsIdenticalToAny(snippet, current.Snippets, known))
            {
                identical++;
                continue;
            }

            var clashes = SnippetClashes(snippet, current.Snippets, snippetContext);
            if (clashes.Count > 0)
            {
                if (!takeFromFile.Contains(new ConflictKey(ConflictKind.Snippet, i)))
                {
                    kept++;
                    continue;
                }

                removedSnippets.UnionWith(clashes);
                replaced += clashes.Count;
            }

            newSnippets.Add(snippet);
            addedSnippets++;
        }

        obsolete.AddRange(removedSnippets.Select(index => current.Snippets[index].SecretRef).OfType<string>());
        var snippetIds = new HashSet<string>(
            current.Snippets.Where((_, index) => !removedSnippets.Contains(index)).Select(s => s.Id), StringComparer.Ordinal);
        var snippets = current.Snippets.Where((_, index) => !removedSnippets.Contains(index)).ToList();
        snippets.AddRange(newSnippets.Select(s => context.Import(s with { Id = context.UniqueId(s.Id, snippetIds, "a-") })));

        var exclusions = current.Exclusions.ToList();
        for (var i = 0; i < imported.Exclusions.Count; i++)
        {
            var exclusion = imported.Exclusions[i];
            var index = exclusions.FindIndex(e => SameProgram(e, exclusion));
            if (index < 0)
            {
                exclusions.Add(exclusion);
                addedExclusions++;
            }
            else if (SameFlags(exclusions[index], exclusion))
            {
                identical++;
            }
            else if (takeFromFile.Contains(new ConflictKey(ConflictKind.Exclusion, i)))
            {
                exclusions[index] = exclusion;
                replaced++;
            }
            else
            {
                kept++;
            }
        }

        var settings = current with { Rules = rules, Snippets = snippets, Exclusions = exclusions };
        var report = new ImportReport(addedRules, addedSnippets, addedExclusions, identical, replaced, kept, context.WithoutSecret);
        return new ImportOutcome(settings, context.NewSecrets, obsolete, report);
    }

    /// <summary>Номера текущих включённых правил, чьи клавиши и программы пересекаются с правилом из файла.</summary>
    private static List<int> RuleClashes(Rule imported, IReadOnlyList<Rule> currentRules)
    {
        // Пустой Id: иначе проверка приняла бы правило с таким же Id за то же самое и промолчала.
        var issues = TriggerChecks.Check(imported with { Id = string.Empty }, currentRules, LanguageHotkeys.None);
        var clashes = new List<int>();
        foreach (var issue in issues.Where(i => i.Kind == TriggerIssueKind.Busy && i.Other is not null))
        {
            for (var index = 0; index < currentRules.Count; index++)
            {
                if (ReferenceEquals(currentRules[index], issue.Other))
                {
                    clashes.Add(index);
                }
            }
        }

        return clashes;
    }

    /// <summary>Номера текущих включённых сокращений с теми же клавишами, что у сокращения из файла.</summary>
    private static List<int> SnippetClashes(Snippet imported, IReadOnlyList<Snippet> currentSnippets, SnippetContext context)
    {
        var clashes = new List<int>();
        if (!imported.Enabled)
        {
            return clashes;
        }

        for (var index = 0; index < currentSnippets.Count; index++)
        {
            if (currentSnippets[index].Enabled && context.SameKeys(imported.Keys, currentSnippets[index].Keys))
            {
                clashes.Add(index);
            }
        }

        return clashes;
    }

    /// <summary>Есть ли среди текущих такое же правило (кроме идентификатора).</summary>
    private static bool IsIdenticalToAny(Rule rule, IReadOnlyList<Rule> current)
    {
        var text = SettingsJson.SerializeRule(rule with { Id = string.Empty });
        return current.Any(c => SettingsJson.SerializeRule(c with { Id = string.Empty }) == text);
    }

    /// <summary>
    /// Есть ли среди текущих такое же сокращение (кроме идентификатора и ключа секрета). У секретных сокращений сравниваются
    /// тексты секретов; если текст хотя бы одного неизвестен, сокращения считаются разными.
    /// </summary>
    private static bool IsIdenticalToAny(Snippet snippet, IReadOnlyList<Snippet> current, SecretTexts known)
    {
        var shape = SettingsJson.SerializeSnippet(snippet with { Id = string.Empty, SecretRef = null, Text = snippet.Secret ? null : snippet.Text });
        var importedSecret = snippet.Secret ? known.ImportedText(snippet) : null;
        return current.Any(c =>
            c.Secret == snippet.Secret
            && SettingsJson.SerializeSnippet(c with { Id = string.Empty, SecretRef = null, Text = c.Secret ? null : c.Text }) == shape
            && (!snippet.Secret || (importedSecret is not null && string.Equals(importedSecret, known.CurrentText(c), StringComparison.Ordinal))));
    }

    /// <summary>Тексты секретов текущих настроек и файла для сравнения секретных сокращений.</summary>
    /// <param name="current">Текущие секреты по ключам.</param>
    /// <param name="file">Секреты файла по ключам.</param>
    private sealed class SecretTexts(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string> file)
    {
        /// <summary>Текст секрета текущего сокращения.</summary>
        /// <param name="snippet">Текущее сокращение.</param>
        /// <returns>Текст или <c>null</c>, если его нет.</returns>
        public string? CurrentText(Snippet snippet) =>
            snippet.SecretRef is { } key && current.TryGetValue(key, out var text) ? text : null;

        /// <summary>Текст секрета сокращения из файла: открытый или расшифрованный.</summary>
        /// <param name="snippet">Сокращение из файла.</param>
        /// <returns>Текст или <c>null</c>, если его нет.</returns>
        public string? ImportedText(Snippet snippet) =>
            !string.IsNullOrEmpty(snippet.Text) ? snippet.Text
            : snippet.SecretRef is { } key && file.TryGetValue(key, out var text) ? text : null;
    }

    /// <summary>Это одна и та же программа в списке исключений.</summary>
    private static bool SameProgram(ExclusionEntry a, ExclusionEntry b) =>
        string.Equals(a.Process, b.Process, StringComparison.OrdinalIgnoreCase);

    /// <summary>Одинаковы ли галочки двух исключений одной программы.</summary>
    private static bool SameFlags(ExclusionEntry a, ExclusionEntry b) =>
        a.LayoutSwitch == b.LayoutSwitch && a.Correction == b.Correction && a.Snippets == b.Snippets && a.Rules == b.Rules;

    /// <summary>Сравнение сокращений по клавишам: шаблоны по таблицам раскладок, а если сокращение не набирается — по тексту.</summary>
    private sealed class SnippetContext
    {
        /// <summary>Таблицы раскладок.</summary>
        private readonly IReadOnlyList<LayoutTable> _tables;

        /// <summary>Клавиши, у которых Shift не сравнивается.</summary>
        private readonly bool[] _ignoreShift;

        /// <summary>Создаёт контекст.</summary>
        /// <param name="tables">Таблицы раскладок.</param>
        private SnippetContext(IReadOnlyList<LayoutTable> tables)
        {
            _tables = tables;
            _ignoreShift = SnippetKeys.BuildIgnoreShift(tables);
        }

        /// <summary>Создаёт контекст по таблицам.</summary>
        /// <param name="tables">Таблицы раскладок.</param>
        /// <returns>Контекст.</returns>
        public static SnippetContext Create(IReadOnlyList<LayoutTable> tables) => new(tables);

        /// <summary>Совпадают ли клавиши двух сокращений.</summary>
        /// <param name="a">Первое сокращение.</param>
        /// <param name="b">Второе сокращение.</param>
        /// <returns><c>true</c>, если набираются одними и теми же клавишами.</returns>
        public bool SameKeys(string a, string b)
        {
            var left = SnippetKeys.Parse(a, _tables, _ignoreShift);
            var right = SnippetKeys.Parse(b, _tables, _ignoreShift);
            return left is not null && right is not null
                ? SnippetKeys.Compare(left, right) == SnippetRelation.Same
                : string.Equals(a, b, StringComparison.Ordinal);
        }
    }

    /// <summary>Состояние одной загрузки: новые идентификаторы и перенос секретов под новыми ключами.</summary>
    private sealed class ApplyContext(IReadOnlyDictionary<string, string> secrets, Func<Guid> newGuid)
    {
        /// <summary>Секреты для записи в хранилище.</summary>
        private readonly Dictionary<string, string> _newSecrets = [];

        /// <summary>Секреты для записи в хранилище под новыми ключами.</summary>
        public IReadOnlyDictionary<string, string> NewSecrets => _newSecrets;

        /// <summary>Сколько секретных сокращений пришло без текста.</summary>
        public int WithoutSecret { get; private set; }

        /// <summary>Возвращает идентификатор, которого ещё нет среди занятых; свой оставляет, если он свободен.</summary>
        /// <param name="id">Идентификатор из файла.</param>
        /// <param name="taken">Занятые идентификаторы; пополняется.</param>
        /// <param name="prefix">Начало нового идентификатора.</param>
        /// <returns>Свободный идентификатор.</returns>
        public string UniqueId(string id, HashSet<string> taken, string prefix)
        {
            var result = id.Length > 0 && !taken.Contains(id) ? id : RuleIds.Next(taken, newGuid, prefix);
            taken.Add(result);
            return result;
        }

        /// <summary>
        /// Подготавливает сокращение из файла: секретный текст получает новый ключ в хранилище; нет текста — сокращение
        /// выключается, потому что вставлять нечего.
        /// </summary>
        /// <param name="snippet">Сокращение из файла.</param>
        /// <returns>Сокращение для настроек.</returns>
        public Snippet Import(Snippet snippet)
        {
            if (!snippet.Secret)
            {
                return snippet with { SecretRef = null };
            }

            // Открытый text в обычном settings.json — тоже секрет (как при запуске, SecretSealer); приоритетнее ссылки: он явно задан.
            var plain = !string.IsNullOrEmpty(snippet.Text) ? snippet.Text
                : snippet.SecretRef is { } oldKey && secrets.TryGetValue(oldKey, out var sealedText) ? sealedText : null;
            if (plain is not null)
            {
                var key = "s-" + newGuid().ToString("N");
                _newSecrets[key] = plain;
                return snippet with { Text = null, SecretRef = key };
            }

            WithoutSecret++;
            return snippet with { Text = null, SecretRef = null, Enabled = false };
        }
    }
}
