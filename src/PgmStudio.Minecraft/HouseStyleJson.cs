using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft;

/// <summary>
/// Serialization for a room style (docs/world-export/structures.md §7). The JSON <b>is</b> the record graph
/// round-tripped: each part's course stack and extent, plus the knobs that are not materials — the eave, the
/// roof hole and the door.
///
/// <para>This is the form a map <b>snapshots</b>. A map's bound room style is a copy kept with the map rather
/// than a foreign key into the library, the same rule the applied terrain theme follows (M0011): a library
/// edit must never silently rebuild a shipped map's spawn rooms. Nothing consumed it while a room style was
/// only a library row, which is why it lands with the binding rather than before it.</para>
/// </summary>
public static class HouseStyleJson
{
    /// <summary>Canonical options: camelCase names, compact, enums as their camelCase names — so
    /// <c>eave</c> reads <c>"flush"</c> and <c>door</c> reads <c>"stainedGlassPane"</c> rather than an
    /// ordinal nobody could edit by hand or diff usefully.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(HouseStyle style) => JsonSerializer.Serialize(style, Options);

    public static HouseStyle Deserialize(string json)
        => JsonSerializer.Deserialize<HouseStyle>(Upgraded(json), Options)!;

    /// <summary>
    /// A stored style read forward into the shape the record has now.
    ///
    /// <para><b>A snapshot outlives the shape it was written in.</b> A map keeps its bound style rather than a
    /// key into the library, so every style ever stored is still out there in a layout blob, and a field that
    /// moves has to be carried rather than dropped — the reader falls back to the built-in shell on anything it
    /// cannot make sense of (<see cref="DeserializeOr"/>), so a shape change without an upgrade is not an error
    /// but a map that quietly stops looking like itself. The dressing document has held this contract since it
    /// had props to carry; a style is the other thing a map snapshots, and it had none.</para>
    /// </summary>
    private static JsonNode Upgraded(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new JsonException("empty house style JSON");
        if (node is not JsonObject style) return node;

        // The sill, the floor and its zoning were three fields beside everything else; they are the one thing
        // a building stands on. A sill resolving to air was how "no footing" was said before it was a state.
        if (style["foundation"] is null && (style["sill"] ?? style["floor"] ?? style["surface"]) is not null)
        {
            var foundation = new JsonObject();
            if (style["floor"] is { } plate) foundation["plate"] = plate.DeepClone();
            if (style["surface"] is { } surface) foundation["surface"] = surface.DeepClone();
            foundation["footing"] = IsAir(style["sill"]) ? null : style["sill"]?.DeepClone();
            style["foundation"] = foundation;
        }
        style.Remove("sill");
        style.Remove("floor");
        style.Remove("surface");
        return style;
    }

    /// <summary>Whether a stored material is the bare air one that used to stand in for no footing.</summary>
    private static bool IsAir(JsonNode? material) =>
        material is JsonObject solid
        && solid["kind"]?.GetValue<string>() == "solid"
        && solid["id"]?.GetValue<int>() == Blocks.Air;

    /// <summary>A stored style, or <paramref name="fallback"/> when the text is absent or cannot be read. A
    /// snapshot is a hand-editable leaf inside a map's layout, so a malformed one is a map that exports with
    /// the built-in shell rather than a map that refuses to export.</summary>
    public static HouseStyle DeserializeOr(string? json, HouseStyle fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return Deserialize(json) ?? fallback; }
        catch (JsonException) { return fallback; }
    }
}
