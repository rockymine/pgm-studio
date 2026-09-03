using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Api.Services;

/// <summary>
/// The two map-wide entries of a sketch's finish that are neither a registry nor a list: the shells its rooms
/// are stamped in, and the biome its columns carry. Read and written without the caller holding the layout.
/// </summary>
public static class SketchFinishWrite
{
    /// <summary>The two parts of <c>roomStyles</c>, which are the words the stored layout uses. <c>cage</c>
    /// rather than <c>wool</c> because that is what the bridge writes and renaming it would leave every bound
    /// style falling back to the built-in shell on load. Not vocabulary: these two routes are the only
    /// consumers, so the set is their constant.</summary>
    public static readonly string[] RoomParts = ["cage", "spawn"];

    /// <summary>What a body states as a house style, or null where it states none. A body that is literally
    /// <c>null</c> is <b>not</b> nothing — it is the author asking for open ground — so it answers
    /// <see cref="StatedStyle.OpenGround"/> rather than a failure.</summary>
    public static StatedStyle StyleStated(string json)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch (JsonException) { return StatedStyle.Unreadable; }
        if (node is null) return StatedStyle.OpenGround;

        try
        {
            return JsonSerializer.Deserialize<HouseStyle>(json, HouseStyleJson.Options) is { } style
                ? new StatedStyle(style, node)
                : StatedStyle.OpenGround;
        }
        catch (JsonException) { return StatedStyle.Unreadable; }
    }

    /// <summary>What a body states as a biome field, or null where it states none.</summary>
    public static BiomeField? BiomeStated(string json)
    {
        try { return TerrainThemeJson.DeserializeBiome(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Whether the layout binds this room part at all — what tells a delete from a 404. An explicit
    /// null counts as bound, because asking for open ground is a statement and removing it is a change.</summary>
    public static bool Binds(string? layoutJson, string part) =>
        Root(layoutJson)["roomStyles"] is JsonObject styles && styles.ContainsKey(part);

    /// <summary>Whether the layout states a biome field.</summary>
    public static bool HasBiome(string? layoutJson) => Root(layoutJson)["biome"] is not null;

    /// <summary>The layout with one room part bound to <paramref name="style"/>, or unbound where it is null.
    /// A <see cref="JsonValue"/> holding null binds open ground; removing the key entirely restores the
    /// built-in shell, which are different answers and both reachable.</summary>
    public static string WithRoomStyle(string? layoutJson, string part, JsonNode? style, bool unbind)
    {
        var root = Root(layoutJson);
        if (root["roomStyles"] is not JsonObject styles) root["roomStyles"] = styles = [];
        styles.Remove(part);
        if (!unbind) styles[part] = style;
        return root.ToJsonString();
    }

    /// <summary>The layout with its biome field set, or without one where it is null — which is plains
    /// everywhere, and what every board that states none already exports as.</summary>
    public static string WithBiome(string? layoutJson, JsonNode? biome)
    {
        var root = Root(layoutJson);
        root.Remove("biome");
        if (biome is not null) root["biome"] = biome;
        return root.ToJsonString();
    }

    private static JsonObject Root(string? layoutJson) =>
        string.IsNullOrWhiteSpace(layoutJson) ? [] : JsonNode.Parse(layoutJson) as JsonObject ?? [];
}

/// <summary>What a body said about a room's shell — the three answers the binding has. <see cref="Style"/> is
/// the parsed shell where one was stated; <see cref="Node"/> is the JSON to store, so a snapshot keeps
/// whatever the reader has no field for rather than losing it to a round trip.</summary>
public readonly record struct StatedStyle(HouseStyle? Style, JsonNode? Node, bool Readable = true)
{
    public static StatedStyle Unreadable => new(null, null, false);
    public static StatedStyle OpenGround => new(null, null);
}
