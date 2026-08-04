using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing a map carries: everything the author placed, in the order they placed it. Stored
/// beside the sketch geometry, because a prop's position is as much a part of the map as a shape's.</summary>
public sealed record DressingDoc
{
    public List<PlacedProp> Props { get; init; } = [];

    /// <summary>Nothing placed — what a map that never opened the phase carries, and what makes the pass a
    /// no-op rather than a walk over an empty world.</summary>
    public static DressingDoc Empty { get; } = new();
}

/// <summary>
/// Serialization for placed dressing. The JSON <b>is</b> the record graph round-tripped, the same contract
/// <see cref="TerrainThemeJson"/> holds for a theme: what the canvas writes is the wire format the pass
/// deserializes, so there is no second model of a prop to fall out of step with this one.
///
/// <para>Props are polymorphic on a <c>kind</c> discriminator (<see cref="PlacedProp"/>), which is what lets
/// one list hold a path, a tree and an area without the reader having to sort them first.</para>
/// </summary>
public static class DressingJson
{
    /// <summary>Canonical options: camelCase names, compact, enums by name — a form written by hand or read in
    /// a diff should say <c>"cairn"</c>, not <c>3</c>.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(DressingDoc doc) => JsonSerializer.Serialize(doc, Options);

    public static DressingDoc Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<DressingDoc>(json, Options) ?? DressingDoc.Empty; }
        catch (JsonException) { return DressingDoc.Empty; }   // a hand-edited blob must not fail an export
    }

    public static string SerializeProp(PlacedProp prop) => JsonSerializer.Serialize(prop, Options);

    public static PlacedProp? DeserializeProp(string json)
    {
        try { return JsonSerializer.Deserialize<PlacedProp>(json, Options); }
        catch (JsonException) { return null; }
    }
}
