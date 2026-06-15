namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Shared accessors for the PGM document dict that the declarative generators all need: get-or-create
/// the <c>regions</c>/<c>filters</c> maps and any top-level list. Previously copy-pasted into each slice
/// generator (Teams/Build/Wool); centralised here so the "ensure the container exists, then return it"
/// semantics live in one place.
/// </summary>
internal static class DocAccess
{
    /// <summary>The <c>regions</c> map (id → region dict), created if missing or not a dict.</summary>
    public static Dict Regions(Dict doc) =>
        doc.TryGetValue("regions", out var r) && r is Dict d ? d : (Dict)(doc["regions"] = new Dict());

    /// <summary>The <c>filters</c> map (id → filter dict), created if missing or not a dict.</summary>
    public static Dict Filters(Dict doc) =>
        doc.TryGetValue("filters", out var f) && f is Dict d ? d : (Dict)(doc["filters"] = new Dict());

    /// <summary>A top-level list (e.g. <c>kits</c>, <c>spawners</c>), created if missing or not a list.</summary>
    public static List<object?> EnsureList(Dict doc, string key) =>
        doc.TryGetValue(key, out var v) && v is List<object?> l ? l : (List<object?>)(doc[key] = new List<object?>());
}
