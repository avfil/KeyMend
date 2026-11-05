using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Перестановка и удаление шагов в списке редактора; список не изменяется, возвращается новый.</summary>
internal static class StepListOps
{
    /// <summary>Сдвигает шаг на место выше или ниже.</summary>
    /// <param name="steps">Шаги.</param>
    /// <param name="index">Номер шага с нуля.</param>
    /// <param name="delta">На сколько мест: −1 выше, +1 ниже.</param>
    /// <returns>Новый список; тот же, если сдвигать некуда.</returns>
    public static IReadOnlyList<Step> Move(IReadOnlyList<Step> steps, int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= steps.Count || target < 0 || target >= steps.Count || delta == 0)
        {
            return steps;
        }

        var result = steps.ToList();
        (result[index], result[target]) = (result[target], result[index]);
        return result;
    }

    /// <summary>Убирает шаг.</summary>
    /// <param name="steps">Шаги.</param>
    /// <param name="index">Номер шага с нуля.</param>
    /// <returns>Новый список; тот же, если номера нет.</returns>
    public static IReadOnlyList<Step> RemoveAt(IReadOnlyList<Step> steps, int index)
    {
        if (index < 0 || index >= steps.Count)
        {
            return steps;
        }

        return [.. steps.Where((_, i) => i != index)];
    }
}
