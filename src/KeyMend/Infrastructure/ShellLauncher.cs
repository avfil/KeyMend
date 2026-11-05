using System.IO;
using KeyMend.Native;

namespace KeyMend.Infrastructure;

/// <summary>Открытие файлов и папок оболочкой Windows (для «Настройки…» и «Открыть папку данных»).</summary>
internal static class ShellLauncher
{
    /// <summary>Наибольшее значение, которое <c>ShellExecute</c> возвращает при ошибке; всё, что больше, — успех.</summary>
    internal const int ShellExecuteErrorMax = 32;

    /// <summary>Открывает файл программой по умолчанию, а если для типа ничего не назначено — в Блокноте.</summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <returns><c>true</c>, если какая-то программа запущена.</returns>
    public static bool OpenFile(string path)
    {
        if (Shell32.ShellExecute(0, "open", path, null, null, Win32.SwShowNormal) > ShellExecuteErrorMax)
        {
            return true;
        }

        return Shell32.ShellExecute(0, "open", "notepad.exe", $"\"{path}\"", null, Win32.SwShowNormal) > ShellExecuteErrorMax;
    }

    /// <summary>Открывает папку в Проводнике; при отсутствии создаёт её.</summary>
    /// <param name="path">Полный путь к папке.</param>
    /// <returns><c>true</c>, если Проводник запущен.</returns>
    public static bool OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        return Shell32.ShellExecute(0, "open", path, null, null, Win32.SwShowNormal) > ShellExecuteErrorMax;
    }
}
