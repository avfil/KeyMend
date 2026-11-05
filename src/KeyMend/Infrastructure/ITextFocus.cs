namespace KeyMend.Infrastructure;

/// <summary>Что показал UI Automation про выделение в элементе с фокусом.</summary>
internal enum SelectionState
{
    /// <summary>Элемент не отдаёт выделение (нет <c>TextPattern</c>, ошибка, таймаут): надо действовать запасным способом.</summary>
    Unavailable,

    /// <summary>Выделения нет, стоит только каретка.</summary>
    Caret,

    /// <summary>Текст выделен.</summary>
    Selected,
}

/// <summary>Итог попытки выделить текст от каретки до начала строки.</summary>
internal enum LineSelectResult
{
    /// <summary>UI Automation не помог (нет <c>TextPattern</c>, ошибка, таймаут): надо действовать запасным способом.</summary>
    Unavailable,

    /// <summary>Уже что-то выделено: «до начала строки» ничего не делает.</summary>
    HasSelection,

    /// <summary>Исправлять нечего: каретка в начале строки или перед ней одни пробелы.</summary>
    Empty,

    /// <summary>Текст до начала строки длиннее допустимого.</summary>
    TooLong,

    /// <summary>Текст от начала строки до каретки выделен.</summary>
    Selected,
}

/// <summary>
/// Доступ к тексту окна с фокусом (SPEC F3): есть ли выделение, выделение фрагмента от начала строки до каретки,
/// повторное выделение вставленного текста. Вызывается из потока функций; запросы в чужие процессы идут с таймаутом.
/// Содержимое текста наружу не раскрывается и в лог не пишется.
/// </summary>
internal interface ITextFocus
{
    /// <summary>Находится ли на переднем плане консоль: там исправление не работает, Ctrl+C прервал бы команду.</summary>
    /// <returns><c>true</c> для консольных окон.</returns>
    bool IsTerminalForeground();

    /// <summary>Спрашивает у элемента с фокусом, выделен ли текст.</summary>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Состояние выделения.</returns>
    Task<SelectionState> GetSelectionStateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Выделяет текст от каретки до начала строки (до предыдущего перевода строки): диапазон абзаца обрезается по каретке.
    /// Выделение выставляется только при <see cref="LineSelectResult.Selected"/>.
    /// </summary>
    /// <param name="maxChars">Наибольшая длина текста.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns>Итог попытки.</returns>
    Task<LineSelectResult> SelectToLineStartAsync(int maxChars, CancellationToken cancellationToken);

    /// <summary>
    /// Выделяет текст, который стоит прямо перед кареткой (только что вставленный результат исправления), и сверяет его
    /// с ожидаемым.
    /// </summary>
    /// <param name="expected">Ожидаемый текст перед кареткой.</param>
    /// <param name="cancellationToken">Отмена при завершении программы.</param>
    /// <returns><c>true</c>, если текст найден и выделен; <c>false</c> — надо выделять клавишами.</returns>
    Task<bool> SelectBeforeCaretAsync(string expected, CancellationToken cancellationToken);
}
