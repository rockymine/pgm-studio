using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>
/// One entry of a map's terrain-theme registry, read and written without the caller holding the layout.
///
/// <para>The registry is a <b>snapshot store</b>: a theme copied in from the library is a frozen copy, so
/// editing a library row never repaints a shipped map. That is what makes an entry addressable on its own —
/// it belongs to this map and to nothing else.</para>
///
/// <para>The entry is spliced in as the <b>JSON that was posted</b>, once it has been read as a theme to
/// prove it is one. Round-tripping it through <see cref="TerrainTheme"/> would drop whatever the reader has
/// no field for, which is the loss <c>RQ3</c> exists to report on the way in rather than to cause.</para>
/// </summary>
public static class SketchThemeWrite
{
    /// <summary>What a body states as a theme, or null where it states none.</summary>
    public static TerrainTheme? Stated(string json)
    {
        try { return TerrainThemeJson.Deserialize(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Whether the registry carries this id at all — what tells a delete from a 404, and asked of
    /// the raw registry rather than of the parsed themes so an entry too malformed to read is still one that
    /// can be removed.</summary>
    public static bool Carries(string? layoutJson, string id) =>
        Registry(layoutJson) is { } themes && themes.ContainsKey(id);

    /// <summary>Which theme the map defaults to, or null where it states none.</summary>
    public static string? MapThemeOf(string? layoutJson) =>
        Root(layoutJson)["mapTheme"]?.GetValue<string>();

    /// <summary>The layout with <paramref name="id"/> carrying <paramref name="theme"/>, or without that id
    /// at all where the theme is null.</summary>
    public static string With(string? layoutJson, string id, JsonNode? theme)
    {
        var root = Root(layoutJson);
        if (root["themes"] is not JsonObject themes) root["themes"] = themes = [];
        themes.Remove(id);
        if (theme is not null) themes[id] = theme;
        return root.ToJsonString();
    }

    /// <summary>The layout with its map default set, or cleared where the theme is null or empty.</summary>
    public static string WithMapTheme(string? layoutJson, string? theme)
    {
        var root = Root(layoutJson);
        if (string.IsNullOrEmpty(theme)) root.Remove("mapTheme");
        else root["mapTheme"] = theme;
        return root.ToJsonString();
    }

    private static JsonObject Root(string? layoutJson) =>
        string.IsNullOrWhiteSpace(layoutJson)
            ? []
            : JsonNode.Parse(layoutJson) as JsonObject ?? [];

    private static JsonObject? Registry(string? layoutJson) => Root(layoutJson)["themes"] as JsonObject;
}
