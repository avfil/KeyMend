using KeyMend.Native;

namespace KeyMend.Input;

/// <summary>Физическое состояние клавиатуры из системы (для <see cref="HotkeyEngine"/>).</summary>
internal static class PhysicalKeyboard
{
    /// <summary>Нажата ли клавиша прямо сейчас (старший бит <c>GetAsyncKeyState</c> — знак результата).</summary>
    public static bool IsDown(VirtualKey key) => User32.GetAsyncKeyState((int)key) < 0;
}
