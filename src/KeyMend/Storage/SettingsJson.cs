using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using KeyMend.Rules;
using KeyMend.Storage.Transfer;

namespace KeyMend.Storage;

/// <summary>Итог разбора <c>settings.json</c>: настройки либо описание ошибки, плюс предупреждения о пропущенных правилах.</summary>
/// <param name="Settings">Настройки или <c>null</c>, если файл не разобран.</param>
/// <param name="Error">Описание ошибки (с номером строки, если известен) или <c>null</c>.</param>
/// <param name="Warnings">Пропущенные правила и другие замечания; содержимого ввода в них нет.</param>
internal sealed record SettingsParseResult(AppSettings? Settings, string? Error, IReadOnlyList<string> Warnings);

/// <summary>Чистое преобразование настроек в JSON и обратно (SPEC 7.3): без диска, с проверкой и приведением чисел к границам.</summary>
internal static class SettingsJson
{
    /// <summary>Параметры для записи.</summary>
    private static readonly JsonSerializerOptions _writeOptions = CreateOptions(static _ => { });

    /// <summary>Разбирает текст файла настроек.</summary>
    /// <param name="json">Содержимое <c>settings.json</c>; допускаются комментарии и висячие запятые.</param>
    /// <returns>Настройки или ошибка; пропущенные правила попадают в предупреждения.</returns>
    public static SettingsParseResult Parse(string json)
    {
        var warnings = new List<string>();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(SchemaMigrator.Default.Migrate(json), CreateOptions(warnings.Add));
            if (settings is null)
            {
                return new SettingsParseResult(null, "file is empty", warnings);
            }

            if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            {
                return new SettingsParseResult(
                    null, $"schemaVersion {settings.SchemaVersion} is newer than supported {AppSettings.CurrentSchemaVersion}", warnings);
            }

            return new SettingsParseResult(Normalize(settings), null, warnings);
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? string.Create(CultureInfo.InvariantCulture, $"line {line + 1}: ") : string.Empty;
            return new SettingsParseResult(null, where + ex.Message, warnings);
        }
    }

    /// <summary>Записывает настройки в читаемый JSON.</summary>
    /// <param name="settings">Настройки.</param>
    /// <returns>Текст файла.</returns>
    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, _writeOptions);

    /// <summary>Записывает одно правило в тот же JSON, что и в файле: по тексту видно, изменилось ли правило.</summary>
    /// <param name="rule">Правило.</param>
    /// <returns>Текст правила.</returns>
    public static string SerializeRule(Rule rule) => JsonSerializer.Serialize(rule, _writeOptions);

    /// <summary>Записывает одно сокращение в тот же JSON, что и в файле; нужно только для сравнения «изменено ли», наружу не уходит.</summary>
    /// <param name="snippet">Сокращение.</param>
    /// <returns>Текст сокращения.</returns>
    public static string SerializeSnippet(Snippet snippet) => JsonSerializer.Serialize(snippet, _writeOptions);

    /// <summary>Приводит числа к допустимым границам и убирает записи исключений без имени программы.</summary>
    /// <param name="settings">Разобранные настройки.</param>
    /// <returns>Исправленные настройки.</returns>
    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        WordBufferLimit = Math.Clamp(settings.WordBufferLimit, AppSettings.MinWordBufferLimit, AppSettings.MaxWordBufferLimit),
        Indicator = settings.Indicator with
        {
            DurationMs = Math.Clamp(settings.Indicator.DurationMs, IndicatorSettings.MinDurationMs, IndicatorSettings.MaxDurationMs),
        },
        Exclusions = settings.Exclusions.Where(e => !string.IsNullOrWhiteSpace(e.Process)).ToList(),
    };

    /// <summary>Создаёт параметры сериализации: camelCase, перечисления строками, кириллица без экранирования.</summary>
    /// <param name="onSkipped">Получает описания пропущенных правил при чтении.</param>
    /// <returns>Параметры сериализации.</returns>
    private static JsonSerializerOptions CreateOptions(Action<string> onSkipped) => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter(), new LenientListConverter<Rule>("rule", onSkipped), new LenientListConverter<Snippet>("snippet", onSkipped) },
    };
}
