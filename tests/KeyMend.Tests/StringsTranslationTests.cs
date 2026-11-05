using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using KeyMend.Resources;

namespace KeyMend.Tests;

/// <summary>Английский перевод <c>Strings.en.resx</c> совпадает с русскими строками по составу и меткам (SPEC F9).</summary>
public sealed partial class StringsTranslationTests
{
    /// <summary>Ключи, где кириллица в английском тексте нужна: названия языков и примеры русской раскладки.</summary>
    private static readonly HashSet<string> _cyrillicAllowed =
    [
        "LanguageRussian", "HelpCorrectWhat", "HelpCorrectLanguages", "HelpSnipLead", "HelpSnipKeys",
    ];

    /// <summary>Метка подстановки <c>{0}</c>.</summary>
    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    /// <summary>Читает все строки ресурса одного языка без подмены языка потока (тесты идут параллельно).</summary>
    /// <param name="culture">Язык; нейтральный (русский) — основной ресурс.</param>
    private static Dictionary<string, string> Load(CultureInfo culture)
    {
        var manager = new ResourceManager("KeyMend.Resources.Strings", typeof(Strings).Assembly);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    /// <summary>У каждой русской строки есть английская и наоборот.</summary>
    [Fact]
    public void English_HasSameKeysAsRussian()
    {
        var ru = Load(CultureInfo.InvariantCulture);
        var en = Load(CultureInfo.GetCultureInfo("en"));

        Assert.Empty(ru.Keys.Except(en.Keys));
        Assert.Empty(en.Keys.Except(ru.Keys));
    }

    /// <summary>Перевод не пустой, метки подстановки и выделение <c>**</c> совпадают с русским текстом.</summary>
    [Fact]
    public void English_KeepsPlaceholdersAndBold()
    {
        var ru = Load(CultureInfo.InvariantCulture);
        var en = Load(CultureInfo.GetCultureInfo("en"));
        var broken = new List<string>();
        foreach (var (key, russian) in ru)
        {
            var english = en[key];
            var same = PlaceholderPattern().Matches(russian).Select(m => m.Value).Order()
                .SequenceEqual(PlaceholderPattern().Matches(english).Select(m => m.Value).Order());
            var bold = Regex.Count(russian, @"\*\*") == Regex.Count(english, @"\*\*");
            if (string.IsNullOrWhiteSpace(english) || !same || !bold)
            {
                broken.Add(key);
            }
        }

        Assert.Empty(broken);
    }

    /// <summary>В английском тексте нет кириллицы, кроме примеров русской раскладки и названия языка.</summary>
    [Fact]
    public void English_HasNoCyrillicExceptAllowed()
    {
        var found = Load(CultureInfo.GetCultureInfo("en"))
            .Where(p => !_cyrillicAllowed.Contains(p.Key) && p.Value.Any(c => c is >= 'Ѐ' and <= 'ӿ'))
            .Select(p => p.Key)
            .ToList();

        Assert.Empty(found);
    }
}
