using System.Text.Json;
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
public static class RoomStyleJson
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

    public static string Serialize(RoomStyle style) => JsonSerializer.Serialize(style, Options);

    public static RoomStyle Deserialize(string json) => JsonSerializer.Deserialize<RoomStyle>(json, Options)!;

    /// <summary>A stored style, or <paramref name="fallback"/> when the text is absent or cannot be read. A
    /// snapshot is a hand-editable leaf inside a map's layout, so a malformed one is a map that exports with
    /// the built-in shell rather than a map that refuses to export.</summary>
    public static RoomStyle DeserializeOr(string? json, RoomStyle fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return Deserialize(json) ?? fallback; }
        catch (JsonException) { return fallback; }
    }
}
