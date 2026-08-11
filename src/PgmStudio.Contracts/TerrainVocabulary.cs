namespace PgmStudio.Contracts;

/// <summary>
/// The <c>kind</c> discriminator of every terrain-paint material — the value a style is tagged, stored and
/// browsed by ("show every voronoi"). It lives here because it is wire vocabulary: the client's editor, the
/// HTTP surface and the <c>style.kind</c> column all have to agree on the same strings, and this is the one
/// leaf all three can reach. The painter's own polymorphic attributes carry the same strings by necessity.
/// </summary>
public static class MaterialKind
{
    public const string Solid = "solid";
    public const string Layered = "layered";
    public const string TeamTint = "teamTint";
    public const string Voronoi = "voronoi";
    public const string Cell = "cell";
    public const string Noise = "noise";
    public const string Turbulence = "turbulence";
    public const string Electric = "electric";
    public const string WallRun = "wallRun";
    public const string WallDiagonal = "wallDiagonal";
    public const string Checker = "checker";
    public const string WallFrame = "wallFrame";

    /// <summary>The kinds in offer order — plain blocks first, then the composites, then the patterns.</summary>
    public static readonly (string Id, string Name)[] All =
    [
        (Solid, "Solid block"),
        (Layered, "Layer stack"),
        (TeamTint, "Team tint"),
        (Voronoi, "Voronoi cells"),
        (Cell, "Cell patches"),
        (Noise, "Fractal field"),
        (Turbulence, "Turbulence"),
        (Electric, "Electric"),
        (WallRun, "Wall stripes"),
        (WallDiagonal, "Diagonal stripes"),
        (Checker, "Checkerboard"),
        (WallFrame, "Wall frame"),
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

/// <summary>Which edges a theme's rim caps — the wire words for the painter's <c>RimEdges</c>, shared by the
/// <c>theme.rim_edges</c> column, the theme JSON a map snapshots, and both authoring surfaces.
/// <see cref="Void"/> caps only where the ground borders the void, so a staircase of stacked plateaus takes
/// one rim around its outside rather than a lip on every tread; <see cref="Drop"/> caps wherever the ground
/// falls away; <see cref="Boundary"/> caps every plateau boundary, a face against a structure included.</summary>
public static class RimEdgeModes
{
    public const string Void = "void";
    public const string Drop = "drop";
    public const string Boundary = "boundary";

    /// <summary>The modes narrowest first — how many edges each one calls a rim.</summary>
    public static readonly string[] All = [Void, Drop, Boundary];

    /// <summary>What each mode caps, in the words an authoring surface offers it in.</summary>
    public static string LabelOf(string mode) => mode switch
    {
        Void => "Only where the ground meets the void",
        Boundary => "Every plateau boundary",
        _ => "Wherever the ground drops",
    };

    /// <summary>Fold an unknown or absent mode down to the default, so a hand-edited theme shows a rim rather
    /// than throwing the editor.</summary>
    public static string Canonical(string? mode) => All.Contains(mode) ? mode! : Drop;
}

/// <summary>The three parts of a room shell a style binds courses to (the pad and the doorway are stamped over
/// them and are never a part). Same reasoning as <see cref="ThemeBuckets"/>: the <c>room_style_course.part</c>
/// column, the courses on the wire and the client's editor all name the same three.</summary>
public static class RoomParts
{
    public const string Floor = "floor";
    public const string Wall = "wall";
    public const string Roof = "roof";

    /// <summary>The three a house has that a plain shell does not: the corner columns that frame it, the
    /// footing it meets the ground on, and the trim along its roof's edge. Each takes one material rather
    /// than a stack — a post is a post all the way up — so only the first course of theirs is read.</summary>
    public const string Post = "post";
    public const string Sill = "sill";
    public const string Verge = "verge";

    /// <summary>The parts bottom-up, the order a shell is stamped in.</summary>
    public static readonly string[] All = [Floor, Wall, Roof, Post, Sill, Verge];
}

/// <summary>Which roof a stored style asks for — the wire words for <c>RoofForm</c>. A <c>flat</c> one is the
/// lid every shell has always had; a <c>gable</c> is two slopes meeting at a ridge, which the stamper could
/// always build and no stored style could ask for until now.</summary>
public static class RoofForms
{
    public const string Gable = "gable";
    public const string Flat = "flat";

    public static readonly string[] All = [Flat, Gable];

    /// <summary>Fold an unknown or absent form down to the flat lid, so a hand-edited row still stamps.</summary>
    public static string Canonical(string? form) => form == Gable ? Gable : Flat;
}

/// <summary>One door a room may be stamped with (<c>GET /api/room-styles/doors</c>). Served rather than
/// restated in the client, because the authoritative list is <c>Domain.DoorMaterials</c> — the same table the
/// wool-room block filter is built from, and a second copy here is exactly how a door could come to be offered
/// that the filter never whitelists.</summary>
public sealed record DoorOptionDto(string Slug, string Label);

/// <summary>One block a terrain-paint material may resolve to, as the block picker receives it
/// (<c>GET /api/terrain/blocks</c>). <see cref="Hex"/> is the colour the export actually places, so a swatch
/// cannot promise a block a different colour.
/// <para><see cref="InFamily"/> says which kind of group <see cref="Group"/> names: a <b>tone family</b>, the
/// set of blocks that read as one ground and the unit a pattern is filled from, or one of the three
/// sixteen-shade colour families, whose members are shades of one block and are chosen from a swatch row
/// instead. The list arrives in group order, so grouping the flagged blocks by <see cref="Group"/> recovers
/// the families whole, in the order they are offered.</para></summary>
public sealed record PaintBlockDto(int Id, int Data, string Name, string Group, string Hex, bool InFamily);
