using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Reads a <see cref="Rect"/> list tolerantly so intents persisted before rooms/protection became a
/// <b>union</b> still load: a JSON <b>array</b> is the current shape, a single <b>object</b>
/// (<c>{minX,minZ,maxX,maxZ}</c>) is the legacy single-rect shape, and <c>null</c>/absent is an empty list.
/// Without this, deserializing an old single-object <c>protection</c>/<c>room</c> into a <c>List&lt;Rect&gt;</c>
/// throws (object → array), which 400s the intent GET and shows the author an empty map. Always writes a
/// plain array, so a re-save migrates the blob forward.
/// </summary>
public sealed class RectListJsonConverter : JsonConverter<List<Rect>>
{
    // Opt into null handling so a legacy `"protection": null` reaches Read (→ empty list) instead of STJ
    // setting the property to null itself (which would NRE the generators' `.Count`).
    public override bool HandleNull => true;

    public override List<Rect> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return new();
            case JsonTokenType.StartObject:               // legacy single rect
                return new() { JsonSerializer.Deserialize<Rect>(ref reader, options) };
            case JsonTokenType.StartArray:
                var list = new List<Rect>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    list.Add(JsonSerializer.Deserialize<Rect>(ref reader, options));
                return list;
            default:
                throw new JsonException($"Unexpected token {reader.TokenType} reading a Rect list.");
        }
    }

    public override void Write(Utf8JsonWriter writer, List<Rect> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var r in value) JsonSerializer.Serialize(writer, r, options);
        writer.WriteEndArray();
    }
}
