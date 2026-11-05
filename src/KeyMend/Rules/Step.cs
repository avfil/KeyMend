using System.Text;
using System.Text.Json.Serialization;
using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>
/// Шаг цепочки правила (SPEC 3.2). В JSON вид шага — поле <c>kind</c>. Новые виды добавляются на своих этапах
/// и не меняют версию схемы.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SwitchLayoutStep), "SwitchLayout")]
[JsonDerivedType(typeof(ServiceStep), "Service")]
[JsonDerivedType(typeof(CorrectStep), "Correct")]
[JsonDerivedType(typeof(InsertTextStep), "InsertText")]
[JsonDerivedType(typeof(PressStep), "Press")]
[JsonDerivedType(typeof(DelayStep), "Delay")]
[JsonDerivedType(typeof(LaunchStep), "Launch")]
[JsonDerivedType(typeof(PowerStep), "Power")]
internal abstract record Step;

/// <summary>Как выбирается раскладка в шаге «Переключить язык» (SPEC F2.1).</summary>
internal enum SwitchMode
{
    /// <summary>Переключение между двумя выбранными раскладками.</summary>
    Pair,

    /// <summary>Включение одной конкретной раскладки.</summary>
    Specific,
}

/// <summary>Шаг «Переключить язык» (SPEC F2.1).</summary>
internal sealed record SwitchLayoutStep : Step
{
    /// <summary>Раскладки пары по умолчанию.</summary>
    private static readonly string[] _defaultPair = ["ru-RU", "en-US"];

    /// <summary>Режим выбора раскладки.</summary>
    public SwitchMode Mode { get; init; } = SwitchMode.Pair;

    /// <summary>Теги языков пары (<c>ru-RU</c>, <c>en-US</c>) для режима <see cref="SwitchMode.Pair"/>.</summary>
    public IReadOnlyList<string> Pair { get; init; } = _defaultPair;

    /// <summary>Тег языка для режима <see cref="SwitchMode.Specific"/>.</summary>
    public string? Layout { get; init; }
}

/// <summary>Служебное действие KeyMend.</summary>
internal enum ServiceAction
{
    /// <summary>Включить или выключить паузу KeyMend.</summary>
    TogglePause,

    /// <summary>Открыть настройки.</summary>
    OpenSettings,

    /// <summary>Отменить таймер питания (SPEC F4.2).</summary>
    CancelPowerTimer,
}

/// <summary>Служебный шаг: пауза KeyMend, открыть настройки.</summary>
internal sealed record ServiceStep : Step
{
    /// <summary>Что сделать.</summary>
    public required ServiceAction Action { get; init; }
}

/// <summary>Что исправляет шаг «Исправить» (SPEC F3). Оба вида берут текст из окна, а не из буфера набранного.</summary>
internal enum CorrectScope
{
    /// <summary>Выделенный текст (3.1а).</summary>
    Selection,

    /// <summary>Текст от каретки до начала строки (3.1б).</summary>
    Line,
}

/// <summary>Где окажется каретка после исправления выделенного (SPEC F3.1а). Для «до начала строки» не используется: там каретка всегда на месте.</summary>
internal enum CaretAfter
{
    /// <summary>Как есть: результат выделен, каретка у левого края выделения (повторное нажатие возвращает текст).</summary>
    AsIs,

    /// <summary>Результат выделен, каретка у правого края, как при выделении слева направо (повторное нажатие возвращает текст).</summary>
    Restore,

    /// <summary>Выделения нет, каретка слева от результата.</summary>
    Left,

    /// <summary>Выделения нет, каретка справа от результата.</summary>
    Right,
}

/// <summary>Шаг «Исправить выделенное / до начала строки»: текст, набранный не в той раскладке, перепечатывается в другой.</summary>
internal sealed record CorrectStep : Step
{
    /// <summary>Наименьший допустимый лимит длины текста.</summary>
    public const int MinChars = 10;

    /// <summary>Наибольший допустимый лимит длины текста.</summary>
    public const int MaxCharsLimit = 1_000_000;

