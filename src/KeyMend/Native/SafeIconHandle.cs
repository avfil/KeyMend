using Microsoft.Win32.SafeHandles;

namespace KeyMend.Native;

/// <summary>
/// Значок, созданный через <c>CreateIconFromResourceEx</c> без <c>LR_SHARED</c>: освобождается только через
/// <c>DestroyIcon</c> после того, как трей перестал использовать дескриптор.
/// </summary>
internal sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Создаёт владеющий дескриптор значка.</summary>
    /// <param name="handle">Нативный дескриптор HICON.</param>
    public SafeIconHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    /// <summary>Уничтожает значок; общие системные значки сюда не передаются.</summary>
    /// <returns><c>true</c>, если дескриптор освобождён.</returns>
    protected override bool ReleaseHandle() => User32.DestroyIcon(handle);
}
