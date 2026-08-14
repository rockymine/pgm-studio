using System.Text.Json.Nodes;

namespace PgmStudio.Pgm;

/// <summary>
/// Merges a JSON object handed through verbatim onto a document already produced by another path — the
/// mechanism behind an addressing layer over a real document type (<c>SketchLayout</c>, <c>MapIntent</c>,
/// …): the overlay states nothing but the target document's own fields, so nothing here invents a
/// vocabulary of its own the way a reduced spec format does (`docs/tools/mapgen-review.md` MG29).
///
/// <para>An object key present on both sides merges recursively, so an overlay that names one new theme in
/// a registry of three does not have to restate the other two. An array key present on both sides is
/// <b>concatenated</b>, target's elements first — a spec adds a shape, a destroyable or a prop rather than
/// replacing the whole list it is reaching into. Anything else — a scalar, a key the target does not have,
/// a type mismatch (an object where the target held an array) — the overlay's value replaces the target's
/// outright, including an explicit JSON <c>null</c>, which states "no value" rather than "no change".</para>
/// </summary>
public static class DocumentOverlay
{
    /// <summary>The base document, with <paramref name="overlayJson"/> merged onto it. Both must parse as
    /// JSON objects — a document is always an object at its root, and an overlay that is not one has
    /// nothing to address into it.</summary>
    public static string Merge(string targetJson, string overlayJson)
    {
        var target = JsonNode.Parse(targetJson) as JsonObject
            ?? throw new ArgumentException("the base document is not a JSON object");
        var overlay = JsonNode.Parse(overlayJson) as JsonObject
            ?? throw new ArgumentException("the overlay is not a JSON object");
        MergeInto(target, overlay);
        return target.ToJsonString();
    }

    private static void MergeInto(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, overlayValue) in overlay)
        {
            if (overlayValue is null) { target[key] = null; continue; }

            if (target[key] is JsonObject targetChild && overlayValue is JsonObject overlayChild)
                MergeInto(targetChild, overlayChild);
            else if (target[key] is JsonArray targetItems && overlayValue is JsonArray overlayItems)
                foreach (var item in overlayItems) targetItems.Add(item?.DeepClone());
            else
                target[key] = overlayValue.DeepClone();
        }
    }
}
