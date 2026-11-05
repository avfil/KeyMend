namespace KeyMend.UI;

/// <summary>Перемещение выделения по меню трея стрелками, Home и End (SPEC F10). Разделители и недоступные пункты пропускаются.</summary>
internal static class TrayMenuNavigator
{
    /// <summary>Индекс «ничего не выделено».</summary>
    public const int None = -1;

    /// <summary>Шаг на соседний доступный пункт по кругу.</summary>
    /// <param name="entries">Строки меню.</param>
    /// <param name="current">Выделенная строка или <see cref="None"/>.</param>
    /// <param name="forward"><c>true</c> — вниз, <c>false</c> — вверх.</param>
    /// <returns>Индекс следующего доступного пункта; <see cref="None"/>, если выбирать нечего.</returns>
    public static int Next(IReadOnlyList<TrayMenuEntry> entries, int current, bool forward)
    {
        var count = entries.Count;
        if (count == 0)
        {
            return None;
        }

        var step = forward ? 1 : -1;
        var index = current is >= 0 && current < count ? current : (forward ? -1 : count);
        for (var i = 0; i < count; i++)
        {
            index = ((index + step) % count + count) % count;
            if (IsSelectable(entries[index]))
            {
                return index;
            }
        }

        return None;
    }

    /// <summary>Первый доступный пункт (клавиша Home).</summary>
    /// <param name="entries">Строки меню.</param>
    /// <returns>Индекс или <see cref="None"/>.</returns>
    public static int First(IReadOnlyList<TrayMenuEntry> entries) => Next(entries, None, forward: true);

    /// <summary>Последний доступный пункт (клавиша End).</summary>
    /// <param name="entries">Строки меню.</param>
    /// <returns>Индекс или <see cref="None"/>.</returns>
    public static int Last(IReadOnlyList<TrayMenuEntry> entries) => Next(entries, None, forward: false);

    /// <summary>Строку можно выделить и выбрать.</summary>
    /// <param name="entry">Строка меню.</param>
    /// <returns><c>true</c> — это доступный пункт.</returns>
    public static bool IsSelectable(TrayMenuEntry entry) => !entry.IsSeparator && entry.IsEnabled;
}
