using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>
/// One group's relief, read and written without the caller holding the layout — the interior elevation half
/// of what <see cref="SketchThemeWrite"/> does for the paint.
///
/// <para>The relief rides at the layout's top level rather than inside the shapes because a plan recompile
/// replaces every shape it produced and a relief is hand work a plan cannot express, so it is carried across
/// one under its own rule. That is also what makes it addressable: it is keyed already.</para>
/// </summary>
public static class SketchReliefWrite
{
    /// <summary>What a body states as a relief, or null where it states none.</summary>
    public static SketchReliefJson? Stated(string json)
    {
        try { return JsonSerializer.Deserialize<SketchReliefJson>(json, SketchLayout.Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Whether the layout states a relief for this group — what tells a delete from a 404.</summary>
    public static bool Carries(string? layoutJson, string id) =>
        Root(layoutJson)["relief"] is JsonObject relief && relief.ContainsKey(id);

    /// <summary>The layout with <paramref name="id"/> carrying <paramref name="relief"/>, or without that
    /// group at all where the relief is null.</summary>
    public static string With(string? layoutJson, string id, JsonNode? relief)
    {
        var root = Root(layoutJson);
        if (root["relief"] is not JsonObject group) root["relief"] = group = [];
        group.Remove(id);
        if (relief is not null) group[id] = relief;
        return root.ToJsonString();
    }

    private static JsonObject Root(string? layoutJson) =>
        string.IsNullOrWhiteSpace(layoutJson) ? [] : JsonNode.Parse(layoutJson) as JsonObject ?? [];
}
