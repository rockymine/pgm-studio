using System.Text.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Models;

/// <summary>
/// One region as the editor holds it while the author works on it: what <c>RegionNodeDto</c> answered, plus
/// what only a screen needs — an icon, a formatted coordinate, the first wiring event as a tag — and
/// <b>mutable</b> bounds and coords, which is the point. An inspector edit writes the new number here and
/// draws from it before the save round-trips (<see cref="RegionEdits"/>), so the tree the canvas renders is
/// the author's current one rather than the last one the server confirmed.
/// </summary>
public sealed class RegionNode
{
    public string Id = "";
    public string Type = "unknown";
    public string Label = "";
    public string? Category;         // the region's own derived category (may differ from its display group)
    public string? Subtype;          // refines the category, e.g. spawn → "point" | "protection"
    public string? DraftStep;        // editor step it was drawn in (teams/objective/build) while unwired (E10)
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

    /// <summary>Take the answered region into the editor's own. The two numeric bags stay bags: a region
    /// states its shape in whichever fields its type is made of, and both are rendered and edited generically
    /// — so the values are flattened to scalars here, keeping a half-space's <c>oo</c> as the string the
    /// contract spells it with rather than coercing it to a number.</summary>
    public static RegionNode From(RegionNodeDto dto)
    {
        var node = new RegionNode
        {
            Id = dto.Id,
            Type = dto.Type,
            Label = dto.Label,
            Category = dto.Category,
            Subtype = dto.Subtype,
            DraftStep = dto.DraftStep,
            Synthetic = dto.SyntheticId,
            IsNegative = dto.IsNegative,
            Bounds = Extent(dto.Bounds),
            Coords = dto.Coords is null ? new() : dto.Coords.ToDictionary(c => c.Key, c => Scalar(c.Value)),
            Wiring = [.. dto.Wiring ?? []],
        };
        foreach (var child in dto.Children) node.Children.Add(From(child));
        if (dto.Source is { } source) node.Source = From(source);
        return node;
    }

    private static Dictionary<string, object?>? Extent(RegionExtentDto? bounds) => bounds is null ? null : new()
    {
        ["min_x"] = Scalar(bounds.MinX), ["min_z"] = Scalar(bounds.MinZ),
        ["max_x"] = Scalar(bounds.MaxX), ["max_z"] = Scalar(bounds.MaxZ),
    };

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

    /// <summary>Region type → Lucide icon name (the mapping region-types.js draws with).</summary>
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

/// <summary>A category group of root region nodes, as the editor holds them.</summary>
public sealed class RegionGroup
{
    public string Name = "";
    public string Label = "";
    public List<RegionNode> Regions = new();

    /// <summary>Take the answered tree into the editor's own. A map with no tree answers no groups rather
    /// than failing, which is the state a freshly originated map is in.</summary>
    public static List<RegionGroup> From(RegionTreeDto? tree) =>
        [.. (tree?.Groups ?? []).Select(group => new RegionGroup
        {
            Name = group.Name,
            Label = group.Label,
            Regions = [.. group.Regions.Select(RegionNode.From)],
        })];
}
