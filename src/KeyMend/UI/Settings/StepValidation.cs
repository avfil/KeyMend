using KeyMend.Actions;
using KeyMend.Input;
using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>Что не так с шагом в редакторе.</summary>
internal enum StepProblem
{
    /// <summary>Всё в порядке.</summary>
    None,

    /// <summary>В паре языков выбран один и тот же язык.</summary>
    SameLanguages,

    /// <summary>Не указан файл программы.</summary>
    NoFile,

    /// <summary>Файл указан неверно: кавычки или путь за пределами папки скриптов.</summary>
    BadFile,

    /// <summary>Не указана клавиша для нажатия.</summary>
    NoKeys,

    /// <summary>Нет текста для вставки.</summary>
    NoText,
}

/// <summary>Проверка шага при сохранении правила (SPEC 5). Существование файла не проверяется: он может быть в PATH или появиться позже.</summary>
internal static class StepValidation
{
    /// <summary>Проверяет шаг.</summary>
    /// <param name="step">Шаг.</param>
    /// <param name="scriptsDir">Папка скриптов: относительные пути считаются от неё.</param>
    /// <returns>Первая найденная проблема или <see cref="StepProblem.None"/>.</returns>
    public static StepProblem Check(Step step, string scriptsDir) => step switch
    {
        SwitchLayoutStep { Mode: SwitchMode.Pair, Pair: [var a, var b] } when Same(a, b) => StepProblem.SameLanguages,
        CorrectStep { Pair: [var a, var b] } when Same(a, b) => StepProblem.SameLanguages,
        LaunchStep s => CheckFile(s.File, scriptsDir),
        PressStep s when s.Keys.Key == VirtualKey.None => StepProblem.NoKeys,
        InsertTextStep { SecretRef: null } s when string.IsNullOrEmpty(s.Text) => StepProblem.NoText,
        _ => StepProblem.None,
    };

    /// <summary>Два тега языка совпадают без учёта регистра.</summary>
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Проверяет поле «Файл».</summary>
    private static StepProblem CheckFile(string file, string scriptsDir)
    {
        var trimmed = file.Trim();
        if (trimmed.Length == 0)
        {
            return StepProblem.NoFile;
        }

        return LaunchCommandBuilder.IsFileAllowed(trimmed, scriptsDir) ? StepProblem.None : StepProblem.BadFile;
    }
}
