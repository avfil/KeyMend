using System.IO;
using KeyMend.Layouts;
using KeyMend.Rules;
using Microsoft.Win32;

namespace KeyMend.Infrastructure;

/// <summary>Источник сочетаний, на которых Windows меняет язык или раскладку (для предупреждения у триггеров).</summary>
internal interface ILanguageHotkeysSource
{
    /// <summary>Читает текущие сочетания.</summary>
    /// <returns>Занятые сочетания; если прочитать не удалось, ничего не занято.</returns>
    LanguageHotkeys Read();
}

/// <summary>Читает <c>HKCU\Keyboard Layout\Toggle</c> (только чтение, SPEC 2.3) и список установленных раскладок.</summary>
internal sealed class RegistryLanguageHotkeys : ILanguageHotkeysSource
{
    /// <summary>Раздел реестра с сочетаниями смены языка.</summary>
    private const string _keyPath = @"Keyboard Layout\Toggle";

    /// <summary>Значение смены языка ввода.</summary>
    private const string _languageValue = "Language Hotkey";

    /// <summary>Значение смены раскладки внутри языка.</summary>
    private const string _layoutValue = "Layout Hotkey";

    /// <summary>Маска идентификатора языка в описателе раскладки.</summary>
    private const long _languageMask = 0xFFFF;

    /// <inheritdoc />
    public LanguageHotkeys Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            var several = SeveralLayoutsInOneLanguage(LayoutService.GetInstalledLayouts());
            return LanguageHotkeys.From(key?.GetValue(_languageValue) as string, key?.GetValue(_layoutValue) as string, several);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return LanguageHotkeys.None;
        }
    }

    /// <summary>Есть ли язык, у которого установлено две раскладки или больше.</summary>
    /// <param name="layouts">Описатели установленных раскладок; младшие 16 бит — идентификатор языка.</param>
    /// <returns><c>true</c>, если есть повтор идентификатора языка.</returns>
    internal static bool SeveralLayoutsInOneLanguage(IEnumerable<nint> layouts) =>
        layouts.GroupBy(handle => (long)handle & _languageMask).Any(group => group.Count() > 1);
}
