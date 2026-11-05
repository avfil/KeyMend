using System.Globalization;
using System.Resources;

namespace KeyMend.Resources;

/// <summary>
/// Строки интерфейса из <c>Strings.resx</c> (SPEC F9). Класс написан вручную: сгенерированный MSBuild класс не виден проходу
/// разметки WPF. Новая строка — запись в <c>.resx</c> и свойство здесь.
/// </summary>
internal static class Strings
{
    /// <summary>Источник строк; язык выбирается по <see cref="CultureInfo.CurrentUICulture"/>.</summary>
    private static readonly ResourceManager _manager = new("KeyMend.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>Берёт строку по ключу.</summary>
    /// <param name="key">Имя записи в <c>.resx</c>.</param>
    /// <returns>Строка; если записи нет, сам ключ, чтобы пропуск был виден в окне.</returns>
    private static string Get(string key) => _manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Подставляет аргументы в шаблон с метками <c>{0}</c>, <c>{1}</c>; строки лежат в ресурсах.</summary>
    /// <param name="template">Шаблон из ресурсов.</param>
    /// <param name="args">Значения по порядку меток.</param>
    /// <returns>Строка с подставленными значениями.</returns>
    public static string Fill(string template, params string?[] args)
    {
        var result = template;
        for (var i = 0; i < args.Length; i++)
        {
            result = result.Replace("{" + i.ToString(CultureInfo.InvariantCulture) + "}", args[i], StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>Настройки KeyMend</summary>
    public static string WindowTitle => Get(nameof(WindowTitle));

    /// <summary>· администратор</summary>
    public static string WindowTitleAdmin => Get(nameof(WindowTitleAdmin));

    /// <summary>KeyMend</summary>
    public static string AppName => Get(nameof(AppName));

    /// <summary>Найти параметр</summary>
    public static string SearchPlaceholder => Get(nameof(SearchPlaceholder));

    /// <summary>Общие</summary>
    public static string SectionGeneral => Get(nameof(SectionGeneral));

    /// <summary>Правила</summary>
    public static string SectionRules => Get(nameof(SectionRules));

    /// <summary>Автозамена</summary>
    public static string SectionSnippets => Get(nameof(SectionSnippets));

    /// <summary>Подсказка раскладки</summary>
    public static string SectionHint => Get(nameof(SectionHint));

    /// <summary>Исключения</summary>
    public static string SectionExclusions => Get(nameof(SectionExclusions));

    /// <summary>Перенос</summary>
    public static string SectionTransfer => Get(nameof(SectionTransfer));

    /// <summary>О программе</summary>
    public static string SectionAbout => Get(nameof(SectionAbout));

    /// <summary>Работает · обычный режим</summary>
    public static string StatusWorking => Get(nameof(StatusWorking));

    /// <summary>Работает · от администратора</summary>
    public static string StatusWorkingAdmin => Get(nameof(StatusWorkingAdmin));

    /// <summary>На паузе</summary>
    public static string StatusPaused => Get(nameof(StatusPaused));

    /// <summary>Внешний вид</summary>
    public static string GroupAppearance => Get(nameof(GroupAppearance));

    /// <summary>Тема</summary>
    public static string ThemeTitle => Get(nameof(ThemeTitle));

    /// <summary>Цвет окна настроек</summary>
    public static string ThemeDescription => Get(nameof(ThemeDescription));

    /// <summary>Как в Windows</summary>
    public static string ThemeSystem => Get(nameof(ThemeSystem));

    /// <summary>Светлая</summary>
    public static string ThemeLight => Get(nameof(ThemeLight));

    /// <summary>Тёмная</summary>
    public static string ThemeDark => Get(nameof(ThemeDark));

    /// <summary>Язык интерфейса</summary>
    public static string LanguageTitle => Get(nameof(LanguageTitle));

    /// <summary>Язык окна настроек, меню и уведомлений</summary>
    public static string LanguageDescription => Get(nameof(LanguageDescription));

    /// <summary>Как в Windows</summary>
    public static string LanguageSystem => Get(nameof(LanguageSystem));

    /// <summary>Русский</summary>
    public static string LanguageRussian => Get(nameof(LanguageRussian));

    /// <summary>English</summary>
    public static string LanguageEnglish => Get(nameof(LanguageEnglish));

    /// <summary>Меняется сразу, без перезапуска. «Как в Windows» — русский или английский по языку Windows.</summary>
    public static string HelpLanguage => Get(nameof(HelpLanguage));

    /// <summary>Связанные ссылки</summary>
    public static string GroupLinks => Get(nameof(GroupLinks));

    /// <summary>Открыть файл настроек</summary>
    public static string OpenSettingsFile => Get(nameof(OpenSettingsFile));

    /// <summary>Включено</summary>
    public static string TrayEnabled => Get(nameof(TrayEnabled));

    /// <summary>Настройки…</summary>
    public static string TraySettings => Get(nameof(TraySettings));

    /// <summary>Отменить таймер питания</summary>
    public static string TrayCancelTimer => Get(nameof(TrayCancelTimer));

    /// <summary>Автозапуск</summary>
    public static string TrayAutostart => Get(nameof(TrayAutostart));

    /// <summary>Автозапуск от администратора</summary>
    public static string TrayAdminAutostart => Get(nameof(TrayAdminAutostart));

    /// <summary>Открыть папку данных</summary>
    public static string TrayOpenDataFolder => Get(nameof(TrayOpenDataFolder));

    /// <summary>О программе</summary>
    public static string TrayAbout => Get(nameof(TrayAbout));

    /// <summary>Выход</summary>
    public static string TrayExit => Get(nameof(TrayExit));

    /// <summary>KeyMend · администратор</summary>
    public static string TrayTipAdmin => Get(nameof(TrayTipAdmin));

    /// <summary>Пауза</summary>
    public static string TrayTipPaused => Get(nameof(TrayTipPaused));
    /// <summary>Не задано</summary>
    public static string TriggerNotSet => Get(nameof(TriggerNotSet));

    /// <summary>Записать</summary>
    public static string TriggerRecord => Get(nameof(TriggerRecord));

    /// <summary>Записать заново</summary>
    public static string TriggerRerecord => Get(nameof(TriggerRerecord));

    /// <summary>Нажмите клавиши…</summary>
    public static string TriggerRecordingPrompt => Get(nameof(TriggerRecordingPrompt));

    /// <summary>Отмена</summary>
    public static string TriggerCancel => Get(nameof(TriggerCancel));

    /// <summary>Пока идёт запись, правила KeyMend не срабатывают. Esc — отменить.</summary>
    public static string TriggerRecordingNote => Get(nameof(TriggerRecordingNote));

    /// <summary>держать</summary>
    public static string TriggerHoldLabel => Get(nameof(TriggerHoldLabel));

    /// <summary>Одиночное</summary>
    public static string TriggerSingle => Get(nameof(TriggerSingle));

    /// <summary>Двойное</summary>
    public static string TriggerDouble => Get(nameof(TriggerDouble));

    /// <summary>Вид нажатия</summary>
    public static string TriggerKindName => Get(nameof(TriggerKindName));

    /// <summary>Отпустить за</summary>
    public static string TriggerHoldFor => Get(nameof(TriggerHoldFor));

    /// <summary>Второе нажатие в течение</summary>
    public static string TriggerDoubleWithin => Get(nameof(TriggerDoubleWithin));

    /// <summary>{0} с</summary>
    public static string TriggerSecondsLeft => Get(nameof(TriggerSecondsLeft));

    /// <summary>мс</summary>
    public static string TriggerMs => Get(nameof(TriggerMs));

    /// <summary>Открыть это правило</summary>
    public static string TriggerOpenRule => Get(nameof(TriggerOpenRule));

    /// <summary>{0} уже занята правилом «{1}» {2}.</summary>
    public static string TriggerBusy => Get(nameof(TriggerBusy));

    /// <summary>{0} — системное сочетание Windows, его назначить нельзя.</summary>
    public static string TriggerSystem => Get(nameof(TriggerSystem));

    /// <summary>{0} может заодно переключить язык в Windows. Сохранить можно</summary>
    public static string TriggerWindowsSwitch => Get(nameof(TriggerWindowsSwitch));

    /// <summary>Сработает через {0} мс: на {1} есть другое правило</summary>
    public static string TriggerDelaySingle => Get(nameof(TriggerDelaySingle));

    /// <summary>Одиночное правило «{1}» на этих клавишах будет срабатывать через {0} мс</summary>
    public static string TriggerDelayDouble => Get(nameof(TriggerDelayDouble));

    /// <summary>во всех программах</summary>
    public static string TriggerEverywhere => Get(nameof(TriggerEverywhere));

    /// <summary>только в {0}</summary>
    public static string TriggerInPrograms => Get(nameof(TriggerInPrograms));





    /// <summary>Нажимаете клавиши — KeyMend выполняет действие</summary>
    public static string RulesLead => Get(nameof(RulesLead));

    /// <summary>Переключение языка</summary>
    public static string RuleGroupLanguage => Get(nameof(RuleGroupLanguage));

    /// <summary>Исправление ввода</summary>
    public static string RuleGroupCorrection => Get(nameof(RuleGroupCorrection));

    /// <summary>Запуск программ</summary>
    public static string RuleGroupLaunch => Get(nameof(RuleGroupLaunch));

    /// <summary>Питание</summary>
    public static string RuleGroupPower => Get(nameof(RuleGroupPower));

    /// <summary>Другое</summary>
    public static string RuleGroupOther => Get(nameof(RuleGroupOther));

    /// <summary>Все</summary>
    public static string RuleFilterAll => Get(nameof(RuleFilterAll));

    /// <summary>Включённые</summary>
    public static string RuleFilterEnabled => Get(nameof(RuleFilterEnabled));

    /// <summary>Выключенные</summary>
    public static string RuleFilterDisabled => Get(nameof(RuleFilterDisabled));

    /// <summary>Поиск по названию или клавише</summary>
    public static string RulesSearch => Get(nameof(RulesSearch));

    /// <summary>Добавить правило</summary>
    public static string RulesAdd => Get(nameof(RulesAdd));

    /// <summary>Нет правил по такому условию</summary>
    public static string RulesEmpty => Get(nameof(RulesEmpty));

    /// <summary>Вкл.</summary>
    public static string ColEnabled => Get(nameof(ColEnabled));

    /// <summary>Название</summary>
    public static string ColName => Get(nameof(ColName));

    /// <summary>Клавиши</summary>
    public static string ColKeys => Get(nameof(ColKeys));

    /// <summary>Что делает</summary>
    public static string ColAction => Get(nameof(ColAction));

    /// <summary>Где работает</summary>
    public static string ColWhere => Get(nameof(ColWhere));

    /// <summary>Ничего не делает</summary>
    public static string SummaryNoSteps => Get(nameof(SummaryNoSteps));

    /// <summary>{0} и ещё {1}</summary>
    public static string SummaryMore => Get(nameof(SummaryMore));

    /// <summary>{0} ⇄ {1}</summary>
    public static string SummaryLanguagePair => Get(nameof(SummaryLanguagePair));

    /// <summary>Включить: {0}</summary>
    public static string SummaryLanguageSpecific => Get(nameof(SummaryLanguageSpecific));

    /// <summary>Исправить выделенный текст</summary>
    public static string SummaryCorrectSelection => Get(nameof(SummaryCorrectSelection));

    /// <summary>Исправить текст до начала строки</summary>
    public static string SummaryCorrectLine => Get(nameof(SummaryCorrectLine));

    /// <summary>Запустить {0}</summary>
    public static string SummaryLaunch => Get(nameof(SummaryLaunch));

    /// <summary>Нажать {0}</summary>
    public static string SummaryPress => Get(nameof(SummaryPress));

    /// <summary>Пауза {0} мс</summary>
    public static string SummaryDelay => Get(nameof(SummaryDelay));

    /// <summary>Вставить текст</summary>
    public static string SummaryInsert => Get(nameof(SummaryInsert));

    /// <summary>Пауза KeyMend вкл/выкл</summary>
    public static string SummaryServicePause => Get(nameof(SummaryServicePause));

    /// <summary>Открыть настройки</summary>
    public static string SummaryServiceOpen => Get(nameof(SummaryServiceOpen));

    /// <summary>Отменить таймер питания</summary>
    public static string SummaryServiceCancelTimer => Get(nameof(SummaryServiceCancelTimer));

    /// <summary>Сон</summary>
    public static string PowerSleep => Get(nameof(PowerSleep));

    /// <summary>Гибернация</summary>
    public static string PowerHibernate => Get(nameof(PowerHibernate));

    /// <summary>Выключение</summary>
    public static string PowerShutdown => Get(nameof(PowerShutdown));

    /// <summary>Перезагрузка</summary>
    public static string PowerRestart => Get(nameof(PowerRestart));

    /// <summary>Блокировка</summary>
    public static string PowerLock => Get(nameof(PowerLock));

    /// <summary>Выход из системы</summary>
    public static string PowerSignOut => Get(nameof(PowerSignOut));

    /// <summary>{0} сейчас</summary>
    public static string SummaryPowerNow => Get(nameof(SummaryPowerNow));

    /// <summary>{0} через {1} мин</summary>
    public static string SummaryPowerAfter => Get(nameof(SummaryPowerAfter));

    /// <summary>везде</summary>
    public static string WhereEverywhere => Get(nameof(WhereEverywhere));

    /// <summary>только в {0}</summary>
    public static string WhereOnly => Get(nameof(WhereOnly));

    /// <summary>только в {0} +{1}</summary>
    public static string WhereOnlyMore => Get(nameof(WhereOnlyMore));

    /// <summary>Изменить</summary>
    public static string RuleMenuEdit => Get(nameof(RuleMenuEdit));

    /// <summary>Дублировать</summary>
    public static string RuleMenuDuplicate => Get(nameof(RuleMenuDuplicate));

    /// <summary>Удалить</summary>
    public static string RuleMenuDelete => Get(nameof(RuleMenuDelete));

    /// <summary>{0} (копия)</summary>
    public static string RuleCopyName => Get(nameof(RuleCopyName));

    /// <summary>Ещё</summary>
    public static string RuleMore => Get(nameof(RuleMore));

    /// <summary>через {0} мс</summary>
    public static string RuleDelayBadge => Get(nameof(RuleDelayBadge));

    /// <summary>{0}: включено</summary>
    public static string RuleToggleLabel => Get(nameof(RuleToggleLabel));

    /// <summary>Не удалось сохранить настройки. Проверьте, что файл не занят другой программой.</summary>
    public static string RuleSaveFailed => Get(nameof(RuleSaveFailed));

    /// <summary>Правило «{0}» не включено. {1}</summary>
    public static string RuleNotEnabled => Get(nameof(RuleNotEnabled));

    /// <summary>Удалить правило?</summary>
    public static string DeleteRuleTitle => Get(nameof(DeleteRuleTitle));

    /// <summary>Правило «{0}» будет удалено.</summary>
    public static string DeleteRuleText => Get(nameof(DeleteRuleText));

    /// <summary>Удалить</summary>
    public static string DialogDelete => Get(nameof(DialogDelete));

    /// <summary>Отмена</summary>
    public static string DialogCancel => Get(nameof(DialogCancel));

    /// <summary>Переключение языка</summary>
    public static string AddRuleLanguage => Get(nameof(AddRuleLanguage));

    /// <summary>Исправление ввода</summary>
    public static string AddRuleCorrection => Get(nameof(AddRuleCorrection));

    /// <summary>{0} (не установлен)</summary>
    public static string LanguageNotInstalled => Get(nameof(LanguageNotInstalled));

    /// <summary>Новое правило</summary>
    public static string EditorNewRule => Get(nameof(EditorNewRule));

    /// <summary>Название</summary>
    public static string EditorName => Get(nameof(EditorName));

    /// <summary>Включено</summary>
    public static string EditorEnabled => Get(nameof(EditorEnabled));

    /// <summary>Вкл.</summary>
    public static string SwitchOn => Get(nameof(SwitchOn));

    /// <summary>Откл.</summary>
    public static string SwitchOff => Get(nameof(SwitchOff));

    /// <summary>Клавиши</summary>
    public static string EditorKeys => Get(nameof(EditorKeys));

    /// <summary>Где работает</summary>
    public static string EditorWhere => Get(nameof(EditorWhere));

    /// <summary>Во всех программах</summary>
    public static string WhereAll => Get(nameof(WhereAll));

    /// <summary>Только в выбранных</summary>
    public static string WhereSelected => Get(nameof(WhereSelected));

    /// <summary>Имя программы, например chrome.exe</summary>
    public static string ProgramPlaceholder => Get(nameof(ProgramPlaceholder));

    /// <summary>Добавить</summary>
    public static string ProgramAdd => Get(nameof(ProgramAdd));

    /// <summary>Убрать {0}</summary>
    public static string ProgramRemove => Get(nameof(ProgramRemove));

    /// <summary>Что делает</summary>
    public static string EditorSteps => Get(nameof(EditorSteps));

    /// <summary>шаги выполняются по порядку</summary>
    public static string EditorStepsHint => Get(nameof(EditorStepsHint));

    /// <summary>Переключить язык</summary>
    public static string StepSwitchLayout => Get(nameof(StepSwitchLayout));

    /// <summary>Исправить раскладку текста</summary>
    public static string StepCorrect => Get(nameof(StepCorrect));

    /// <summary>Запустить программу</summary>
    public static string StepLaunch => Get(nameof(StepLaunch));

    /// <summary>Питание</summary>
    public static string StepPower => Get(nameof(StepPower));

    /// <summary>Нажать клавишу</summary>
    public static string StepPress => Get(nameof(StepPress));

    /// <summary>Пауза</summary>
    public static string StepDelay => Get(nameof(StepDelay));

    /// <summary>Вставить текст</summary>
    public static string StepInsert => Get(nameof(StepInsert));

    /// <summary>Служебное действие</summary>
    public static string StepService => Get(nameof(StepService));

    /// <summary>Как переключать</summary>
    public static string SwitchHow => Get(nameof(SwitchHow));

    /// <summary>По очереди между двумя языками</summary>
    public static string SwitchPair => Get(nameof(SwitchPair));

    /// <summary>Всегда включать один язык</summary>
    public static string SwitchOne => Get(nameof(SwitchOne));

    /// <summary>Подсказка раскладки</summary>
    public static string SwitchHintLabel => Get(nameof(SwitchHintLabel));

    /// <summary>Показывается при смене языка</summary>
    public static string SwitchHintText => Get(nameof(SwitchHintText));

    /// <summary>Настроить</summary>
    public static string SwitchHintLink => Get(nameof(SwitchHintLink));

    /// <summary>Что исправлять</summary>
    public static string CorrectWhat => Get(nameof(CorrectWhat));

    /// <summary>Выделенный текст</summary>
    public static string CorrectSelection => Get(nameof(CorrectSelection));

    /// <summary>До начала строки</summary>
    public static string CorrectLine => Get(nameof(CorrectLine));

    /// <summary>Языки</summary>
    public static string CorrectLanguages => Get(nameof(CorrectLanguages));

    /// <summary>Переключить язык после исправления</summary>
    public static string CorrectSwitchAfter => Get(nameof(CorrectSwitchAfter));

    /// <summary>Не больше</summary>
    public static string CorrectMax => Get(nameof(CorrectMax));

    /// <summary>символов</summary>
    public static string CorrectChars => Get(nameof(CorrectChars));

    /// <summary>Введите название правила</summary>
    public static string ErrNameEmpty => Get(nameof(ErrNameEmpty));

    /// <summary>Запишите клавиши</summary>
    public static string ErrTriggerMissing => Get(nameof(ErrTriggerMissing));

    /// <summary>Добавьте хотя бы одну программу или выберите «Во всех программах»</summary>
    public static string ErrNoPrograms => Get(nameof(ErrNoPrograms));

    /// <summary>Выберите два разных языка</summary>
    public static string ErrSameLanguages => Get(nameof(ErrSameLanguages));

    /// <summary>Удалить правило</summary>
    public static string DeleteRule => Get(nameof(DeleteRule));

    /// <summary>Сохранить</summary>
    public static string EditorSave => Get(nameof(EditorSave));

    /// <summary>Сохранить изменения?</summary>
    public static string UnsavedTitle => Get(nameof(UnsavedTitle));

    /// <summary>В правиле «{0}» есть несохранённые изменения.</summary>
    public static string UnsavedText => Get(nameof(UnsavedText));

    /// <summary>Сохранить</summary>
    public static string UnsavedSave => Get(nameof(UnsavedSave));

    /// <summary>Не сохранять</summary>
    public static string UnsavedDiscard => Get(nameof(UnsavedDiscard));

    /// <summary>Показывать правила</summary>
    public static string RuleFilterState => Get(nameof(RuleFilterState));

    /// <summary>Выше</summary>
    public static string StepUp => Get(nameof(StepUp));

    /// <summary>Ниже</summary>
    public static string StepDown => Get(nameof(StepDown));

    /// <summary>Удалить шаг</summary>
    public static string StepRemove => Get(nameof(StepRemove));

    /// <summary>Добавить шаг</summary>
    public static string StepAdd => Get(nameof(StepAdd));

    /// <summary>Добавьте хотя бы один шаг</summary>
    public static string ErrNoSteps => Get(nameof(ErrNoSteps));

    /// <summary>Укажите файл программы</summary>
    public static string ErrNoFile => Get(nameof(ErrNoFile));

    /// <summary>Файл указан неверно: без кавычек, а путь без диска — только внутри папки скриптов</summary>
    public static string ErrBadFile => Get(nameof(ErrBadFile));

    /// <summary>Укажите клавишу, например Ctrl+Enter</summary>
    public static string ErrNoKeys => Get(nameof(ErrNoKeys));

    /// <summary>Введите текст для вставки</summary>
    public static string ErrNoText => Get(nameof(ErrNoText));

    /// <summary>Файл</summary>
    public static string LaunchFile => Get(nameof(LaunchFile));

    /// <summary>Обзор…</summary>
    public static string LaunchBrowse => Get(nameof(LaunchBrowse));

    /// <summary>Чем запускать</summary>
    public static string LaunchType => Get(nameof(LaunchType));

    /// <summary>Программа (.exe)</summary>
    public static string LaunchTypeExe => Get(nameof(LaunchTypeExe));

    /// <summary>PowerShell 7 (.ps1)</summary>
    public static string LaunchTypePwsh => Get(nameof(LaunchTypePwsh));

    /// <summary>Windows PowerShell 5.1 (.ps1)</summary>
    public static string LaunchTypePowerShell => Get(nameof(LaunchTypePowerShell));

    /// <summary>Пакетный файл (.cmd, .bat)</summary>
    public static string LaunchTypeCmd => Get(nameof(LaunchTypeCmd));

    /// <summary>Параметры</summary>
    public static string LaunchArgs => Get(nameof(LaunchArgs));

    /// <summary>Окно программы</summary>
    public static string LaunchWindow => Get(nameof(LaunchWindow));

    /// <summary>Обычное</summary>
    public static string LaunchWindowNormal => Get(nameof(LaunchWindowNormal));

    /// <summary>Скрытое</summary>
    public static string LaunchWindowHidden => Get(nameof(LaunchWindowHidden));

    /// <summary>Свёрнутое</summary>
    public static string LaunchWindowMin => Get(nameof(LaunchWindowMin));

    /// <summary>Если уже запущена</summary>
    public static string LaunchRepeat => Get(nameof(LaunchRepeat));

    /// <summary>Не запускать снова</summary>
    public static string LaunchRepeatIgnore => Get(nameof(LaunchRepeatIgnore));

    /// <summary>Запустить ещё одну</summary>
    public static string LaunchRepeatAnother => Get(nameof(LaunchRepeatAnother));

    /// <summary>Закрыть и запустить заново</summary>
    public static string LaunchRepeatStop => Get(nameof(LaunchRepeatStop));

    /// <summary>Ждать завершения</summary>
    public static string LaunchWait => Get(nameof(LaunchWait));

    /// <summary>Рабочая папка</summary>
    public static string LaunchWorkDir => Get(nameof(LaunchWorkDir));

    /// <summary>Папка файла</summary>
    public static string LaunchWorkDirHint => Get(nameof(LaunchWorkDirHint));

    /// <summary>Действие</summary>
    public static string PowerAction => Get(nameof(PowerAction));

    /// <summary>Когда</summary>
    public static string PowerWhen => Get(nameof(PowerWhen));

    /// <summary>Сейчас</summary>
    public static string PowerNow => Get(nameof(PowerNow));

    /// <summary>Через</summary>
    public static string PowerAfter => Get(nameof(PowerAfter));

    /// <summary>мин</summary>
    public static string PowerMinutes => Get(nameof(PowerMinutes));

    /// <summary>Отсчёт 10 с с кнопкой «Отменить»</summary>
    public static string PowerCountdown => Get(nameof(PowerCountdown));

    /// <summary>Гибернация выключена в Windows: шаг ничего не сделает, пока её не включить</summary>
    public static string PowerHibernateOff => Get(nameof(PowerHibernateOff));

    /// <summary>Клавиши</summary>
    public static string PressKeys => Get(nameof(PressKeys));

    /// <summary>Выбрать ▾</summary>
    public static string PressChoose => Get(nameof(PressChoose));

    /// <summary>Стрелки</summary>
    public static string PressMenuArrows => Get(nameof(PressMenuArrows));

    /// <summary>F1–F24</summary>
    public static string PressMenuFKeys => Get(nameof(PressMenuFKeys));

    /// <summary>например, Ctrl+Enter</summary>
    public static string PressPlaceholder => Get(nameof(PressPlaceholder));

    /// <summary>Пауза</summary>
    public static string DelayMs => Get(nameof(DelayMs));

    /// <summary>мс</summary>
    public static string DelayUnit => Get(nameof(DelayUnit));

    /// <summary>Текст</summary>
    public static string InsertText => Get(nameof(InsertText));

    /// <summary>Как вставлять</summary>
    public static string InsertHow => Get(nameof(InsertHow));

    /// <summary>Печатать</summary>
    public static string InsertType => Get(nameof(InsertType));

    /// <summary>Через буфер обмена</summary>
    public static string InsertClipboard => Get(nameof(InsertClipboard));

    /// <summary>Перенос строки в тексте</summary>
    public static string InsertNewline => Get(nameof(InsertNewline));

    /// <summary>Shift+Enter</summary>
    public static string NewlineShiftEnter => Get(nameof(NewlineShiftEnter));

    /// <summary>Enter</summary>
    public static string NewlineEnter => Get(nameof(NewlineEnter));

    /// <summary>Секретный текст: правится в разделе «Автозамена»</summary>
    public static string InsertSecret => Get(nameof(InsertSecret));

    /// <summary>Действие</summary>
    public static string ServiceAction => Get(nameof(ServiceAction));

    /// <summary>Запуск программ</summary>
    public static string AddRuleLaunch => Get(nameof(AddRuleLaunch));

    /// <summary>Питание</summary>
    public static string AddRulePower => Get(nameof(AddRulePower));

    /// <summary>Программы и скрипты (*.exe;*.ps1;*.cmd;*.bat)|*.exe;*.ps1;*.cmd;*.bat|Все файлы (*.*)|*.*</summary>
    public static string LaunchFileFilter => Get(nameof(LaunchFileFilter));

    /// <summary>Наберите сокращение — KeyMend заменит его текстом</summary>
    public static string SnipLead => Get(nameof(SnipLead));

    /// <summary>Память набора</summary>
    public static string SnipBufferTitle => Get(nameof(SnipBufferTitle));

    /// <summary>Сколько последних нажатий помнит автозамена</summary>
    public static string SnipBufferHint => Get(nameof(SnipBufferHint));

    /// <summary>нажатий</summary>
    public static string SnipBufferUnit => Get(nameof(SnipBufferUnit));

    /// <summary>Поиск по сокращению или тексту</summary>
    public static string SnipSearch => Get(nameof(SnipSearch));

    /// <summary>Только секреты</summary>
    public static string SnipOnlySecrets => Get(nameof(SnipOnlySecrets));

    /// <summary>Сокращений: {0} · секретов: {1}</summary>
    public static string SnipCount => Get(nameof(SnipCount));

    /// <summary>Добавить</summary>
    public static string SnipAdd => Get(nameof(SnipAdd));

    /// <summary>Сокращение</summary>
    public static string SnipColKeys => Get(nameof(SnipColKeys));

    /// <summary>Текст</summary>
    public static string SnipColText => Get(nameof(SnipColText));

    /// <summary>После вставки</summary>
    public static string SnipColAfter => Get(nameof(SnipColAfter));

    /// <summary>Как вставлять</summary>
    public static string SnipColHow => Get(nameof(SnipColHow));

    /// <summary>Нет сокращений по такому условию</summary>
    public static string SnipEmpty => Get(nameof(SnipEmpty));

    /// <summary>секрет ••••••••</summary>
    public static string SnipSecretCell => Get(nameof(SnipSecretCell));

    /// <summary>печать</summary>
    public static string SnipHowType => Get(nameof(SnipHowType));

    /// <summary>буфер обмена</summary>
    public static string SnipHowClipboard => Get(nameof(SnipHowClipboard));

    /// <summary>␣ — пробел</summary>
    public static string SnipSpaceNote => Get(nameof(SnipSpaceNote));

    /// <summary>{0}: включено</summary>
    public static string SnipToggleLabel => Get(nameof(SnipToggleLabel));

    /// <summary>Сокращение «{0}» не включено. {1}</summary>
    public static string SnipNotEnabled => Get(nameof(SnipNotEnabled));

    /// <summary>Открыть это сокращение</summary>
    public static string SnipOpenSnippet => Get(nameof(SnipOpenSnippet));

    /// <summary>Новое сокращение</summary>
    public static string SnipEditorNew => Get(nameof(SnipEditorNew));

    /// <summary>Сокращение</summary>
    public static string SnipKeys => Get(nameof(SnipKeys));

    /// <summary>те же клавиши в другой раскладке: {0}</summary>
    public static string SnipInOther => Get(nameof(SnipInOther));

    /// <summary>Секрет</summary>
    public static string SnipSecret => Get(nameof(SnipSecret));

    /// <summary>Для паролей: текст хранится зашифрованным</summary>
    public static string SnipSecretHint => Get(nameof(SnipSecretHint));

    /// <summary>Текст</summary>
    public static string SnipText => Get(nameof(SnipText));

    /// <summary>Текст секрета</summary>
    public static string SnipTextSecretName => Get(nameof(SnipTextSecretName));

    /// <summary>Показать</summary>
    public static string SnipShow => Get(nameof(SnipShow));

    /// <summary>Скрыть</summary>
    public static string SnipHide => Get(nameof(SnipHide));

    /// <summary>Секрет — только через буфер обмена</summary>
    public static string SnipSecretOnlyClipboard => Get(nameof(SnipSecretOnlyClipboard));

    /// <summary>Чтобы чат или форма не отправились раньше времени</summary>
    public static string SnipNewlineHint => Get(nameof(SnipNewlineHint));

    /// <summary>После вставки</summary>
    public static string SnipAfter => Get(nameof(SnipAfter));

    /// <summary>необязательно</summary>
    public static string SnipAfterHint => Get(nameof(SnipAfterHint));

    /// <summary>Удалить сокращение</summary>
    public static string SnipDelete => Get(nameof(SnipDelete));

    /// <summary>Удалить сокращение?</summary>
    public static string DeleteSnipTitle => Get(nameof(DeleteSnipTitle));

    /// <summary>Сокращение «{0}» будет удалено.</summary>
    public static string DeleteSnipText => Get(nameof(DeleteSnipText));

    /// <summary>В сокращении «{0}» есть несохранённые изменения.</summary>
    public static string UnsavedSnipText => Get(nameof(UnsavedSnipText));

    /// <summary>Введите сокращение</summary>
    public static string SnipErrKeysEmpty => Get(nameof(SnipErrKeysEmpty));

    /// <summary>Эти символы нельзя набрать ни в одной установленной раскладке</summary>
    public static string SnipErrUntypable => Get(nameof(SnipErrUntypable));

    /// <summary>Введите текст замены</summary>
    public static string SnipErrTextEmpty => Get(nameof(SnipErrTextEmpty));

    /// <summary>Эти клавиши уже занимает сокращение «{0}».</summary>
    public static string SnipErrTaken => Get(nameof(SnipErrTaken));

    /// <summary>Это сокращение — начало «{0}»: оно сработает первым, а «{0}» не сработает никогда.</summary>
    public static string SnipWarnPrefix => Get(nameof(SnipWarnPrefix));

    /// <summary>«{0}» — начало этого сокращения: «{0}» сработает первым, а это — никогда.</summary>
    public static string SnipWarnStartsWith => Get(nameof(SnipWarnStartsWith));

    /// <summary>Текст секрета не найден в хранилище — введите его заново.</summary>
    public static string SnipSecretMissing => Get(nameof(SnipSecretMissing));

    /// <summary>Не удалось сохранить секрет в зашифрованное хранилище.</summary>
    public static string SnipSecretFailed => Get(nameof(SnipSecretFailed));

    /// <summary>Показывать подсказку раскладки</summary>
    public static string HintEnabledTitle => Get(nameof(HintEnabledTitle));

    /// <summary>Включите, чтобы видеть, когда сменилась раскладка</summary>
    public static string HintEnabledHint => Get(nameof(HintEnabledHint));

    /// <summary>Где показывать</summary>
    public static string HintPositionTitle => Get(nameof(HintPositionTitle));

    /// <summary>У текстового курсора или у указателя мыши</summary>
    public static string HintPositionHint => Get(nameof(HintPositionHint));

    /// <summary>У курсора, иначе у мыши</summary>
    public static string HintPositionCaret => Get(nameof(HintPositionCaret));

    /// <summary>Всегда у мыши</summary>
    public static string HintPositionMouse => Get(nameof(HintPositionMouse));

    /// <summary>Сколько показывать</summary>
    public static string HintDurationTitle => Get(nameof(HintDurationTitle));

    /// <summary>Через это время подсказка исчезнет</summary>
    public static string HintDurationHint => Get(nameof(HintDurationHint));

    /// <summary>мс</summary>
    public static string HintDurationUnit => Get(nameof(HintDurationUnit));

    /// <summary>Только когда язык сменил KeyMend</summary>
    public static string HintOnlyOwnTitle => Get(nameof(HintOnlyOwnTitle));

    /// <summary>Не показывать при Alt+Shift и смене на панели задач</summary>
    public static string HintOnlyOwnHint => Get(nameof(HintOnlyOwnHint));

    /// <summary>Как выглядит</summary>
    public static string HintSampleTitle => Get(nameof(HintSampleTitle));

    /// <summary>Нажмите, чтобы увидеть подсказку на экране</summary>
    public static string HintSampleHint => Get(nameof(HintSampleHint));

    /// <summary>Показать</summary>
    public static string HintShow => Get(nameof(HintShow));

    /// <summary>Правила переключения языка</summary>
    public static string HintLanguageRulesLink => Get(nameof(HintLanguageRulesLink));

    /// <summary>Программы, в которых функции KeyMend не нужны</summary>
    public static string ExclLead => Get(nameof(ExclLead));

    /// <summary>Ввести имя вручную</summary>
    public static string ExclManual => Get(nameof(ExclManual));

    /// <summary>Имя программы, например chrome.exe</summary>
    public static string ExclManualHint => Get(nameof(ExclManualHint));

    /// <summary>Добавить из запущенных</summary>
    public static string ExclFromRunning => Get(nameof(ExclFromRunning));

    /// <summary>Добавить</summary>
    public static string ExclAdd => Get(nameof(ExclAdd));

    /// <summary>Введите имя программы, например chrome.exe: без символов \ / : * ? &quot; &lt; &gt; |</summary>
    public static string ExclInvalidName => Get(nameof(ExclInvalidName));

    /// <summary>{0} уже в списке.</summary>
    public static string ExclDuplicate => Get(nameof(ExclDuplicate));

    /// <summary>Найти программу</summary>
    public static string ExclSearchRunning => Get(nameof(ExclSearchRunning));

    /// <summary>Сейчас запущены</summary>
    public static string ExclRunningNow => Get(nameof(ExclRunningNow));

    /// <summary>Подходящих программ нет</summary>
    public static string ExclNoRunning => Get(nameof(ExclNoRunning));

    /// <summary>программа</summary>
    public static string ExclCountOne => Get(nameof(ExclCountOne));

    /// <summary>программы</summary>
    public static string ExclCountFew => Get(nameof(ExclCountFew));

    /// <summary>программ</summary>
    public static string ExclCountMany => Get(nameof(ExclCountMany));

    /// <summary>Исключений нет: KeyMend работает во всех программах</summary>
    public static string ExclEmpty => Get(nameof(ExclEmpty));

    /// <summary>Программа</summary>
    public static string ExclColProgram => Get(nameof(ExclColProgram));

    /// <summary>Что работает в программе</summary>
    public static string ExclColWorks => Get(nameof(ExclColWorks));

    /// <summary>Переключение языка</summary>
    public static string ExclFeatureLayout => Get(nameof(ExclFeatureLayout));

    /// <summary>Исправление ввода</summary>
    public static string ExclFeatureCorrection => Get(nameof(ExclFeatureCorrection));

    /// <summary>Автозамена</summary>
    public static string ExclFeatureSnippets => Get(nameof(ExclFeatureSnippets));

    /// <summary>Остальные правила</summary>
    public static string ExclFeatureRules => Get(nameof(ExclFeatureRules));

    /// <summary>Удалить {0}</summary>
    public static string ExclDelete => Get(nameof(ExclDelete));

    /// <summary>Работа</summary>
    public static string GeneralGroupWork => Get(nameof(GeneralGroupWork));

    /// <summary>KeyMend включён</summary>
    public static string GeneralEnabledTitle => Get(nameof(GeneralEnabledTitle));

    /// <summary>Включите, чтобы работали все функции KeyMend</summary>
    public static string GeneralEnabledHint => Get(nameof(GeneralEnabledHint));

    /// <summary>Вести журнал работы</summary>
    public static string GeneralLogTitle => Get(nameof(GeneralLogTitle));

    /// <summary>Нужен, только чтобы разобраться, если что-то работает не так</summary>
    public static string GeneralLogHint => Get(nameof(GeneralLogHint));

    /// <summary>Автозапуск</summary>
    public static string GeneralGroupAutostart => Get(nameof(GeneralGroupAutostart));

    /// <summary>Запускать вместе с Windows</summary>
    public static string GeneralAutostartTitle => Get(nameof(GeneralAutostartTitle));

    /// <summary>KeyMend будет запускаться при входе в Windows с обычными правами</summary>
    public static string GeneralAutostartHint => Get(nameof(GeneralAutostartHint));

    /// <summary>Запускать от администратора</summary>
    public static string GeneralAdminTitle => Get(nameof(GeneralAdminTitle));

    /// <summary>Позволяет работать в окнах, запущенных от администратора</summary>
    public static string GeneralAdminHint => Get(nameof(GeneralAdminHint));

    /// <summary>Включить…</summary>
    public static string GeneralAdminEnable => Get(nameof(GeneralAdminEnable));

    /// <summary>Выключить…</summary>
    public static string GeneralAdminDisable => Get(nameof(GeneralAdminDisable));

    /// <summary>Открыть папку с настройками</summary>
    public static string GeneralOpenFolder => Get(nameof(GeneralOpenFolder));

    /// <summary>Версия {0}</summary>
    public static string AboutVersion => Get(nameof(AboutVersion));

    /// <summary>Запущен от администратора</summary>
    public static string AboutAdminBadge => Get(nameof(AboutAdminBadge));

    /// <summary>Сведения</summary>
    public static string AboutGroupInfo => Get(nameof(AboutGroupInfo));

    /// <summary>Приватность</summary>
    public static string AboutPrivacyTitle => Get(nameof(AboutPrivacyTitle));

    /// <summary>KeyMend не выходит в интернет и не записывает, что вы печатаете. В реестр пишет только пункт автозапуска</summary>
    public static string AboutPrivacyText => Get(nameof(AboutPrivacyText));

    /// <summary>Папки приложения</summary>
    public static string AboutGroupFolders => Get(nameof(AboutGroupFolders));

    /// <summary>Программа</summary>
    public static string AboutFolderProgram => Get(nameof(AboutFolderProgram));

    /// <summary>Настройки, секреты и резервные копии</summary>
    public static string AboutFolderData => Get(nameof(AboutFolderData));

    /// <summary>Скрипты</summary>
    public static string AboutFolderScripts => Get(nameof(AboutFolderScripts));

    /// <summary>Журнал работы</summary>
    public static string AboutFolderLog => Get(nameof(AboutFolderLog));

    /// <summary>сейчас пусто, журнал выключен</summary>
    public static string AboutLogOff => Get(nameof(AboutLogOff));

    /// <summary>Открыть</summary>
    public static string AboutOpen => Get(nameof(AboutOpen));

    /// <summary>Реестр Windows</summary>
    public static string AboutGroupRegistry => Get(nameof(AboutGroupRegistry));

    /// <summary>Автозапуск</summary>
    public static string AboutRegistryTitle => Get(nameof(AboutRegistryTitle));

    /// <summary>Если значения «{0}» нет, автозапуск выключен</summary>
    public static string AboutRegistryValue => Get(nameof(AboutRegistryValue));

    /// <summary>Копировать путь</summary>
    public static string AboutCopyPath => Get(nameof(AboutCopyPath));

    /// <summary>Репозиторий</summary>
    public static string AboutRepoTitle => Get(nameof(AboutRepoTitle));

    /// <summary>Исходный код и новые версии.</summary>
    public static string AboutRepoHint => Get(nameof(AboutRepoHint));

    /// <summary>Копировать ссылку</summary>
    public static string AboutCopyLink => Get(nameof(AboutCopyLink));

    /// <summary>Подсказка: </summary>
    public static string HelpButtonName => Get(nameof(HelpButtonName));

    /// <summary>Меняет цвет окна настроек и меню значка в трее.</summary>
    public static string HelpTheme => Get(nameof(HelpTheme));

    /// <summary>Выключите, чтобы на время остановить всё: переключение языка, исправление ввода, автозамену и остальные правила. KeyMend остаётся в трее, клавиши работают как обычно.</summary>
    public static string HelpEnabled => Get(nameof(HelpEnabled));

    /// <summary>Журнал (лог программы) — текстовые файлы, по одному на день, хранятся 7 дней. В нём только события, например «converted 5 chars». То, что вы печатаете, KeyMend не записывает никогда.</summary>
    public static string HelpLog => Get(nameof(HelpLog));

    /// <summary>KeyMend запускается при входе в Windows с обычными правами, сразу в трей, без окна. Для этого в реестре создаётся одна запись (её видно в «О программе»).</summary>
    public static string HelpAutostart => Get(nameof(HelpAutostart));

    /// <summary>Windows защищает программы, запущенные от администратора: обычная программа не может в них переключить язык или исправить текст. Например, это Диспетчер задач или редактор реестра.</summary>
    public static string HelpAdmin => Get(nameof(HelpAdmin));

    /// <summary>При смене языка рядом с текстовым курсором ненадолго появляется подсказка с названием раскладки.</summary>
    public static string HelpHintEnabled => Get(nameof(HelpHintEnabled));

    /// <summary>**У курсора, иначе у мыши** — подсказка всплывает рядом с текстовым курсором. Некоторые браузеры и консоли не сообщают, где курсор. Тогда подсказка всплывает у указателя мыши.</summary>
    public static string HelpHintPosition => Get(nameof(HelpHintPosition));

    /// <summary>Сколько миллисекунд подсказка видна на экране: от 200 до 5000 (1000 мс — одна секунда).</summary>
    public static string HelpHintDuration => Get(nameof(HelpHintDuration));

    /// <summary>Подсказка появится, только если язык переключил сам KeyMend. При смене через Alt+Shift, Win+Пробел или на панели задач подсказки не будет.</summary>
    public static string HelpHintOnlyOwn => Get(nameof(HelpHintOnlyOwn));

    /// <summary>Правила позволяют по нажатию клавиш переключить язык, исправить текст, запустить программу, усыпить компьютер. Шагов может быть несколько, они выполняются по порядку.</summary>
    public static string HelpRulesLead => Get(nameof(HelpRulesLead));

    /// <summary>Нажмите «Записать», затем нажмите клавиши, на которые должно срабатывать правило. Записать можно три вида:</summary>
    public static string HelpKeys => Get(nameof(HelpKeys));

    /// <summary>**Одиночное** — правило сработает, если отпустить клавиши быстрее этого времени. Держали дольше — значит, клавиша была нужна для другого, например для Ctrl+C, и правило не сработает.</summary>
    public static string HelpTime => Get(nameof(HelpTime));

    /// <summary>**Во всех программах** — везде, кроме программ из раздела «Исключения».</summary>
    public static string HelpWhere => Get(nameof(HelpWhere));

    /// <summary>**По очереди между двумя языками** — каждое нажатие включает другой язык пары; если сейчас включён язык не из пары, включается первый.</summary>
    public static string HelpSwitchHow => Get(nameof(HelpSwitchHow));

    /// <summary>Исправляет текст, набранный не в той раскладке.</summary>
    public static string HelpCorrectWhat => Get(nameof(HelpCorrectWhat));

    /// <summary>Между какими раскладками переводить. KeyMend смотрит, каких букв в тексте больше, и переводит в другую раскладку пары. Например, в «ghbdtn» английских букв больше, получится «привет».</summary>
    public static string HelpCorrectLanguages => Get(nameof(HelpCorrectLanguages));

    /// <summary>Когда текст исправлен, KeyMend включает язык, на котором он теперь написан, и показывает подсказку раскладки. Можно сразу печатать дальше.</summary>
    public static string HelpCorrectSwitchAfter => Get(nameof(HelpCorrectSwitchAfter));

    /// <summary>Защита от случайного исправления большого текста: если текста больше, KeyMend его не трогает. От 10 до 1 000 000 символов.</summary>
    public static string HelpCorrectMax => Get(nameof(HelpCorrectMax));

    /// <summary>Программа или скрипт: .exe, .ps1, .cmd, .bat. Указать можно:</summary>
    public static string HelpLaunchFile => Get(nameof(HelpLaunchFile));

    /// <summary>Подбирается по расширению файла. Например, для backup.bat будет выбран «Пакетный файл (.cmd, .bat)», для .ps1 — «PowerShell 7».</summary>
    public static string HelpLaunchType => Get(nameof(HelpLaunchType));

    /// <summary>Что передать программе после имени файла, как в командной строке. Например, /SetOrientation 1 90. Пути с пробелами берите в кавычки: "C:\Мои документы\отчёт.txt".</summary>
    public static string HelpLaunchArgs => Get(nameof(HelpLaunchArgs));

    /// <summary>**Обычное** — как при запуске двойным кликом.</summary>
    public static string HelpLaunchWindow => Get(nameof(HelpLaunchWindow));

    /// <summary>Что делать, если вы нажали клавиши, а программа с прошлого раза ещё работает:</summary>
    public static string HelpLaunchRepeat => Get(nameof(HelpLaunchRepeat));

    /// <summary>Следующий шаг правила начнётся только после того, как программа закроется. Если она завершится с ошибкой, появится уведомление и остальные шаги не выполнятся.</summary>
    public static string HelpLaunchWait => Get(nameof(HelpLaunchWait));

    /// <summary>Папка, в которой программа будет искать файлы, заданные без полного пути. Например, скрипт сохраняет report.txt — с рабочей папкой D:\Отчёты файл окажется там.</summary>
    public static string HelpLaunchWorkDir => Get(nameof(HelpLaunchWorkDir));

    /// <summary>**Сон** — компьютер засыпает, всё остаётся в памяти.</summary>
    public static string HelpPowerAction => Get(nameof(HelpPowerAction));

    /// <summary>**Сейчас** — сразу.</summary>
    public static string HelpPowerWhen => Get(nameof(HelpPowerWhen));

    /// <summary>Перед выключением или перезагрузкой на 10 секунд появится окно с отсчётом и кнопкой «Отменить» — на случай, если клавиши нажаты случайно.</summary>
    public static string HelpPowerCountdown => Get(nameof(HelpPowerCountdown));

    /// <summary>Какие клавиши нажать в активном окне, например Ctrl+Enter, Alt+F4 или Win+D. Другие правила KeyMend на нажатия, которые делает сам KeyMend, не срабатывают.</summary>
    public static string HelpPress => Get(nameof(HelpPress));

    /// <summary>Подождать перед следующим шагом, например, пока откроется окно программы. От 0 до 60 000 мс (минута).</summary>
    public static string HelpDelay => Get(nameof(HelpDelay));

    /// <summary>**Печатать** — KeyMend набирает текст, как с клавиатуры. Работает почти везде, но длинный текст печатается заметно медленнее.</summary>
    public static string HelpInsertHow => Get(nameof(HelpInsertHow));

    /// <summary>Чем KeyMend печатает переносы строк из поля «Текст».</summary>
    public static string HelpInsertNewline => Get(nameof(HelpInsertNewline));

    /// <summary>**Пауза KeyMend** — выключить или снова включить все функции, как «Включено» в меню значка в трее.</summary>
    public static string HelpServiceAction => Get(nameof(HelpServiceAction));

    /// <summary>KeyMend помнит последние нажатые клавиши. Когда они совпадают с сокращением, он стирает сокращение и вставляет текст.</summary>
    public static string HelpSnipLead => Get(nameof(HelpSnipLead));

    /// <summary>Сколько последних нажатий KeyMend держит, чтобы узнать сокращение (20–1000). Набранное хранится только в памяти и стирается при смене окна, клике мышью, Enter и Esc.</summary>
    public static string HelpSnipBuffer => Get(nameof(HelpSnipBuffer));

    /// <summary>Буквы, цифры, знаки и пробел, которые вы набираете. KeyMend запоминает клавиши, а не буквы, поэтому рядом показано, как сокращение выглядит в другой раскладке. Например, «дд » и «ll ».</summary>
    public static string HelpSnipKeys => Get(nameof(HelpSnipKeys));

    /// <summary>Для паролей и других секретов. Текст хранится зашифрованным средствами Windows (прочитать его можно только под вашей учётной записью), в файле настроек остаётся только ссылка.</summary>
    public static string HelpSnipSecret => Get(nameof(HelpSnipSecret));

    /// <summary>Что нажать после вставки текста, например Ctrl+Enter, чтобы отправить сообщение.</summary>
    public static string HelpSnipAfter => Get(nameof(HelpSnipAfter));

    /// <summary>Программы, в которых KeyMend должен вести себя иначе: например, игры или удалённый рабочий стол со своими сочетаниями клавиш. Галочки в строке — что в этой программе всё же работает; без галочек KeyMend в ней ничего не делает.</summary>
    public static string HelpExclLead => Get(nameof(HelpExclLead));

    /// <summary>Правило «{0}»: не удалось запустить программу.</summary>
    public static string LaunchFailed => Get(nameof(LaunchFailed));

    /// <summary>Правило «{0}»: {1}.</summary>
    public static string LaunchRejected => Get(nameof(LaunchRejected));

    /// <summary>в шаге не указан файл</summary>
    public static string LaunchReasonNoFile => Get(nameof(LaunchReasonNoFile));

    /// <summary>файл не найден</summary>
    public static string LaunchReasonFileMissing => Get(nameof(LaunchReasonFileMissing));

    /// <summary>рабочая папка не найдена</summary>
    public static string LaunchReasonWorkDirMissing => Get(nameof(LaunchReasonWorkDirMissing));

    /// <summary>относительный путь выходит за папку скриптов</summary>
    public static string LaunchReasonOutsideScripts => Get(nameof(LaunchReasonOutsideScripts));

    /// <summary>PowerShell 7 (pwsh.exe) не найден</summary>
    public static string LaunchReasonNoPwsh => Get(nameof(LaunchReasonNoPwsh));

    /// <summary>неверные параметры запуска</summary>
    public static string LaunchReasonBad => Get(nameof(LaunchReasonBad));

    /// <summary>Правило «{0}»: программа завершилась с кодом {1}.</summary>
    public static string LaunchExitCode => Get(nameof(LaunchExitCode));

    /// <summary>Гибернация выключена в Windows.</summary>
    public static string NotifyHibernateOff => Get(nameof(NotifyHibernateOff));

    /// <summary>Компьютер выключится через {0} с.</summary>
    public static string PowerCountdownShutdown => Get(nameof(PowerCountdownShutdown));

    /// <summary>Компьютер перезагрузится через {0} с.</summary>
    public static string PowerCountdownRestart => Get(nameof(PowerCountdownRestart));

    /// <summary>сон</summary>
    public static string PowerNameSleep => Get(nameof(PowerNameSleep));

    /// <summary>гибернация</summary>
    public static string PowerNameHibernate => Get(nameof(PowerNameHibernate));

    /// <summary>выключение</summary>
    public static string PowerNameShutdown => Get(nameof(PowerNameShutdown));

    /// <summary>перезагрузка</summary>
    public static string PowerNameRestart => Get(nameof(PowerNameRestart));

    /// <summary>блокировка</summary>
    public static string PowerNameLock => Get(nameof(PowerNameLock));

    /// <summary>выход из системы</summary>
    public static string PowerNameSignOut => Get(nameof(PowerNameSignOut));

    /// <summary>Таймер установлен: {0} через {1} мин, в {2}. Отмена — в меню значка.</summary>
    public static string PowerTimerSet => Get(nameof(PowerTimerSet));

    /// <summary>{0} через {1} с.</summary>
    public static string PowerSoon => Get(nameof(PowerSoon));

    /// <summary>Не удалось выполнить действие: {0}.</summary>
    public static string PowerFailed => Get(nameof(PowerFailed));

    /// <summary>Таймер питания отменён.</summary>
    public static string PowerTimerCancelled => Get(nameof(PowerTimerCancelled));

    /// <summary>{0} в {1} (через {2} мин)</summary>
    public static string PowerTimerAt => Get(nameof(PowerTimerAt));

    /// <summary>{0} в {1} (менее чем через минуту)</summary>
    public static string PowerTimerSoon => Get(nameof(PowerTimerSoon));

    /// <summary>KeyMend уже запущен от имени администратора. Закройте его из трея и запустите снова.</summary>
    public static string AppAdminRunning => Get(nameof(AppAdminRunning));

    /// <summary>Не удалось скопировать в буфер обмена.</summary>
    public static string ErrClipboard => Get(nameof(ErrClipboard));

    /// <summary>Не удалось открыть папку.</summary>
    public static string ErrOpenFolder => Get(nameof(ErrOpenFolder));

    /// <summary>Не удалось установить перехват клавиатуры.</summary>
    public static string ErrHook => Get(nameof(ErrHook));

    /// <summary>Не удалось сохранить секреты в зашифрованном хранилище.</summary>
    public static string ErrSecretsSave => Get(nameof(ErrSecretsSave));

    /// <summary>Секреты сохранены, но settings.json не удалось перезаписать: удалите открытый текст вручную.</summary>
    public static string ErrSecretsPlain => Get(nameof(ErrSecretsPlain));

    /// <summary>Не удалось открыть папку данных.</summary>
    public static string ErrOpenDataFolder => Get(nameof(ErrOpenDataFolder));

    /// <summary>Не удалось открыть settings.json.</summary>
    public static string ErrOpenSettingsFile => Get(nameof(ErrOpenSettingsFile));

    /// <summary>Не удалось изменить автозапуск.</summary>
    public static string ErrAutostart => Get(nameof(ErrAutostart));

    /// <summary>settings.json: пропущено правил — {0} (подробности в логе).</summary>
    public static string SettingsSkipped => Get(nameof(SettingsSkipped));

    /// <summary>settings.json не применён, работают прежние настройки. {0}</summary>
    public static string SettingsRejected => Get(nameof(SettingsRejected));

    /// <summary>Автозапуск от администратора</summary>
    public static string AdminAutostartCaption => Get(nameof(AdminAutostartCaption));

    /// <summary>Будет сделано:</summary>
    public static string AdminAutostartAsk => Get(nameof(AdminAutostartAsk));

    /// <summary>Не удалось включить автозапуск от администратора. Подробности в логе.</summary>
    public static string AdminAutostartInstallFailed => Get(nameof(AdminAutostartInstallFailed));

    /// <summary>Готово. Запустить сейчас версию с правами администратора?</summary>
    public static string AdminAutostartDone => Get(nameof(AdminAutostartDone));

    /// <summary>Выключить автозапуск от администратора? Задача и копия в Program Files будут удалены (потребуется UAC).</summary>
    public static string AdminAutostartRemoveAsk => Get(nameof(AdminAutostartRemoveAsk));

    /// <summary>Не удалось выключить автозапуск от администратора. Подробности в логе.</summary>
    public static string AdminAutostartRemoveFailed => Get(nameof(AdminAutostartRemoveFailed));

    /// <summary>Отменить</summary>
    public static string CountdownCancel => Get(nameof(CountdownCancel));

    /// <summary>Осталось: {0} с</summary>
    public static string CountdownLeft => Get(nameof(CountdownLeft));

    /// <summary>Язык</summary>
    public static string DefaultRuleLang => Get(nameof(DefaultRuleLang));

    /// <summary>Язык (работа)</summary>
    public static string DefaultRuleLangWork => Get(nameof(DefaultRuleLangWork));

    /// <summary>Исправить выделенное</summary>
    public static string DefaultRuleFixSelection => Get(nameof(DefaultRuleFixSelection));

    /// <summary>Исправить до начала строки</summary>
    public static string DefaultRuleFixLine => Get(nameof(DefaultRuleFixLine));

    /// <summary>Сохранить</summary>
    public static string TransferGroupSave => Get(nameof(TransferGroupSave));

    /// <summary>Загрузить</summary>
    public static string TransferGroupLoad => Get(nameof(TransferGroupLoad));

    /// <summary>Резервные копии</summary>
    public static string TransferGroupBackups => Get(nameof(TransferGroupBackups));

    /// <summary>Папка с настройками</summary>
    public static string TransferGroupFolder => Get(nameof(TransferGroupFolder));

    /// <summary>Сохранить настройки в файл</summary>
    public static string TransferSaveTitle => Get(nameof(TransferSaveTitle));

    /// <summary>Чтобы перенести их на другой компьютер</summary>
    public static string TransferSaveHint => Get(nameof(TransferSaveHint));

    /// <summary>Сохранить…</summary>
    public static string TransferSaveButton => Get(nameof(TransferSaveButton));

    /// <summary>В файл .keymend попадают правила, автозамена, исключения, настройки подсказки раскладки и файлы из папки скриптов.</summary>
    public static string HelpTransferSave => Get(nameof(HelpTransferSave));

    /// <summary>Добавить секреты</summary>
    public static string TransferSecretsTitle => Get(nameof(TransferSecretsTitle));

    /// <summary>Пароли из автозамены, защищённые вашим паролем</summary>
    public static string TransferSecretsHint => Get(nameof(TransferSecretsHint));

    /// <summary>Без этой галочки секретные сокращения попадут в файл без текста и на другом компьютере будут выключены.</summary>
    public static string HelpTransferSecrets => Get(nameof(HelpTransferSecrets));

    /// <summary>Пароль</summary>
    public static string TransferPasswordLabel => Get(nameof(TransferPasswordLabel));

    /// <summary>Повторите пароль</summary>
    public static string TransferPasswordRepeat => Get(nameof(TransferPasswordRepeat));

    /// <summary>Пароль короче {0} символов</summary>
    public static string TransferPasswordShort => Get(nameof(TransferPasswordShort));

    /// <summary>Пароли не совпадают</summary>
    public static string TransferPasswordMismatch => Get(nameof(TransferPasswordMismatch));

    /// <summary>Файл сохранён.</summary>
    public static string TransferExportDone => Get(nameof(TransferExportDone));

    /// <summary>Не удалось сохранить файл. Подробности в журнале.</summary>
    public static string TransferExportFailed => Get(nameof(TransferExportFailed));

    /// <summary>Файлы в папке скриптов не помещаются в один файл переноса.</summary>
    public static string TransferExportTooLarge => Get(nameof(TransferExportTooLarge));

    /// <summary>Сохранить настройки</summary>
    public static string TransferSaveDialogTitle => Get(nameof(TransferSaveDialogTitle));

    /// <summary>Файлы KeyMend (*.keymend)|*.keymend</summary>
    public static string TransferSaveFilter => Get(nameof(TransferSaveFilter));

    /// <summary>Выберите файл настроек</summary>
    public static string TransferOpenDialogTitle => Get(nameof(TransferOpenDialogTitle));

    /// <summary>Файлы KeyMend и настройки (*.keymend;*.json)|*.keymend;*.json|Все файлы (*.*)|*.*</summary>
    public static string TransferOpenFilter => Get(nameof(TransferOpenFilter));

    /// <summary>Загрузить настройки из файла</summary>
    public static string TransferLoadTitle => Get(nameof(TransferLoadTitle));

    /// <summary>Текущие настройки сначала сохранятся в резервную копию</summary>
    public static string TransferLoadHint => Get(nameof(TransferLoadHint));

    /// <summary>Выбрать файл…</summary>
    public static string TransferLoadButton => Get(nameof(TransferLoadButton));

    /// <summary>Подходит файл .keymend или обычный settings.json, например стартовый набор.</summary>
    public static string HelpTransferLoad => Get(nameof(HelpTransferLoad));

    /// <summary>Правил: {0} · сокращений: {1} · исключений: {2}</summary>
    public static string TransferFileSummary => Get(nameof(TransferFileSummary));

    /// <summary>Заменить текущие</summary>
    public static string TransferModeReplace => Get(nameof(TransferModeReplace));

    /// <summary>Добавить к текущим</summary>
    public static string TransferModeMerge => Get(nameof(TransferModeMerge));

    /// <summary>**Заменить текущие** — настройки из файла полностью занимают место ваших: общие настройки, правила, автозамена, исключения. Файлы скриптов из архива перезаписывают ваши с теми же именами.</summary>
    public static string HelpTransferMode => Get(nameof(HelpTransferMode));

    /// <summary>Пароль от секретов</summary>
    public static string TransferSecretsPassword => Get(nameof(TransferSecretsPassword));

    /// <summary>Пусто — загрузить без секретов</summary>
    public static string TransferSecretsPasswordHint => Get(nameof(TransferSecretsPasswordHint));

    /// <summary>Совпадают клавиши</summary>
    public static string TransferConflictsTitle => Get(nameof(TransferConflictsTitle));

    /// <summary>Правило «{0}» из файла на тех же клавишах, что у вас: «{1}»</summary>
    public static string TransferConflictRule => Get(nameof(TransferConflictRule));

    /// <summary>Сокращение «{0}» из файла на тех же клавишах, что у вас: «{1}»</summary>
    public static string TransferConflictSnippet => Get(nameof(TransferConflictSnippet));

    /// <summary>Исключение для «{0}» из файла отличается от вашего</summary>
    public static string TransferConflictExclusion => Get(nameof(TransferConflictExclusion));

    /// <summary>Оставить текущее</summary>
    public static string TransferChoiceKeep => Get(nameof(TransferChoiceKeep));

    /// <summary>Взять из файла</summary>
    public static string TransferChoiceTake => Get(nameof(TransferChoiceTake));

    /// <summary>Отмена</summary>
    public static string TransferCancel => Get(nameof(TransferCancel));

    /// <summary>Загрузить</summary>
    public static string TransferApply => Get(nameof(TransferApply));

    /// <summary>Файл не читается или это не файл KeyMend.</summary>
    public static string TransferErrUnreadable => Get(nameof(TransferErrUnreadable));

    /// <summary>В файле нет настроек.</summary>
    public static string TransferErrMissingSettings => Get(nameof(TransferErrMissingSettings));

    /// <summary>Настройки в файле повреждены.</summary>
    public static string TransferErrBadSettings => Get(nameof(TransferErrBadSettings));

    /// <summary>Файл создан более новой версией KeyMend. Обновите программу.</summary>
    public static string TransferErrNewer => Get(nameof(TransferErrNewer));

    /// <summary>Секреты в файле повреждены.</summary>
    public static string TransferErrBadSecrets => Get(nameof(TransferErrBadSecrets));

    /// <summary>В файле есть лишние или небезопасные записи.</summary>
    public static string TransferErrUnexpected => Get(nameof(TransferErrUnexpected));

    /// <summary>Файл слишком большой.</summary>
    public static string TransferErrTooLarge => Get(nameof(TransferErrTooLarge));

    /// <summary>Пароль не подошёл. Ничего не изменилось.</summary>
    public static string TransferFailWrongPassword => Get(nameof(TransferFailWrongPassword));

    /// <summary>Не удалось сделать резервную копию. Ничего не изменилось.</summary>
    public static string TransferFailBackup => Get(nameof(TransferFailBackup));

    /// <summary>Не удалось записать секреты. Настройки не менялись.</summary>
    public static string TransferFailSecrets => Get(nameof(TransferFailSecrets));

    /// <summary>Не удалось записать файлы скриптов. Настройки не менялись.</summary>
    public static string TransferFailScripts => Get(nameof(TransferFailScripts));

    /// <summary>Не удалось записать настройки.</summary>
    public static string TransferFailSettings => Get(nameof(TransferFailSettings));

    /// <summary>Резервная копия повреждена.</summary>
    public static string TransferFailBadBackup => Get(nameof(TransferFailBadBackup));

    /// <summary>Добавлено: правил {0}, сокращений {1}, исключений {2}.</summary>
    public static string TransferDoneAdded => Get(nameof(TransferDoneAdded));

    /// <summary>Загружено: правил {0}, сокращений {1}, исключений {2}.</summary>
    public static string TransferDoneLoaded => Get(nameof(TransferDoneLoaded));

    /// <summary>Заменено ваших: {0}. Оставлено ваших: {1}. Одинаковых пропущено: {2}.</summary>
    public static string TransferDoneReplaced => Get(nameof(TransferDoneReplaced));

    /// <summary>Секретных сокращений без текста: {0}. Они выключены.</summary>
    public static string TransferDoneNoSecret => Get(nameof(TransferDoneNoSecret));

    /// <summary>Файлы скриптов не заменены, у вас другие: {0}.</summary>
    public static string TransferDoneSkippedScripts => Get(nameof(TransferDoneSkippedScripts));

    /// <summary>Не найдены файлы программ у правил: {0}.</summary>
    public static string TransferDoneMissing => Get(nameof(TransferDoneMissing));

    /// <summary>Настройки восстановлены из копии.</summary>
    public static string TransferRestoreDone => Get(nameof(TransferRestoreDone));

    /// <summary>Копии перед загрузкой</summary>
    public static string TransferBackupsTitle => Get(nameof(TransferBackupsTitle));

    /// <summary>Хранятся последние {0}</summary>
    public static string TransferBackupsHint => Get(nameof(TransferBackupsHint));

    /// <summary>Копий пока нет</summary>
    public static string TransferBackupsNone => Get(nameof(TransferBackupsNone));

    /// <summary>{0} · перед загрузкой {1}</summary>
    public static string TransferBackupRow => Get(nameof(TransferBackupRow));

    /// <summary>{0} · перед восстановлением</summary>
    public static string TransferBackupRowPlain => Get(nameof(TransferBackupRowPlain));

    /// <summary>Восстановить</summary>
    public static string TransferRestoreButton => Get(nameof(TransferRestoreButton));

    /// <summary>Восстановить из копии?</summary>
    public static string TransferRestoreAskTitle => Get(nameof(TransferRestoreAskTitle));

    /// <summary>Настройки, секреты и скрипты вернутся к состоянию на {0}. Текущее состояние перед этим тоже сохранится в копию.</summary>
    public static string TransferRestoreAsk => Get(nameof(TransferRestoreAsk));

    /// <summary>Настройки, секреты, скрипты и резервные копии</summary>
    public static string TransferFolderHint => Get(nameof(TransferFolderHint));

    /// <summary>Открыть папку</summary>
    public static string TransferFolderOpen => Get(nameof(TransferFolderOpen));

    /// <summary>Подождите…</summary>
    public static string TransferBusy => Get(nameof(TransferBusy));
}