    /// <summary>Лимит длины текста по умолчанию.</summary>
    public const int DefaultMaxChars = 10_000;

    /// <summary>Раскладки пары по умолчанию.</summary>
    private static readonly string[] _defaultPair = ["ru-RU", "en-US"];

    /// <summary>Лимит длины текста, приведённый к допустимым границам.</summary>
    private readonly int _maxChars = DefaultMaxChars;

    /// <summary>Что исправлять.</summary>
    public required CorrectScope Scope { get; init; }

    /// <summary>Теги языков пары (<c>ru-RU</c>, <c>en-US</c>): текст в одной из них печатается в другой.</summary>
    public IReadOnlyList<string> Pair { get; init; } = _defaultPair;

    /// <summary>Самый длинный текст в символах, который шаг исправляет; значение вне границ приводится к ближайшей.</summary>
    public int MaxChars
    {
        get => _maxChars;
        init => _maxChars = Math.Clamp(value, MinChars, MaxCharsLimit);
    }

    /// <summary>Переключать ли раскладку окна на язык результата и показывать подсказку после исправления.</summary>
    public bool SwitchLayout { get; init; } = true;

    /// <summary>Где окажется каретка после исправления выделенного; для <see cref="CorrectScope.Line"/> игнорируется.</summary>
    public CaretAfter CaretAfter { get; init; } = CaretAfter.AsIs;
}

/// <summary>
/// Шаг «Вставить текст» (SPEC F5.3). Текст либо задан прямо, либо лежит в <c>secrets.dat</c> под <see cref="SecretRef"/>.
/// Текст в <see cref="object.ToString"/> не попадает.
/// </summary>
internal sealed record InsertTextStep : Step
{
    /// <summary>Текст; у секретного шага пуст.</summary>
    public string? Text { get; init; }

    /// <summary>Ключ секретного текста в <c>secrets.dat</c>; при его наличии вставка всегда через буфер обмена.</summary>
    public string? SecretRef { get; init; }

    /// <summary>Способ вставки.</summary>
    public InsertMode Insert { get; init; } = InsertMode.Type;

    /// <summary>Разделитель строк при печати.</summary>
    public NewlineMode Newline { get; init; } = NewlineMode.ShiftEnter;

    /// <summary>Сколько набранных клавиш стереть перед вставкой; заполняется только у правила сокращения, в файл не пишется.</summary>
    [JsonIgnore]
    public int Erase { get; init; }

    /// <summary>Печатает только признаки без текста.</summary>
    /// <param name="builder">Приёмник текста.</param>
    /// <returns>Всегда <c>true</c>.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Insert = ").Append(Insert).Append(", Secret = ").Append(SecretRef is not null);
        return true;
    }
}

/// <summary>Шаг «Нажать клавишу или сочетание» (SPEC 3.2), например Ctrl+Enter.</summary>
internal sealed record PressStep : Step
{
    /// <summary>Сочетание: модификаторы и одна клавиша.</summary>
    [JsonConverter(typeof(KeyChordJsonConverter))]
    public required KeyChord Keys { get; init; }
}

/// <summary>Шаг «Пауза» (SPEC 3.2): цепочка ждёт заданное время перед следующим шагом.</summary>
internal sealed record DelayStep : Step
{
    /// <summary>Наибольшая пауза: цепочки короткие, а долгое ожидание держало бы очередь.</summary>
    public const int MaxMs = 60_000;

    /// <summary>Длительность паузы в миллисекундах.</summary>
    private readonly int _ms;

    /// <summary>Длительность паузы в миллисекундах; значение вне границ приводится к ближайшей.</summary>
    public required int Ms
    {
        get => _ms;
        init => _ms = Math.Clamp(value, 0, MaxMs);
    }
}

/// <summary>Чем запускается файл шага «Запустить программу» (SPEC F4.1).</summary>
internal enum LaunchType
{
    /// <summary>Программа <c>.exe</c> напрямую.</summary>
    Exe,

