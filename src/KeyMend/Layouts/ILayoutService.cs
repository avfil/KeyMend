namespace KeyMend.Layouts;

/// <summary>Чтение и переключение раскладки окна с фокусом (SPEC F2.1). Вызывается из потока функций.</summary>
internal interface ILayoutService
{
    /// <summary>Возвращает раскладку окна с фокусом.</summary>
    /// <returns>Раскладка или <c>null</c>, если окна нет.</returns>
    LayoutInfo? GetCurrent();

    /// <summary>Возвращает раскладку окна с фокусом вместе с окном переднего плана.</summary>
    /// <returns>Снимок или <c>null</c>, если окна нет.</returns>
    LayoutSnapshot? Snapshot();

    /// <summary>Режим «между двумя выбранными»: переключает окно на вторую раскладку пары.</summary>
    /// <param name="languageA">Язык A.</param>
    /// <param name="languageB">Язык B.</param>
    /// <returns>Раскладка, на которую запрошено переключение; <c>null</c> — переключать нечего или некуда.</returns>
    LayoutInfo? SwitchPair(int languageA, int languageB);

    /// <summary>Режим «включить конкретную»: переключает окно на раскладку языка.</summary>
    /// <param name="languageId">Идентификатор языка.</param>
    /// <returns>Раскладка, на которую запрошено переключение; <c>null</c>, если такой нет или запрос не принят.</returns>
    LayoutInfo? SwitchTo(int languageId);

    /// <summary>Выбирает раскладки для исправления ввода по паре языков, ничего не переключая.</summary>
    /// <param name="languageA">Язык A пары.</param>
    /// <param name="languageB">Язык B пары.</param>
    /// <returns>Текущая раскладка и другая раскладка пары; <c>null</c>, если окна нет или целевой раскладки нет в системе.</returns>
    LayoutPairResolution? ResolvePair(int languageA, int languageB);

    /// <summary>Находит первую установленную раскладку языка, ничего не переключая.</summary>
    /// <param name="languageId">Идентификатор языка.</param>
    /// <returns>Раскладка или <c>null</c>, если такой нет.</returns>
    LayoutInfo? FindInstalled(int languageId);
}
