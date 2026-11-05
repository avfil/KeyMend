using System.Text.Json;
using System.Text.Json.Serialization;
using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>
/// Читает и пишет <see cref="Trigger"/> в виде <c>{ "kind": "Tap", "keys": "RCtrl", "hold": "Ctrl", "maxHoldMs": 150, "doubleMs": 200 }</c>
/// или <c>{ "kind": "Press", "keys": "LCtrl+RCtrl+1" }</c>. Некорректный триггер — <see cref="JsonException"/> без содержимого строки.
/// </summary>
internal sealed class TriggerJsonConverter : JsonConverter<Trigger>
{
    /// <inheritdoc />
    public override Trigger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("trigger must be an object");
        }

        var kind = ReadString(element, "kind") ?? throw new JsonException("trigger.kind is required");
        var keys = ReadString(element, "keys") ?? throw new JsonException("trigger.keys is required");
        var doubleMs = ReadInt(element, "doubleMs") is { } d ? TriggerLimits.ClampDouble(d) : (int?)null;

        if (string.Equals(kind, "Tap", StringComparison.OrdinalIgnoreCase))
        {
            return ReadTap(element, keys, doubleMs);
        }

        if (string.Equals(kind, "Press", StringComparison.OrdinalIgnoreCase))
        {
            if (!KeyChordParser.TryParse(keys, out var chord) || chord.Key == VirtualKey.None)
            {
                throw new JsonException("trigger.keys is not a valid key combination");
            }

            return new PressTrigger { Hold = chord.Modifiers, Key = chord.Key, DoubleMs = doubleMs };
        }

        throw new JsonException("trigger.kind is unknown");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Trigger value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case TapTrigger tap:
                writer.WriteString("kind", "Tap");
                writer.WriteString("keys", KeyChordParser.Format(new KeyChord(tap.Keys, VirtualKey.None)));
                if (tap.Hold != ModifierSet.None)
                {
                    writer.WriteString("hold", KeyChordParser.Format(new KeyChord(tap.Hold, VirtualKey.None)));
                }

                writer.WriteNumber("maxHoldMs", tap.MaxHoldMs);
                break;
            case PressTrigger press:
                writer.WriteString("kind", "Press");
                writer.WriteString("keys", KeyChordParser.Format(new KeyChord(press.Hold, press.Key)));
                break;
            default:
                throw new JsonException("unknown trigger type");
        }

        if (value.DoubleMs is { } doubleMs)
        {
            writer.WriteNumber("doubleMs", doubleMs);
        }

        writer.WriteEndObject();
    }

    /// <summary>Разбирает триггер вида Tap.</summary>
    /// <param name="element">Объект триггера.</param>
    /// <param name="keys">Строка <c>keys</c>.</param>
    /// <param name="doubleMs">Интервал двойного нажатия или <c>null</c>.</param>
    /// <returns>Готовый триггер.</returns>
    private static TapTrigger ReadTap(JsonElement element, string keys, int? doubleMs)
    {
        if (!KeyChordParser.TryParse(keys, out var chord)
            || chord.Key != VirtualKey.None
            || chord.Modifiers == ModifierSet.None
            || ModifierSets.HasAny(chord.Modifiers))
        {
            throw new JsonException("trigger.keys of a Tap must be sided modifiers");
        }

        var hold = ModifierSet.None;
        if (ReadString(element, "hold") is { } holdText)
        {
            if (!KeyChordParser.TryParse(holdText, out var holdChord) || holdChord.Key != VirtualKey.None)
            {
                throw new JsonException("trigger.hold must be modifiers");
            }

            hold = holdChord.Modifiers;
        }

        var maxHold = ReadInt(element, "maxHoldMs") is { } h ? TriggerLimits.ClampHold(h) : TapTrigger.DefaultMaxHoldMs;
        return new TapTrigger { Keys = chord.Modifiers, Hold = hold, MaxHoldMs = maxHold, DoubleMs = doubleMs };
    }

    /// <summary>Читает строковое поле объекта без учёта регистра имени.</summary>
    /// <param name="element">Объект.</param>
    /// <param name="name">Имя поля.</param>
    /// <returns>Строка или <c>null</c>, если поля нет.</returns>
    /// <exception cref="JsonException">Поле есть, но не строка.</exception>
    private static string? ReadString(JsonElement element, string name)
    {
        var property = Find(element, name);
        if (property is not { } value)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new JsonException($"trigger.{name} must be a string");
    }

    /// <summary>Читает целочисленное поле объекта без учёта регистра имени.</summary>
    /// <param name="element">Объект.</param>
    /// <param name="name">Имя поля.</param>
    /// <returns>Число или <c>null</c>, если поля нет.</returns>
    /// <exception cref="JsonException">Поле есть, но не целое число.</exception>
    private static int? ReadInt(JsonElement element, string name)
    {
        var property = Find(element, name);
        if (property is not { } value)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new JsonException($"trigger.{name} must be an integer");
    }

    /// <summary>Ищет поле объекта без учёта регистра имени.</summary>
    /// <param name="element">Объект.</param>
    /// <param name="name">Имя поля.</param>
    /// <returns>Значение поля или <c>null</c>.</returns>
    private static JsonElement? Find(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }
}
