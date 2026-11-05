using System.Text.Json;
using System.Text.Json.Nodes;

namespace KeyMend.Storage.Transfer;

/// <summary>
/// Приводит JSON настроек старой версии схемы к текущей (SPEC F8) до обычного разбора. Каждая миграция превращает версию
/// <c>N</c> в <c>N + 1</c>. Файл без <c>schemaVersion</c> считается версией 1. Текст, который не надо менять (текущая версия, новее
/// текущей, не JSON), возвращается как есть, чтобы разбор сообщил об ошибке с прежним номером строки.
/// </summary>
/// <param name="currentVersion">Версия схемы, которую понимает сборка.</param>
/// <param name="migrations">Миграции по номеру версии, из которой они переводят в следующую.</param>
internal sealed class SchemaMigrator(int currentVersion, IReadOnlyDictionary<int, Action<JsonObject>> migrations)
{
    /// <summary>Параметры разбора: как у файла настроек, допускаются комментарии и висячие запятые.</summary>
    private static readonly JsonDocumentOptions _documentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Мигратор для настроек этой сборки. Миграций пока нет: схема v1 единственная.</summary>
    public static SchemaMigrator Default { get; } = new(AppSettings.CurrentSchemaVersion, new Dictionary<int, Action<JsonObject>>());

    /// <summary>Читает версию схемы из текста файла, не меняя его.</summary>
    /// <param name="json">Содержимое файла настроек.</param>
    /// <returns>Версия схемы; 1, если поля нет; <c>null</c>, если текст не разбирается как объект JSON.</returns>
    public static int? ReadVersion(string json)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: _documentOptions) is JsonObject root ? VersionOf(root) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Применяет миграции, если версия файла старше текущей.</summary>
    /// <param name="json">Содержимое файла настроек.</param>
    /// <returns>Текст настроек текущей версии или исходный текст, если миграция не нужна или невозможна.</returns>
    public string Migrate(string json)
    {
        JsonObject root;
        try
        {
            if (JsonNode.Parse(json, documentOptions: _documentOptions) is not JsonObject parsed)
            {
                return json;
            }

            root = parsed;
        }
        catch (JsonException)
        {
            return json;
        }

        var version = VersionOf(root);
        if (version >= currentVersion)
        {
            return json;
        }

        for (; version < currentVersion; version++)
        {
            if (migrations.TryGetValue(version, out var migrate))
            {
                migrate(root);
            }
        }

        root["schemaVersion"] = currentVersion;
        return root.ToJsonString();
    }

    /// <summary>Версия схемы объекта; без поля или с не числом — 1.</summary>
    /// <param name="root">Корень настроек.</param>
    /// <returns>Версия схемы.</returns>
    private static int VersionOf(JsonObject root) =>
        root["schemaVersion"] is JsonValue value && value.TryGetValue<int>(out var version) ? version : 1;
}