    /// <summary>Скрипт <c>.ps1</c> в Windows PowerShell 5.1 (<c>powershell.exe</c>).</summary>
    PowerShell,

    /// <summary>Скрипт <c>.ps1</c> в PowerShell 7 (<c>pwsh.exe</c>).</summary>
    Pwsh,

    /// <summary>Пакетный файл <c>.cmd</c> или <c>.bat</c> через <c>cmd.exe /c</c>.</summary>
    Cmd,
}

/// <summary>Каким будет окно запущенной программы.</summary>
internal enum LaunchWindow
{
    /// <summary>Как решит сама программа.</summary>
    Normal,

    /// <summary>Скрытое окно.</summary>
    Hidden,

    /// <summary>Свёрнутое окно.</summary>
    Minimized,
}

/// <summary>Что делать, если предыдущий запуск этого шага ещё работает (SPEC F4.1, В9).</summary>
internal enum LaunchRepeat
{
    /// <summary>Не запускать снова.</summary>
    Ignore,

    /// <summary>Запустить ещё одну копию.</summary>
    Another,

    /// <summary>Остановить предыдущий процесс и запустить заново.</summary>
    StopPrevious,
}

/// <summary>
/// Шаг «Запустить программу» (SPEC F4.1). Всегда с правами обычного пользователя (SPEC 2.6). Путь внутри
/// <c>%AppData%\KeyMend\scripts\</c> хранится относительным.
/// </summary>
internal sealed record LaunchStep : Step
{
    /// <summary>Чем запускать файл.</summary>
    public LaunchType Type { get; init; } = LaunchType.Exe;

    /// <summary>Файл: полный путь, путь относительно папки скриптов или (для <c>Exe</c>) имя программы из PATH.</summary>
    public required string File { get; init; }

    /// <summary>Аргументы командной строки.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Рабочая папка; пусто — папка файла.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Окно запущенной программы.</summary>
    public LaunchWindow Window { get; init; } = LaunchWindow.Normal;

    /// <summary>Ждать завершения процесса, прежде чем выполнять следующие шаги цепочки.</summary>
    public bool Wait { get; init; }

    /// <summary>Поведение при повторном запуске, пока предыдущий процесс жив.</summary>
    public LaunchRepeat Repeat { get; init; } = LaunchRepeat.Ignore;
}

/// <summary>Действие шага «Питание» (SPEC F4.2).</summary>
internal enum PowerAction
{
    /// <summary>Сон (не гибернация, даже если гибернация включена).</summary>
    Sleep,

    /// <summary>Гибернация; только если включена в Windows.</summary>
    Hibernate,

    /// <summary>Выключение компьютера.</summary>
    Shutdown,

    /// <summary>Перезагрузка.</summary>
    Restart,

    /// <summary>Блокировка сеанса.</summary>
    Lock,

    /// <summary>Выход из системы.</summary>
    SignOut,
}

/// <summary>
/// Шаг «Питание» (SPEC F4.2): действие сейчас или по таймеру. Таймер в системе один: новый заменяет старый. Выключение и
/// перезагрузка «сейчас» идут с отсчётом, который можно отменить.
/// </summary>
internal sealed record PowerStep : Step
{
    /// <summary>Наибольшая задержка таймера в минутах (сутки).</summary>
    public const int MaxAfterMinutes = 1440;

    /// <summary>Задержка в минутах, приведённая к допустимым границам.</summary>
    private readonly int _afterMinutes;

    /// <summary>Что сделать.</summary>
    public required PowerAction Action { get; init; }

    /// <summary>Через сколько минут; 0 — сейчас. Значение вне границ приводится к ближайшей.</summary>
    public int AfterMinutes
    {
        get => _afterMinutes;
        init => _afterMinutes = Math.Clamp(value, 0, MaxAfterMinutes);
    }

    /// <summary>Показывать ли отсчёт с кнопкой «Отменить» перед выключением и перезагрузкой «сейчас».</summary>
    public bool Countdown { get; init; } = true;
}
