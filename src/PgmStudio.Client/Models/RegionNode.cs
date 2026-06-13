using System.Text.Json;

namespace PgmStudio.Client.Models;

/// <summary>
/// A node in the region tree (the shape of <c>GET /regions/tree</c>'s recursive nodes). Mirrors the
/// reference's region tree/inspector node: id, type, label, derived bounds, type-specific coords,
/// nested children, and the transform source (mirror/translate) as a single child-like node.
/// </summary>
public sealed class RegionNode
{
    public string Id = "";
    public string Type = "unknown";
    public string Label = "";
    public string? Subtype;          // refines the group category, e.g. spawn → "point" | "protection"
    public List<string> Wiring = new();  // spatial filter wiring, e.g. "enter=only-blue", "block_break=…"
    public bool Synthetic;
    public bool IsNegative;

    /// <summary>The first filter event applied to this region ("enter", "block-break", …), or null when
    /// unwired (monuments/spawners). Surfaced as a tag; the full wiring is in <see cref="Wiring"/>.</summary>
    public string? FirstEvent => Wiring.Count == 0 ? null : Wiring[0].Split('=')[0].Replace('_', '-');
    public Dictionary<string, object?>? Bounds;     // min_x, min_z, max_x, max_z
    public Dictionary<string, object?> Coords = new();
    public List<RegionNode> Children = new();
    public RegionNode? Source;

    public bool HasKids => Children.Count > 0 || Source is not null;

    public static RegionNode Parse(JsonElement e)
    {
        var n = new RegionNode
        {
            Id = Str(e, "id"),
            Type = Str(e, "type"),
            Label = Str(e, "label"),
            Subtype = e.TryGetProperty("subtype", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null,
            Synthetic = Bool(e, "synthetic_id"),
            IsNegative = Bool(e, "is_negative"),
            Bounds = Obj(e, "bounds"),
            Coords = Obj(e, "coords") ?? new(),
        };
        if (e.TryGetProperty("wiring", out var wr) && wr.ValueKind == JsonValueKind.Array)
            foreach (var w in wr.EnumerateArray()) if (w.ValueKind == JsonValueKind.String) n.Wiring.Add(w.GetString() ?? "");
        if (e.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
            foreach (var k in kids.EnumerateArray()) n.Children.Add(Parse(k));
        if (e.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.Object)
            n.Source = Parse(src);
        return n;
    }

    private static string Str(JsonElement e, string k)
        => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static bool Bool(JsonElement e, string k)
        => e.TryGetProperty(k, out var v) && (v.ValueKind == JsonValueKind.True);
    private static Dictionary<string, object?>? Obj(JsonElement e, string k)
    {
        if (!e.TryGetProperty(k, out var v) || v.ValueKind != JsonValueKind.Object) return null;
        var d = new Dictionary<string, object?>();
        foreach (var p in v.EnumerateObject()) d[p.Name] = Scalar(p.Value);
        return d;
    }
    private static object? Scalar(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String => v.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    /// <summary>Display a coord value: null → "—", non-integer number → 2 dp, else as-is (e.g. "oo").</summary>
    public static string Fmt(object? v) => v switch
    {
        null => "—",
        double d => d == Math.Floor(d) && !double.IsInfinity(d) ? ((long)d).ToString() : d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "—",
    };

    /// <summary>Region type → Lucide icon name (port of region-types.js TYPE_ICON).</summary>
    public static string Icon(string type) => type switch
    {
        "point" => "dot",
        "block" => "square",
        "rectangle" => "rectangle-horizontal",
        "cuboid" => "box",
        "cylinder" => "cylinder",
        "circle" => "circle",
        "sphere" => "globe",
        "complement" => "squares-subtract",
        "union" => "squares-unite",
        "negative" => "square-square",
        "intersect" => "squares-intersect",
        "reference" => "square-arrow-out-up-right",
        "mirror" => "square-split-horizontal",
        "half" => "arrows-up-from-line",
        "translate" => "move-3d",
        _ => "shapes",
    };
}

/// <summary>A category group of root region nodes (from <c>GET /regions/tree</c>'s groups).</summary>
public sealed class RegionGroup
{
    public string Name = "";
    public string Label = "";
    public List<RegionNode> Regions = new();

    public static List<RegionGroup> ParseGroups(JsonElement groupsArray)
    {
        var groups = new List<RegionGroup>();
        if (groupsArray.ValueKind != JsonValueKind.Array) return groups;
        foreach (var g in groupsArray.EnumerateArray())
        {
            var grp = new RegionGroup
            {
                Name = g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Label = g.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "",
            };
            if (g.TryGetProperty("regions", out var rs) && rs.ValueKind == JsonValueKind.Array)
                foreach (var r in rs.EnumerateArray()) grp.Regions.Add(RegionNode.Parse(r));
            groups.Add(grp);
        }
        return groups;
    }
}
