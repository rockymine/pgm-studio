using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Painting;

/// <summary>The five buckets every paintable terrain block sorts into (docs/world-export/terrain-painting.md
/// §3). <see cref="Fill"/> is the required base — it claims whatever no other bucket took.</summary>
public enum TerrainBucket { Bedrock, Fill, Wall, Surface, Rim }

/// <summary>Which edges the rim caps (TP3) — the three nested edge tests a <see cref="ColumnProfile"/> answers,
/// narrowest first. <see cref="Void"/> caps only the landmass's true outside, so a staircase of stacked
/// plateaus reads as one body with a rim around it rather than a lip on every tread; <see cref="Drop"/> caps
/// wherever the ground falls away, tread edges included; <see cref="Boundary"/> caps every plateau boundary,
/// including a face against a structure or against level ground the paint calls a different plateau.</summary>
[JsonConverter(typeof(RimEdgesConverter))]
public enum RimEdges { Void, Drop, Boundary }

/// <summary>Writes <see cref="RimEdges"/> as the lowercase word the column, the wire DTO and both authoring
/// surfaces already spell it — one spelling for the mode wherever it is stored, rather than a JSON that says
/// <c>Boundary</c> beside a database that says <c>boundary</c>.</summary>
public sealed class RimEdgesConverter() : JsonStringEnumConverter<RimEdges>(JsonNamingPolicy.CamelCase);

/// <summary>Where a block sits for the material resolver: its world coordinate, its bucket, its depth below
/// the top of that bucket's band (0 = the band's top course) — the parameter a layered material (grass over
/// dirt) reads — the <see cref="TeamData"/> of the team that owns the cell (a 0–15 wool/clay damage nibble,
/// -1 = neutral), and <see cref="PerimeterArc"/>, the cell's arc index along the outer void-facing wall (-1
/// off it) that a wall-run pattern reads (TP13).</summary>
/// <param name="PerimeterRun">Which axis the wall runs along where this cell sits
/// (<see cref="PgmStudio.Geom.Algorithms.GridBoundary.RunAlongX"/> or <c>RunAlongZ</c>), or 0 off a wall. The
/// arc says how far round the face a cell is and the turn how sharply the face bends; this says which way the
/// face is <em>going</em>, which is what a block with a direction of its own needs. A log laid across a wall
/// rather than along it points its cut end at whoever is looking.</param>
/// <param name="Inset">How many steps <em>in</em> from the landmass's void-facing edge the column stands — 0 on
/// the edge itself, -1 off the footprint. The plan-direction axis, and the companion to
/// <paramref name="PerimeterArc"/>: the arc says how far <em>round</em> the edge a cell sits and this how far
/// in from it, so a band stack can be read along either. The walk crosses an elevation step, so on a staircase
/// the count runs across the treads and up the hill rather than restarting on each.</param>
public readonly record struct BucketContext(int X, int Y, int Z, TerrainBucket Bucket, int DepthFromTop, int TeamData = -1, int PerimeterArc = -1, int HeightFromBottom = 0, int PerimeterTurn = 0, int PerimeterRun = 0, int Inset = -1)
{
    /// <summary>Whether the cell belongs to a team (a colour is available for a team-tinted material).</summary>
    public bool HasTeam => TeamData >= 0;
}

/// <summary>
/// Which distance a <see cref="BandStack"/> is read along. The stack states its bands and where they run out
/// and deliberately not the axis — "the axis is the caller's" — so the material doing the reading is where the
/// choice belongs, and stating it is what keeps one type from becoming two identical ones.
/// </summary>
[JsonConverter(typeof(BandAxisConverter))]
public enum BandAxis
{
    /// <summary>Down from the top of the bucket: grass over two dirt, a wall's banded riser. The reading every
    /// layered material had before there was a second one.</summary>
    Depth,

    /// <summary>In from the landmass's void-facing edge: a cobble rim, then two rings of stone brick, then a
    /// field. Reads <see cref="BucketContext.Inset"/>, so bands run as concentric rings round a shape rather
    /// than as courses down a column.</summary>
    Inward,
}

/// <summary>Written as <c>"depth"</c>/<c>"inward"</c> rather than as a number, the way every other enum in a
/// theme is — a stored theme is read by people as well as by the deserializer.</summary>
public sealed class BandAxisConverter() : JsonStringEnumConverter<BandAxis>(JsonNamingPolicy.CamelCase);

