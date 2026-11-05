using System.Reflection;
using KeyMend.Resources;

namespace KeyMend.Tests;

/// <summary>Строки интерфейса: у каждого свойства <see cref="Strings"/> есть запись в <c>.resx</c>.</summary>
public sealed class StringsTests
{
    /// <summary>Забытая запись возвращает свой ключ: такое свойство ловим тестом, а не в окне.</summary>
    [Fact]
    public void AllProperties_ResolveToResourceValue()
    {
        var missing = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => (string?)p.GetValue(null) == p.Name)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(missing);
    }
}
