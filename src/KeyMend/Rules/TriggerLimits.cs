namespace KeyMend.Rules;

/// <summary>Допустимые границы временных настроек триггера (SPEC 7.3). Значения вне границ приводятся к ближайшей, без ошибки.</summary>
internal static class TriggerLimits
{
    /// <summary>Наименьшее удержание для tap, мс.</summary>
    public const int MinHoldMs = 50;

    /// <summary>Наибольшее удержание для tap, мс.</summary>
    public const int MaxHoldMs = 1000;

    /// <summary>Интервал двойного нажатия по умолчанию, мс (SPEC 3.1).</summary>
    public const int DefaultDoubleMs = 300;

    /// <summary>Наименьший интервал двойного нажатия, мс.</summary>
    public const int MinDoubleMs = 100;

    /// <summary>Наибольший интервал двойного нажатия, мс.</summary>
    public const int MaxDoubleMs = 1000;

    /// <summary>Приводит удержание к допустимому диапазону.</summary>
    /// <param name="value">Значение из файла.</param>
    /// <returns>Значение в диапазоне.</returns>
    public static int ClampHold(int value) => Math.Clamp(value, MinHoldMs, MaxHoldMs);

    /// <summary>Приводит интервал двойного нажатия к допустимому диапазону.</summary>
    /// <param name="value">Значение из файла.</param>
    /// <returns>Значение в диапазоне.</returns>
    public static int ClampDouble(int value) => Math.Clamp(value, MinDoubleMs, MaxDoubleMs);
}