/// <summary>
/// A bucket's material — the block(s) its cells resolve to (docs/world-export/terrain-painting.md §3). A single
/// block, a vertical layer stack, a team tint, or an area/perimeter pattern (TP13). Polymorphic under one
/// <c>kind</c> discriminator so a whole theme serializes to the theme JSON that TP10 will scope.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SolidMaterial), "solid")]
[JsonDerivedType(typeof(LayeredMaterial), "layered")]
[JsonDerivedType(typeof(TeamTintedMaterial), "teamTint")]
[JsonDerivedType(typeof(VoronoiMaterial), "voronoi")]
[JsonDerivedType(typeof(CellMaterial), "cell")]
[JsonDerivedType(typeof(NoiseMaterial), "noise")]
[JsonDerivedType(typeof(TurbulenceMaterial), "turbulence")]
[JsonDerivedType(typeof(ElectricMaterial), "electric")]
[JsonDerivedType(typeof(WallRunMaterial), "wallRun")]
[JsonDerivedType(typeof(WallDiagonalMaterial), "wallDiagonal")]
[JsonDerivedType(typeof(CheckerMaterial), "checker")]
[JsonDerivedType(typeof(LogCheckerMaterial), "logChecker")]
[JsonDerivedType(typeof(LaidLogMaterial), "laidLog")]
[JsonDerivedType(typeof(WallFrameMaterial), "wallFrame")]
public abstract record TerrainMaterial
{
    public abstract (int Id, int Data) Resolve(in BucketContext ctx);
}

/// <summary>One block everywhere in the bucket.</summary>
public sealed record SolidMaterial(int Id, int Data = 0) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx) => (Id, Data);
}

/// <summary>
/// A <see cref="BandStack"/> read along a distance — grass over two dirt, a wall's banded riser (TP11), or a
/// cobble rim then two rings of stone brick then a field.
///
/// <para><b>The axis is stated rather than implied</b> (<see cref="BandAxis"/>). Down from the top of the
/// bucket is what this always meant and stays the default; in from the landmass's edge is the same stack read
/// along <see cref="BucketContext.Inset"/> instead. One type with the axis named, not two types differing in
/// which property they read — the bands, the thicknesses and the run-out rule are identical and only the
/// distance differs.</para>
///
/// <para><b><see cref="Beyond"/> is what shows where the stack claims nothing</b>, which is the half
/// <see cref="BandEnding.HandOver"/> leaves to whoever holds the axis. Unset it is stone, the fill every
/// unclaimed block already falls to — right for a depth stack, whose bucket is its whole space, and wrong for
/// a ring stack that is meant to stop a few rings in and let the ground it sits on show. Under
/// <see cref="BandEnding.Repeat"/> nothing is ever unclaimed and this is never reached.</para>
/// </summary>
public sealed record LayeredMaterial(
    BandStack Stack,
    BandAxis Axis = BandAxis.Depth,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TerrainMaterial? Beyond = null)
    : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        // A stated layered carrying no stack is a document fault, not a crash: it falls through to the same
        // answer a voronoi with no bands gives, and the reader's unread walk is what names the field that was
        // written instead. Painting is called per column, so a throw here is a 500 on a whole request.
        if (Stack is null) return Beyond?.Resolve(in ctx) ?? (Blocks.Stone, 0);
        var step = Axis == BandAxis.Inward ? ctx.Inset : ctx.DepthFromTop;
        // Off the footprint the inward axis has no answer, which is not the same as being past the last band:
        // there is no ring to be in, so the stack never gets asked.
        if (step < 0) return Beyond?.Resolve(in ctx) ?? (Blocks.Stone, 0);
        return Stack.At(step).Material?.Resolve(in ctx)
            ?? Beyond?.Resolve(in ctx)
            ?? (Blocks.Stone, 0);
    }
}

/// <summary>The hash a material holding a collection needs, once its equality walks that collection. Written
/// once because three materials do it and a per-site copy is how two of them end up disagreeing with their own
/// <c>Equals</c>.</summary>
internal static class MaterialHash
{
    public static int Of<T>(IEnumerable<T> items)
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(item);
        return hash.ToHashCode();
    }
}

/// <summary>The bucket's block tinted by the team that owns the cell — the same 0–15 damage scale wool uses,
/// so a clay / wool / stained-glass block takes the team's colour (docs/world-export/terrain-painting.md §3).
/// A cell with no team (a neutral mid) falls back to <paramref name="Neutral"/>. Usable on <b>any</b> bucket,
/// and composable inside a <see cref="LayeredMaterial"/> or a pattern (the tint reads from the shared
/// <see cref="BucketContext"/>, so it is not wall-specific).</summary>
public sealed record TeamTintedMaterial(int BlockId, TerrainMaterial Neutral) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
        // A tint stated with no neutral is a document fault rather than a crash: it takes the same
        // fall-through a voronoi with no bands takes, and the reader's unread walk names the field that was
        // written instead. Painting runs per column, so a throw here is a 500 over a whole request.
        => ctx.HasTeam ? (BlockId, ctx.TeamData) : Neutral?.Resolve(in ctx) ?? (Blocks.Stone, 0);
}

