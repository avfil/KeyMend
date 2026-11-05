namespace KeyMend.UI;

/// <summary>Показывает подсказку с кодом языка (SPEC F2.2). Вызывается из потока функций.</summary>
internal interface IIndicatorPresenter
{
    /// <summary>Показывает подсказку, если она включена в настройках.</summary>
    /// <param name="code">Код языка, например «RU».</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача: подсказка поставлена в очередь потока UI.</returns>
    Task ShowAsync(string code, CancellationToken cancellationToken);
}
