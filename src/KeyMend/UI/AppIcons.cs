using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KeyMend.UI;

/// <summary>
/// Имена встроенных вариантов иконки приложения: обычный, на паузе, от администратора и их комбинация (SPEC F10).
/// Все файлы — многоразмерные ICO, встроенные в сборку.
/// </summary>
internal static class AppIcons
{
    /// <summary>Возвращает изображение для значка окна по видимому состоянию программы.</summary>
    /// <param name="paused">KeyMend на паузе.</param>
    /// <param name="elevated">Программа работает от администратора.</param>
    /// <returns>Кадр ICO, загруженный целиком и не привязанный к файлу на диске.</returns>
    public static ImageSource Window(bool paused, bool elevated)
    {
        var frame = BitmapFrame.Create(ResourceUri(paused, elevated), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }

    /// <summary>Возвращает URI встроенного ресурса выбранного варианта иконки.</summary>
    /// <param name="paused">KeyMend на паузе.</param>
    /// <param name="elevated">Программа работает от администратора.</param>
    /// <returns>Pack-URI ресурса приложения.</returns>
    public static Uri ResourceUri(bool paused, bool elevated) => ResourceUri(File(paused, elevated));

    /// <summary>Возвращает URI встроенного ресурса иконки по имени файла.</summary>
    /// <param name="file">Имя файла без папки.</param>
    /// <returns>Pack-URI ресурса приложения.</returns>
    public static Uri ResourceUri(string file) => new($"pack://application:,,,/KeyMend;component/Assets/{file}", UriKind.Absolute);

    /// <summary>Возвращает имя ICO выбранного варианта.</summary>
    /// <param name="paused">KeyMend на паузе.</param>
    /// <param name="elevated">Программа работает от администратора.</param>
    /// <returns>Имя файла без папки.</returns>
    public static string File(bool paused, bool elevated) => (paused, elevated) switch
    {
        (true, false) => "KeyMendPaused.ico",
        (false, true) => "KeyMendAdmin.ico",
        (true, true) => "KeyMendPausedAdmin.ico",
        _ => "KeyMend.ico",
    };
}
