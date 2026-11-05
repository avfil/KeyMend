using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>Решение обработчика по событию: подавить ли клавишу и какое правило запустить.</summary>
/// <param name="Suppress"><c>true</c> — клавиша не попадёт в окно.</param>
/// <param name="Rule">Сработавшее правило или <c>null</c>.</param>
/// <param name="SendMask">Поток функций должен отправить маскирующую клавишу (одиночный Alt/Win, SPEC 3.1).</param>
/// <param name="ReleaseKey">Клавиша, отпускание которой подавлено и которую надо отпустить после маски.</param>
internal readonly record struct HookDecision(
    bool Suppress,
    Rule? Rule,
    bool SendMask = false,
    VirtualKey ReleaseKey = VirtualKey.None)
{
    /// <summary>Пропустить клавишу дальше, ничего не делая.</summary>
    public static HookDecision Pass => default;
}
