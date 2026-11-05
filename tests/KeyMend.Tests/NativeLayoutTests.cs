using System.Runtime.InteropServices;
using KeyMend.Native;

namespace KeyMend.Tests;

/// <summary>Проверки, на которых держится P/Invoke: размеры структур x64.</summary>
public sealed class NativeLayoutTests
{
    /// <summary>SendInput молча отказывает, если cbSize не равен sizeof(INPUT) = 40 на x64.</summary>
    [Fact]
    public void NativeInput_X64_HasSize40()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(40, Marshal.SizeOf<NativeInput>());
    }

    /// <summary>Размеры структур хука и GUI-потока.</summary>
    [Fact]
    public void HookStructs_X64_HaveDocumentedSizes()
    {
        Assert.Equal(24, Marshal.SizeOf<KbdLlHookStruct>());
        Assert.Equal(32, Marshal.SizeOf<MsLlHookStruct>());
        Assert.Equal(72, Marshal.SizeOf<GuiThreadInfo>());
        Assert.Equal(8, Marshal.SizeOf<LastInputInfo>());
    }

    /// <summary>Shell_NotifyIcon проверяет cbSize: у NOTIFYICONDATAW на x64 это 976 байт.</summary>
    [Fact]
    public void NotifyIconData_X64_HasSize976()
    {
        Assert.Equal(976, Marshal.SizeOf<NotifyIconData>());
    }

    /// <summary>GetMonitorInfo требует точного cbSize.</summary>
    [Fact]
    public void MonitorInfo_X64_HasSize40()
    {
        Assert.Equal(40, Marshal.SizeOf<MonitorInfo>());
    }
}
