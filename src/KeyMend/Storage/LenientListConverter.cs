using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyMend.Storage;

/// <summary>
/// Читает список записей (правил или сокращений) по одной: битая запись (неизвестный шаг, плохая строка клавиш)
/// пропускается, остальные работают. Причина пропуска передаётся в <paramref name="onSkipped"/> без содержимого
/// вводимого текста.
/// </summary>
/// <typeparam name="T">Тип записи.</typeparam>
/// <param name="label">Название записи для сообщений: <c>rule</c>, <c>snippet</c>.</param>
/// <param name="onSkipped">Получает описание пропущенной записи.</param>
internal sealed class LenientListConverter<T>(string label, Action<string> onSkipped) : JsonConverter<IReadOnlyList<T>>
    where T : class
{
    /// <inheritdoc />
    public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var array = JsonElement.ParseValue(ref reader);
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"{label}s must be an array");
        }

        var result = new List<T>();
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            index++;
            try
            {
                if (element.Deserialize<T>(options) is { } item)
                {
                    result.Add(item);
                }
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                onSkipped($"{label} #{index}{DescribeName(element)} skipped: {ex.Message}");
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }

    /// <summary>Возвращает « 'имя'» для сообщения, если у записи есть строковое поле <c>name</c> (у сокращений его нет).</summary>
    /// <param name="element">Элемент массива.</param>
    /// <returns>Пустая строка или имя в кавычках.</returns>
    private static string DescribeName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("name", out var name)
            && name.ValueKind == JsonValueKind.String)
        {
            return $" '{name.GetString()}'";
        }

        return string.Empty;
    }
}
