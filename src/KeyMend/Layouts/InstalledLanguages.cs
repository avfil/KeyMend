using System.Globalization;

namespace KeyMend.Layouts;

/// <summary>Языки, для которых в Windows установлены раскладки клавиатуры; нужны спискам выбора языка в редакторе правила.</summary>
internal interface IInstalledLanguages
{
    /// <summary>Читает установленные языки.</summary>
    /// <returns>Теги вида <c>ru-RU</c> без повторов в порядке раскладок Windows.</returns>
    IReadOnlyList<string> Tags();
}

/// <summary>Установленные языки по списку раскладок системы.</summary>
internal sealed class WindowsInstalledLanguages : IInstalledLanguages
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Tags() =>
        [.. LayoutService.GetInstalledLayouts()
            .Select(hkl => CultureInfo.GetCultureInfo(LayoutPairSelector.LanguageOf(hkl)).Name)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
}
