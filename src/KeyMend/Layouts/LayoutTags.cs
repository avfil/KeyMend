using System.Globalization;

namespace KeyMend.Layouts;

/// <summary>Перевод тегов языка из настроек (<c>ru-RU</c>, <c>en-US</c>) в идентификаторы языка раскладок Windows.</summary>
internal static class LayoutTags
{
    /// <summary>Находит LANGID по тегу конкретной культуры.</summary>
    /// <param name="tag">Тег вида <c>ru-RU</c>; нейтральные (<c>ru</c>) и неизвестные не подходят: у раскладки всегда конкретный язык.</param>
    /// <param name="languageId">Идентификатор языка (например, 0x0419).</param>
    /// <returns><c>true</c>, если тег распознан как конкретная культура.</returns>
    public static bool TryGetLanguageId(string? tag, out int languageId)
    {
        languageId = 0;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(tag, predefinedOnly: true);
            if (culture.IsNeutralCulture || culture.LCID == CultureInfo.InvariantCulture.LCID)
            {
                return false;
            }

            languageId = culture.LCID & 0xFFFF;
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