/// <summary>A top-claiming bucket's spec (TP7/TP11/TP12): the material its courses resolve through, how many
/// top courses it claims (its <b>depth</b>), and whether it is enabled. The rim and the surface share this
/// shape — both finish the top of a column with a configurable depth (the rim on an edge, the surface on an
/// interior), and the wall then fills the exposed riser below. Depth is a per-bucket knob living with its
/// bucket, not a loose theme scalar, so a theme sets each independently (a 3-block rim, a grass-over-two-dirt
/// surface). Distinct from <see cref="TerrainBand"/>, which is a <em>resolved</em> Y-run in the output — this
/// is the <em>theme</em> spec a band resolves from. The band resolver always clamps depth to the stone the
/// bedrock floor leaves, so it never recolours bedrock.</summary>
public sealed record TopBand(TerrainMaterial Material, int Depth = 1, bool Enabled = true);

/// <summary>How thick the bedrock floor is (TP8): a fixed block count, or the remainder under a fixed painted
/// terrain depth (bedrock = column height − terrain depth, per column). Always ≥1, never taller than the
/// column.</summary>
public sealed record BedrockSpec(bool Relative, int Value)
{
    /// <summary>A fixed <paramref name="thickness"/>-block bedrock floor.</summary>
    public static BedrockSpec Absolute(int thickness) => new(false, Math.Max(1, thickness));

    /// <summary>Bedrock takes everything under the top <paramref name="terrainDepth"/> painted blocks.</summary>
    public static BedrockSpec TerrainRelative(int terrainDepth) => new(true, Math.Max(0, terrainDepth));

    /// <summary>The first Y that is <em>not</em> bedrock, given a column whose surface top is
    /// <paramref name="surfaceTop"/> — clamped to [1, surfaceTop]. Equal to surfaceTop means the whole column
    /// is bedrock and nothing paints (TP8's stop).</summary>
    public int PaintFloor(int surfaceTop)
        => Math.Clamp(Relative ? surfaceTop - Value : Value, 1, surfaceTop);
}

/// <summary>
/// A terrain-paint theme (docs/world-export/terrain-painting.md §5): the geometry knobs plus a material — and,
/// for the top-claiming buckets, a depth and toggle (<see cref="TopBand"/>) — per bucket. The default is the
/// shipping finish: a one-block quartz rim, a team-tinted stained-clay wall, a grass-over-two-dirt surface
/// three blocks deep, and stone left as fill, over a one-block bedrock floor, capping every drop. Every field has
/// a base default, so <see cref="Default"/> is complete and any single knob can be overridden alone.
/// </summary>
public sealed record TerrainTheme
{
    // grass over two dirt — the standard interior stack (TP11); its layer thicknesses sum to the surface depth.
    private static readonly TerrainMaterial DefaultSurface = new LayeredMaterial(new BandStack(
        [new Band(new SolidMaterial(Blocks.Grass), 1), new Band(new SolidMaterial(Blocks.Dirt), 2)]));

    // ── geometry ──
    /// <summary>The bedrock floor thickness (TP8). Default one block.</summary>
    public BedrockSpec Bedrock { get; init; } = BedrockSpec.Absolute(1);
    /// <summary>Which edges the rim caps (TP3). Default <see cref="RimEdges.Drop"/> — wherever the ground
    /// falls away.</summary>
    public RimEdges RimEdges { get; init; } = RimEdges.Drop;
    /// <summary>Paint wall on terrain-to-terrain faces, not only void-facing ones (TP9). Default on.</summary>
    public bool WallOnTerrainFaces { get; init; } = true;

    // ── buckets ──
    // The two top-claiming buckets carry their own depth (TP7/TP11) and toggle (TP12) via TopBand; the wall's
    // depth is the derived riser, so it stays a bare material + toggle; fill is required and always on.
    /// <summary>The edge cap (TP7): quartz, one block deep.</summary>
    public TopBand Rim { get; init; } = new(new SolidMaterial(Blocks.QuartzBlock), Depth: 1);
    /// <summary>The interior stack (TP11): grass over two dirt, three blocks deep.</summary>
    public TopBand Surface { get; init; } = new(DefaultSurface, Depth: 3);
    /// <summary>The exposed riser (TP9/TP12): team-tinted clay, light-grey clay on neutral land (the tint is a
    /// material any bucket can take, not wall-specific).</summary>
    public TerrainMaterial Wall { get; init; } = new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay, 8));
    /// <summary>Whether the wall paints at all (TP12); off, its riser blocks fall to fill.</summary>
    public bool WallEnabled { get; init; } = true;
    /// <summary>The required base (TP12): every block no enabled bucket claimed. Stone.</summary>
    public TerrainMaterial Fill { get; init; } = new SolidMaterial(Blocks.Stone);

    /// <summary>The shipping finish — the validated base model with example materials.</summary>
    public static TerrainTheme Default { get; } = new();

    /// <summary>The material a bucket resolves through (bedrock is fixed, never themeable).</summary>
    public TerrainMaterial MaterialFor(TerrainBucket bucket) => bucket switch
    {
        TerrainBucket.Rim => Rim.Material,
        TerrainBucket.Wall => Wall,
        TerrainBucket.Surface => Surface.Material,
        _ => Fill,
    };
}
