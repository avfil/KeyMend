using System.Security.Principal;

namespace KeyMend.Infrastructure;

/// <summary>Сведения о правах текущего процесса (SPEC 2.6).</summary>
internal static class Elevation
{
    /// <summary><c>true</c>, если процесс запущен с правами администратора (повышенный токен UAC).</summary>
    public static bool IsElevated { get; } = CheckElevated();

    /// <summary>Проверяет членство токена процесса в группе администраторов (у неповышенного токена она «только для отказа»).</summary>
    private static bool CheckElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
