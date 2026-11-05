using System.Text.Json;
using System.Text.Json.Serialization;
using KeyMend.Input;

namespace KeyMend.Rules;

/// <summary>Читает и пишет <see cref="KeyChord"/> строкой вида <c>Ctrl+Enter</c>; плохая строка — <see cref="JsonException"/> без её содержимого.</summary>
internal sealed class KeyChordJsonConverter : JsonConverter<KeyChord>
{
    /// <inheritdoc />
    public override KeyChord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || !KeyChordParser.TryParse(reader.GetString(), out var chord)
            || chord.Key == VirtualKey.None)
        {
            throw new JsonException("keys is not a valid key combination");
        }

        return chord;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, KeyChord value, JsonSerializerOptions options) =>
        writer.WriteStringValue(KeyChordParser.Format(value));
}
