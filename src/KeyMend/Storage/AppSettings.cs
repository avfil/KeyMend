using KeyMend.Rules;

namespace KeyMend.Storage;

/// <summary>Тема оформления (SPEC F1).</summary>
internal enum AppTheme
{
    /// <summary>Следовать настройке Windows.</summary>
    System,

    /// <summary>Светлая.</summary>
    Light,

    /// <summary>Тёмная.</summary>
    Dark,
}

/// <summary>Язык интерфейса (SPEC F9).</summary>
internal enum AppLanguage
{
    /// <summary>Русский, если Windows на русском, иначе английский.</summary>
    System,

    /// <summary>Русский.</summary>
    Russian,

    /// <summary>Английский.</summary>
    English,
}

/// <summary>Где показывать подсказку с текущим языком (SPEC F2.2).</summary>
internal enum IndicatorPosition
{
    /// <summary>Рядом с кареткой ввода, иначе у мыши.</summary>
    Caret,

    /// <summary>Всегда рядом с указателем мыши.</summary>
    Mouse,
}

/// <summary>Настройки подсказки EN/RU (SPEC F2.2).</summary>
internal sealed record IndicatorSettings
{
    /// <summary>Наименьшая длительность показа, мс.</summary>
    public const int MinDurationMs = 200;

    /// <summary>Наибольшая длительность показа, мс.</summary>
    public const int MaxDurationMs = 5000;

    /// <summary>Длительность показа по умолчанию, мс.</summary>
    public const int DefaultDurationMs = 700;

    /// <summary>Показывать ли подсказку.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Показывать только при переключении через KeyMend, а не при смене раскладки Alt+Shift или с панели задач.</summary>
    public bool OnlyOwnSwitch { get; init; }

    /// <summary>Место показа.</summary>
    public IndicatorPosition Position { get; init; } = IndicatorPosition.Caret;

    /// <summary>Сколько миллисекунд подсказка видна.</summary>
    public int DurationMs { get; init; } = DefaultDurationMs;
}

/// <summary>
/// Настройки пользователя (SPEC 7.3). Отсутствующие в JSON поля получают значения по умолчанию, поэтому добавление
/// нового свойства не ломает существующий <c>settings.json</c>.
/// </summary>
internal sealed record AppSettings
{
    /// <summary>Версия схемы, которую понимает эта сборка.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Наименьший допустимый размер буфера набранного, в клавишах.</summary>
    public const int MinWordBufferLimit = 20;

    /// <summary>Наибольший допустимый размер буфера набранного, в клавишах.</summary>
    public const int MaxWordBufferLimit = 1000;

    /// <summary>Размер буфера набранного по умолчанию, в клавишах.</summary>
    public const int DefaultWordBufferLimit = 200;

    /// <summary>Вести ли журнал работы по умолчанию (SPEC 2.3). В релизе журнал выключен; явное значение в <c>settings.json</c> важнее.</summary>
    public const bool DefaultLogEnabled = false;

    /// <summary>Версия схемы файла.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Вести ли журнал работы (SPEC 2.3). Выключенный журнал не пишет на диск ничего, а файлы журнала удаляются.</summary>
    public bool LogEnabled { get; init; } = DefaultLogEnabled;

    /// <summary>Тема оформления.</summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Язык интерфейса (SPEC F9). Журнал всегда на английском.</summary>
    public AppLanguage UiLanguage { get; init; } = AppLanguage.System;

    /// <summary>Сколько последних клавиш помнит буфер набранного (SPEC 2.3).</summary>
    public int WordBufferLimit { get; init; } = DefaultWordBufferLimit;

    /// <summary>Подсказка EN/RU.</summary>
    public IndicatorSettings Indicator { get; init; } = new();

    /// <summary>Правила: триггер + цепочка шагов.</summary>
    public IReadOnlyList<Rule> Rules { get; init; } = [];

    /// <summary>Сокращения автозамены (SPEC F5).</summary>
    public IReadOnlyList<Snippet> Snippets { get; init; } = [];

    /// <summary>Исключения программ (SPEC F7).</summary>
    public IReadOnlyList<ExclusionEntry> Exclusions { get; init; } = [];
}
