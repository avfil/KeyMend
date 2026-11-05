namespace KeyMend.Layouts;

/// <summary>
/// Чистая логика обнаружения смены раскладки «извне» (Alt+Shift, Win+Space, панель задач; SPEC F2.2). Запоминает окно и
/// его раскладку; о смене сообщает, только если окно то же, а раскладка другая. Смена окна — не повод для подсказки.
/// </summary>
internal sealed class LayoutChangeDetector
{
    /// <summary>Окно, раскладку которого запомнили; 0 — ничего не запомнено.</summary>
    private nint _window;

    /// <summary>Запомненная раскладка окна.</summary>
    private nint _layout;

    /// <summary>Учитывает наблюдение.</summary>
    /// <param name="window">Окно переднего плана.</param>
    /// <param name="layout">Его текущая раскладка.</param>
    /// <returns><c>true</c>, если раскладка того же окна изменилась с прошлого наблюдения.</returns>
    public bool Observe(nint window, nint layout)
    {
        if (window != _window)
        {
            _window = window;
            _layout = layout;
            return false;
        }

        if (layout == _layout)
        {
            return false;
        }

        _layout = layout;
        return true;
    }

    /// <summary>Запоминает раскладку, которую включили мы сами, чтобы ближайшая проверка не приняла её за внешнюю смену.</summary>
    /// <param name="layout">Раскладка, на которую запрошено переключение.</param>
    public void NoteOwnSwitch(nint layout) => _layout = layout;
}
