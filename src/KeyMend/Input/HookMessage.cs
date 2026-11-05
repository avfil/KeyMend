using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>Что поток хука сообщает потоку функций.</summary>
internal enum HookMessageKind
{
    /// <summary>Сработало правило.</summary>
    RuleFired,

    /// <summary>Нужно только отправить маскирующую клавишу (первое нажатие двойного tap на Alt или Win).</summary>
    MaskOnly,

    /// <summary>После ввода стоит сверить раскладку: её могли сменить Alt+Shift, Win+Space или панель задач (SPEC F2.2).</summary>
    LayoutCheck,

    /// <summary>В колбэке хука произошло исключение.</summary>
    HookError,

    /// <summary>Запись триггера в настройках закончилась: жест или отмена (SPEC 3.1, п. 5).</summary>
    TriggerRecorded,
}

/// <summary>Сообщение из потока хука в поток функций.</summary>
/// <param name="Kind">Вид сообщения.</param>
/// <param name="Rule">Правило для <see cref="HookMessageKind.RuleFired"/>.</param>
/// <param name="SendMask">Отправить маскирующую клавишу, чтобы одиночный Alt или Win не открыл меню окна или «Пуск».</param>
/// <param name="ReleaseKey">Клавиша, чьё отпускание движок подавил: после маски её надо отпустить.</param>
/// <param name="Gesture">Записанный жест для <see cref="HookMessageKind.TriggerRecorded"/>.</param>
internal readonly record struct HookMessage(
    HookMessageKind Kind,
    Rule? Rule = null,
    bool SendMask = false,
    VirtualKey ReleaseKey = VirtualKey.None,
    RecordedGesture? Gesture = null);
