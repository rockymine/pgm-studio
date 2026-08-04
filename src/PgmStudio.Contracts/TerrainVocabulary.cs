namespace PgmStudio.Contracts;

/// <summary>
/// The <c>kind</c> discriminator of every terrain-paint material — the value a style is tagged, stored and
/// browsed by ("show every voronoi"). It lives here because it is wire vocabulary: the client's editor, the
/// HTTP surface and the <c>style.kind</c> column all have to agree on the same six strings, and this is the one
/// leaf all three can reach. The painter's own polymorphic attributes carry the same strings by necessity.
/// </summary>
public static class MaterialKind
{
    public const string Solid = "solid";
    public const string Layered = "layered";
    public const string TeamTint = "teamTint";
    public const string Voronoi = "voronoi";
    public const string Noise = "noise";
    public const string WallRun = "wallRun";

    /// <summary>The kinds in offer order — plain blocks first, then the composites, then the patterns.</summary>
    public static readonly (string Id, string Name)[] All =
    [
        (Solid, "Solid block"),
        (Layered, "Layer stack"),
        (TeamTint, "Team tint"),
        (Voronoi, "Voronoi patches"),
        (Noise, "Noise ramp"),
        (WallRun, "Wall stripes"),
    ];

    /// <summary>The label a kind is offered under, or the raw discriminator for one this build does not know.</summary>
    public static string NameOf(string kind) => All.FirstOrDefault(k => k.Id == kind).Name ?? kind;
}

/// <summary>The four themeable buckets a theme binds a style to (bedrock is fixed and never themed). Same
/// reasoning as <see cref="MaterialKind"/>: the <c>theme_bucket.bucket</c> column, the bindings on the wire and
/// the client's editor all name the same four.</summary>
public static class ThemeBuckets
{
    public const string Rim = "rim";
    public const string Surface = "surface";
    public const string Wall = "wall";
    public const string Fill = "fill";

    /// <summary>The buckets top-down: the cap, the interior stack under it, the riser it sits on, then the body
    /// everything else falls to.</summary>
    public static readonly string[] All = [Rim, Surface, Wall, Fill];

    /// <summary>Whether the bucket claims a configurable number of top courses — the rim and the surface do; the
    /// wall's depth is the riser it finds and the fill takes what is left.</summary>
    public static bool HasDepth(string bucket) => bucket is Rim or Surface;
}

/// <summary>One block a terrain-paint material may resolve to, as the block picker receives it
/// (<c>GET /api/terrain/blocks</c>). <see cref="Hex"/> is the colour the export actually places, so a swatch
/// cannot promise a block a different colour.</summary>
public sealed record PaintBlockDto(int Id, int Data, string Name, string Group, string Hex);
