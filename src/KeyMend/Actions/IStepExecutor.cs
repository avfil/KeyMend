using KeyMend.Rules;

namespace KeyMend.Actions;

/// <summary>Выполняет один шаг цепочки правила (SPEC 3.2). Вызывается из потока функций.</summary>
internal interface IStepExecutor
{
    /// <summary>Выполняет шаг.</summary>
    /// <param name="rule">Правило, которому принадлежит шаг (для сообщений об ошибках).</param>
    /// <param name="step">Шаг.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Задача выполнения шага.</returns>
    Task ExecuteAsync(Rule rule, Step step, CancellationToken cancellationToken);
}
