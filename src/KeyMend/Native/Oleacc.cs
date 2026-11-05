using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>
/// MSAA (<c>oleacc.dll</c>): поиск текстовой каретки. Указатель на COM-объект возвращается как <c>nint</c>;
/// превращать его в <c>IAccessible</c> и освобождать должен вызывающий код.
/// </summary>
internal static partial class Oleacc
{
    /// <summary>Идентификатор интерфейса <c>IAccessible</c> (<c>IID_IAccessible</c>).</summary>
    public static readonly Guid IidAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");

    /// <summary>Получает указатель на объект доступности окна (например, каретку при <see cref="Win32.ObjIdCaret"/>); 0 (S_OK) — успех.</summary>
    [LibraryImport("oleacc.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial int AccessibleObjectFromWindow(nint hwnd, uint dwId, in Guid riid, out nint ppvObject);
}
