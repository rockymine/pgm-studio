using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Geom;

/// <summary>
/// Reads and writes a <see cref="CellRect"/> as the four-element array <c>[x, z, w, h]</c> the plan format
/// has always stored, so giving the value a type does not move a byte of <c>plan.json</c>.
///
/// <para>It hangs off the type itself rather than the DTO properties, so every consumer — the plan model,
/// evidence, anything added later — agrees on the wire form by default.</para>
/// </summary>
public sealed class CellRectJsonConverter : JsonConverter<CellRect>
{
    public override CellRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"a cell rect is [x, z, w, h]; got {reader.TokenType}");

        Span<int> v = stackalloc int[4];
        var n = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException($"a cell rect holds numbers; got {reader.TokenType}");
            // Tolerate a longer array by ignoring the tail, matching the bare int[] this replaced: a
            // hand-written plan that carried extra entries loaded before, so it still loads.
            if (n < 4) v[n] = reader.GetInt32();
            n++;
        }
        return new CellRect(v[0], v[1], v[2], v[3]);
    }

    public override void Write(Utf8JsonWriter writer, CellRect value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Z);
        writer.WriteNumberValue(value.Width);
        writer.WriteNumberValue(value.Height);
        writer.WriteEndArray();
    }
}
