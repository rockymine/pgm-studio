using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft.Painting;

/// <summary>
/// A plane the beds of a height stack lean along: blocks of lift per block east (<paramref name="East"/>) and per
/// block south (<paramref name="South"/>), measured from the world origin. Written as the array
/// <c>[east, south]</c>, so <c>[0.5, 0]</c> raises every bed one block for each two blocks east.
/// </summary>
[JsonConverter(typeof(BedDipConverter))]
public readonly record struct BedDip(double East, double South)
{
    /// <summary>The steepest lean either component may state, in blocks of lift per block.</summary>
    public const double MaxSlope = 8;

    /// <summary>The lift at the column <c>(x, z)</c>, each component held to ±<see cref="MaxSlope"/>.</summary>
    public double LiftAt(int x, int z)
        => Math.Clamp(East, -MaxSlope, MaxSlope) * x + Math.Clamp(South, -MaxSlope, MaxSlope) * z;
}

/// <summary>Reads and writes a <see cref="BedDip"/> as the two-number array <c>[east, south]</c>.</summary>
public sealed class BedDipConverter : JsonConverter<BedDip>
{
    public override BedDip Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("dip is the array [east, south]");
        reader.Read();
        var east = reader.GetDouble();
        reader.Read();
        var south = reader.GetDouble();
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("dip is the array [east, south]");
        return new BedDip(east, south);
    }

    public override void Write(Utf8JsonWriter writer, BedDip value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.East);
        writer.WriteNumberValue(value.South);
        writer.WriteEndArray();
    }
}
