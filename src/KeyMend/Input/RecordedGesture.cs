using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>Жест, записанный при настройке триггера (SPEC 3.1, п. 5), или отмена записи.</summary>
/// <param name="Keys">Нажимаемые модификаторы (для tap); пусто, если есть основная клавиша.</param>
/// <param name="Hold">Удерживаемые модификаторы.</param>
/// <param name="Key">Основная клавиша или <see cref="VirtualKey.None"/> для tap.</param>
/// <param name="Cancelled">Запись отменена клавишей Esc, жеста нет.</param>
internal readonly record struct RecordedGesture(ModifierSet Keys, ModifierSet Hold, VirtualKey Key, bool Cancelled = false)
{
    /// <summary>Отмена записи.</summary>
    public static RecordedGesture Cancel => new(ModifierSet.None, ModifierSet.None, VirtualKey.None, true);

    /// <summary>Собирает триггер из жеста; временны́е настройки остаются значениями по умолчанию.</summary>
    /// <returns>Сочетание, если была основная клавиша, иначе tap.</returns>
    /// <exception cref="InvalidOperationException">Жест — отмена.</exception>
    public Trigger ToTrigger()
    {
        if (Cancelled)
        {
            throw new InvalidOperationException("A cancelled recording has no trigger");
        }

        return Key != VirtualKey.None
            ? new PressTrigger { Hold = Hold, Key = Key }
            : new TapTrigger { Keys = Keys, Hold = Hold };
    }
}

/// <summary>Что сделал записывающий автомат с событием клавиши.</summary>
/// <param name="Suppress">Не пускать событие в систему.</param>
/// <param name="Gesture">Записанный жест или отмена, если запись закончилась на этом событии.</param>
internal readonly record struct RecorderOutcome(bool Suppress, RecordedGesture? Gesture = null);
