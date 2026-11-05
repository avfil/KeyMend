namespace KeyMend.Layouts;

/// <summary>Узнаёт о смене раскладки, которую выполнил сам KeyMend; показывает подсказку и не даёт принять смену за внешнюю.</summary>
internal interface ILayoutNotifier
{
    /// <summary>Сообщает, что KeyMend переключил раскладку.</summary>
    /// <param name="layout">Раскладка, на которую запрошено переключение.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача: подсказка показана или не требуется.</returns>
    Task OnOwnSwitchAsync(LayoutInfo layout, CancellationToken cancellationToken);
}
