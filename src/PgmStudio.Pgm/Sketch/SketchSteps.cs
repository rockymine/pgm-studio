using System.Text.Json;
using System.Text.Json.Nodes;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The modifier stack cut short: a layout read as though only its first modifiers were there, which is how the
/// ground after step N of the stack is asked for. The cut is made on the document, so every read of the cut
/// layout (the columns, the contours, the built world) shapes and caches exactly as it does for any other
/// layout.
/// </summary>
public static class SketchSteps
{
    /// <summary>The layout keeping its first <paramref name="steps"/> modifiers that are not switched off, and
    /// every modifier standing before the last of them. Zero keeps none: the ground the shapes alone build. A
    /// document with fewer modifiers than that comes back as it was.</summary>
    public static string Through(string layoutJson, int steps)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(layoutJson); }
        catch (JsonException) { return layoutJson; }
        if (root is not JsonObject document || document["modifiers"] is not JsonArray modifiers) return layoutJson;

        var kept = 0;
        for (var at = 0; at < modifiers.Count; at++)
        {
            if (modifiers[at] is JsonObject modifier && modifier["off"]?.GetValueKind() == JsonValueKind.True) continue;
            if (kept == steps)
            {
                while (modifiers.Count > at) modifiers.RemoveAt(at);
                return document.ToJsonString();
            }
            kept++;
        }
        return layoutJson;
    }
}
