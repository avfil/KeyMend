using KeyMend.Text;

namespace KeyMend.Layouts;

/// <summary>Таблицы установленных раскладок для проверок автозамены в окне настроек.</summary>
internal interface IInstalledTables
{
    /// <summary>Читает таблицы установленных раскладок.</summary>
    /// <returns>Таблицы в порядке списка Windows; никогда не пуст.</returns>
    IReadOnlyList<LayoutTable> Tables();
}

/// <summary>Таблицы установленных раскладок из системы; если системные таблицы недоступны, берутся запасные EN и RU.</summary>
/// <param name="keyMap">Таблицы раскладок системы или <c>null</c>, если они ещё не построены.</param>
internal sealed class SystemInstalledTables(KeyMap? keyMap) : IInstalledTables
{
    /// <inheritdoc/>
    public IReadOnlyList<LayoutTable> Tables()
    {
        if (keyMap is null)
        {
            return [FallbackLayouts.Us, FallbackLayouts.Ru];
        }

        IReadOnlyList<LayoutTable> tables = [.. LayoutService.GetInstalledLayouts().Select(keyMap.GetTable)];
        return tables.Count > 0 ? tables : [FallbackLayouts.Us, FallbackLayouts.Ru];
    }
}
