using System.Globalization;
using System.Runtime.CompilerServices;

namespace KeyMend.Tests;

/// <summary>Фиксирует русскую культуру для всего тестового процесса.</summary>
/// <remarks>Большинство тестов проверяет строки нейтрального русского ресурса. Без фиксации результат зависел бы от языка профиля Windows, на котором запущены тесты.</remarks>
internal static class TestCulture
{
    /// <summary>Задаёт русские текущую и UI-культуры до запуска тестов.</summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
    }
}
