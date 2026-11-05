using System.Globalization;
using KeyMend.Layouts;

namespace KeyMend.UI.Settings;

/// <summary>Названия языков по тегам из настроек для показа в окне.</summary>
internal static class LanguageNames
{
    /// <summary>Название языка без страны, с большой буквы: «Русский», «Английский».</summary>
    /// <param name="tag">Тег вида <c>ru-RU</c>.</param>
    /// <returns>Название на языке интерфейса; если тег не распознан, сам тег.</returns>
    public static string Short(string tag)
    {
        if (!LayoutTags.TryGetLanguageId(tag, out var languageId))
        {
            return tag;
        }

        return Capitalize(Name(CultureInfo.GetCultureInfo(languageId).Parent), tag);
    }

    /// <summary>Название языка со страной: «Английский (США)». Нужно, когда установлены два варианта одного языка.</summary>
    /// <param name="tag">Тег вида <c>en-US</c>.</param>
    /// <returns>Название на языке интерфейса; если тег не распознан, сам тег.</returns>
    public static string Full(string tag) => LayoutTags.TryGetLanguageId(tag, out var languageId)
        ? Capitalize(Name(CultureInfo.GetCultureInfo(languageId)), tag)
        : tag;

    /// <summary>
    /// Название культуры на языке интерфейса KeyMend. <see cref="CultureInfo.DisplayName"/> зависит от языка установленной Windows,
    /// а не от выбранного в KeyMend, поэтому при английском интерфейсе берётся английское имя.
    /// </summary>
    private static string Name(CultureInfo culture) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? culture.DisplayName : culture.EnglishName;

    /// <summary>Делает первую букву заглавной; пустое название заменяется запасным.</summary>
    private static string Capitalize(string name, string fallback) =>
        name.Length == 0 ? fallback : char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..];
}
