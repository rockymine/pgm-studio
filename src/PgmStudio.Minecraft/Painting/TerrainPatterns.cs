using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Painting;

// Pattern materials (docs/world-export/terrain-painting.md TP13): a TerrainMaterial that varies the block across
// a bucket's cells instead of resolving one id. A pattern only changes WHICH block a cell takes, never WHICH
// cells are in the bucket, so it plugs into the existing material seam and the geometry (profile → bands) is
// untouched. Every choice is a deterministic hash of a seed plus the cell (and, for wall-runs, the perimeter arc
// the profile computed) — never RNG, so a map exports the same pattern every time. Palette / stop / run entries
// are themselves TerrainMaterial, so a pattern nests a solid, a team tint, a layer stack, or another pattern.
// The value/fractal noise itself is pure geometry math and lives in PgmStudio.Geom.Algorithms.PatternNoise.
//
// Every area pattern carries a Rise: the vertical period of its field, in blocks, or 0 for none. A pattern of
// the plane gives every block in a column the same answer, so it decides the surface and leaves a wall face as
// vertical stripes — which is what a wall-run already draws deliberately and what a pattern draws by accident.
// A positive Rise samples the field over the volume instead, so a wall carries the same fabric its surface
// does. It is off by default because it is the more expensive field (a voronoi searches twenty-seven sites
// rather than nine) and because on a surface, which is one to three courses deep, there is nothing for it to
// vary: it earns its cost on the buckets that are tall, and those are the wall and the fill.

/// <summary>One stripe of a <see cref="WallRunMaterial"/>: a material and how many arc cells wide it runs.</summary>
public readonly record struct WallStripe(TerrainMaterial Material, int Width);

/// <summary>One band of a <see cref="VoronoiMaterial"/>: a material and how many blocks inward from the cell
/// boundary it runs. The last band's depth is ignored — it takes whatever is left of the cell.</summary>
public readonly record struct VoronoiBand(TerrainMaterial Material, int Depth);

/// <summary>
/// A voronoi area pattern (TP13): the footprint is tiled by a jittered grid of period <paramref name="CellSize"/>,
/// one deterministic seed point per grid cell, and every block belongs to the region whose seed point is nearest —
/// straight-edged convex cells roughly <paramref name="CellSize"/> across. Pure per cell (nearest two of the 3×3
/// grid-cell neighbourhood), no global precompute.
///
/// <para><paramref name="Bands"/> is a <b>ramp measured inward from the cell boundary</b>, not a set of fills to
/// pick between. Band 0 sits on the boundary itself, so it draws the grid — one connected network of lines, which
/// is what makes the pattern read as cells at all. Each later band is a concentric ring further in, and the last
/// takes the middle. A cell too small to reach a band simply never shows it, so small cells come out filled by
/// whichever band they did reach — the ramp gives size a meaning instead of making every cell look alike.</para>
///
/// <para>Depth is the Worley <c>F2 − F1</c> gap: zero where two sites are equidistant, growing towards the
/// middle. Its contours are hyperbolic rather than straight, so the inner bands round off the cell's corners
/// while the outline stays sharp — a stone whose edges have been worn.</para>
/// </summary>
public sealed record VoronoiMaterial(uint Seed, int CellSize, IReadOnlyList<VoronoiBand> Bands, int Rise = 0)
    : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Bands is not { Count: > 0 }) return (Blocks.Stone, 0);
        double depth = Gap(in ctx), edge = 0;
        for (var i = 0; i < Bands.Count - 1; i++)
        {
            edge += Math.Max(1, Bands[i].Depth);
            if (depth < edge) return Bands[i].Material.Resolve(in ctx);
        }
        return Bands[^1].Material.Resolve(in ctx);
    }

    // How far the cell reaches past this block: the Worley F2 − F1 gap, over the plane or over the volume.
    private double Gap(in BucketContext ctx)
    {
        if (Rise <= 0)
        {
            var (flatNear, flatFar, _, _) = Voronoi.NearestTwo(ctx.X, ctx.Z, Seed, CellSize);
            return flatFar - flatNear;
        }
        var (near, far, _, _, _) = Voronoi.NearestTwo(ctx.X, ctx.Y, ctx.Z, Seed, CellSize, Rise);
        return far - near;
    }

    // Bands by value — the generated equality would compare the list by reference (see LayeredMaterial).
    public bool Equals(VoronoiMaterial? other)
        => other is not null && Seed == other.Seed && CellSize == other.CellSize && Rise == other.Rise
        && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode() => HashCode.Combine(Seed, CellSize, Rise, MaterialHash.Of(Bands));
}

/// <summary>
/// A cell area pattern (TP13): the same jittered-grid regions a <see cref="VoronoiMaterial"/> is built from, but
/// each whole region takes one material from <paramref name="Palette"/> and the sampled position is warped before
/// the region is looked up. Where voronoi draws a diagram — a grid of lines with the cells reading off it — this
/// draws a <b>fabric</b>: flat patches of colour, any two of which may meet.
///
/// <para><paramref name="Jitter"/> (0–100%) is how far a site may sit from the middle of its grid cell: at 0 the
/// regions are the grid squares, at 100 they are shards. <paramref name="Warp"/> is how many blocks the boundary
/// wanders, and it is what separates a cell from a voronoi — a straight-edged diagram becomes organic patches
/// once the lookup position is displaced by a noise field of its own.</para>
/// </summary>
public sealed record CellMaterial(uint Seed, int CellSize, int Jitter, int Warp,
    IReadOnlyList<TerrainMaterial> Palette, int Rise = 0) : TerrainMaterial
{
    // Independent hash streams for the three warp axes, so the displacement is a direction rather than a diagonal.
    private const uint WarpX = 0x68BC21EBu;
    private const uint WarpZ = 0x02E5BE93u;
    private const uint WarpY = 0x1B56C4E9u;

    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Palette is not { Count: > 0 }) return (Blocks.Stone, 0);
        var region = Rise > 0 ? VolumeRegion(in ctx) : PlaneRegion(in ctx);
        return Palette[(int)(region % (uint)Palette.Count)].Resolve(in ctx);
    }

    private uint PlaneRegion(in BucketContext ctx)
    {
        int x = ctx.X, z = ctx.Z;
        if (Warp > 0)
        {
            int period = Math.Max(2, CellSize);
            x += Displace(PatternNoise.Value(ctx.X, ctx.Z, Seed ^ WarpX, period));
            z += Displace(PatternNoise.Value(ctx.X, ctx.Z, Seed ^ WarpZ, period));
        }
        var (gx, gz) = Voronoi.NearestSite(x, z, Seed, CellSize, Spread);
        return PatternNoise.Hash(gx, gz, Seed);
    }

    private uint VolumeRegion(in BucketContext ctx)
    {
        int x = ctx.X, y = ctx.Y, z = ctx.Z;
        if (Warp > 0)
        {
            int period = Math.Max(2, CellSize), lift = Math.Max(2, Rise);
            x += Displace(PatternNoise.Value(ctx.X, ctx.Y, ctx.Z, Seed ^ WarpX, period, lift));
            y += Displace(PatternNoise.Value(ctx.X, ctx.Y, ctx.Z, Seed ^ WarpY, period, lift));
            z += Displace(PatternNoise.Value(ctx.X, ctx.Y, ctx.Z, Seed ^ WarpZ, period, lift));
        }
        var (gx, gy, gz) = Voronoi.NearestSite(x, y, z, Seed, CellSize, Rise, Spread);
        return PatternNoise.Hash(gx, gy, gz, Seed);
    }

    private double Spread => Math.Clamp(Jitter, 0, 100) / 100.0;

    private int Displace(double unit) => (int)Math.Round((unit - 0.5) * 2 * Warp);

    public bool Equals(CellMaterial? other)
        => other is not null && Seed == other.Seed && CellSize == other.CellSize && Jitter == other.Jitter
        && Warp == other.Warp && Rise == other.Rise && Palette.SequenceEqual(other.Palette);

    public override int GetHashCode()
        => HashCode.Combine(Seed, CellSize, Jitter, Warp, Rise, MaterialHash.Of(Palette));
}

/// <summary>
/// The shared shape of the three <b>field</b> patterns (TP13): a fractal field over the footprint cut into bands
/// by <paramref name="Stops"/> — the value in [0,1) selects a band, so <c>n</c> stops give <c>n</c> materials in
/// order. Only neighbouring bands can share a boundary, which is what makes a stop list read as a ramp from one
/// material to the next rather than as a set of patches; and the band areas fall off towards the ends, so the
/// first and last stop are accents and the middle ones are the body.
///
/// <para><paramref name="Octaves"/> adds finer detail without narrowing the field — see
/// <see cref="PatternNoise.Field"/> — so raising it no longer starves the outer stops. The three patterns differ
/// in one thing only: which <see cref="PatternNoise.NoiseShape"/> each octave takes.</para>
/// </summary>
public abstract record FieldPatternMaterial(uint Seed, int Scale, int Octaves,
    IReadOnlyList<TerrainMaterial> Stops, int Rise = 0) : TerrainMaterial
{
    /// <summary>How each octave is bent before the sum — the whole difference between the three.</summary>
    protected abstract PatternNoise.NoiseShape Shape { get; }

    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Stops is not { Count: > 0 }) return (Blocks.Stone, 0);
        double v = PatternNoise.Field(ctx.X, ctx.Y, ctx.Z, Seed, Scale, Rise, Octaves, Shape);
        int idx = Math.Clamp((int)(v * Stops.Count), 0, Stops.Count - 1);
        return Stops[idx].Resolve(in ctx);
    }

    /// <summary>Value equality for the shared fields, including the stop list, and only between two of the same
    /// pattern — a turbulence and an electric with identical numbers are still different materials.</summary>
    protected bool SameField(FieldPatternMaterial? other)
        => other is not null && GetType() == other.GetType() && Seed == other.Seed && Scale == other.Scale
        && Octaves == other.Octaves && Rise == other.Rise && Stops.SequenceEqual(other.Stops);

    protected int FieldHash() => HashCode.Combine(GetType(), Seed, Scale, Octaves, Rise, MaterialHash.Of(Stops));
}

/// <summary>The plain fractal field: cloudy, rounded regions that fade into one another. The base every other
/// field pattern is a bend of.</summary>
public sealed record NoiseMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops, int Rise = 0)
    : FieldPatternMaterial(Seed, Scale, Octaves, Stops, Rise)
{
    protected override PatternNoise.NoiseShape Shape => PatternNoise.NoiseShape.Plain;
    public bool Equals(NoiseMaterial? other) => SameField(other);
    public override int GetHashCode() => FieldHash();
}

/// <summary>The field folded at every zero crossing, so it creases instead of fading: billowed, marbled bands
/// that swirl round one another. The same stops as a noise ramp, laid out like smoke rather than cloud.</summary>
public sealed record TurbulenceMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops, int Rise = 0)
    : FieldPatternMaterial(Seed, Scale, Octaves, Stops, Rise)
{
    protected override PatternNoise.NoiseShape Shape => PatternNoise.NoiseShape.Billow;
    public bool Equals(TurbulenceMaterial? other) => SameField(other);
    public override int GetHashCode() => FieldHash();
}

/// <summary>The fold inverted and sharpened, so the crossings become thin branching filaments and everything
/// else falls away from them — veins through a body rather than bands across one.</summary>
public sealed record ElectricMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops, int Rise = 0)
    : FieldPatternMaterial(Seed, Scale, Octaves, Stops, Rise)
{
    protected override PatternNoise.NoiseShape Shape => PatternNoise.NoiseShape.Ridge;
    public bool Equals(ElectricMaterial? other) => SameField(other);
    public override int GetHashCode() => FieldHash();
}

/// <summary>
/// A wall-run pattern (TP13): stripes that travel <em>along</em> the wall face and wrap the whole void-facing
/// perimeter, reading the arc index the profile assigned each outer-wall column (<see cref="BucketContext.PerimeterArc"/>).
/// The runs repeat in order around the loop, each <see cref="WallStripe.Width"/> arc cells wide, so any number
/// of materials with any widths cycle continuously around every corner. A cell off the outer perimeter
/// (<c>PerimeterArc &lt; 0</c> — an internal riser) reads as arc 0, taking the first run.
/// </summary>
public sealed record WallRunMaterial(IReadOnlyList<WallStripe> Runs) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx) =>
        WallStripes.Resolve(Runs, WallStripes.Arc(in ctx), in ctx);

    public bool Equals(WallRunMaterial? other) => other is not null && Runs.SequenceEqual(other.Runs);

    public override int GetHashCode() => MaterialHash.Of(Runs);
}

/// <summary>Walking a stripe cycle at a position along the wall. Shared so a run and a diagonal differ only in
/// the position they ask about, which is the whole of the difference between them.</summary>
internal static class WallStripes
{
    /// <summary>Where round the wall a cell sits. A cell off the outer perimeter — an internal riser — has no
    /// arc, and reads as the start of the loop rather than wrapping to the end of it.</summary>
    public static int Arc(in BucketContext ctx) => ctx.PerimeterArc < 0 ? 0 : ctx.PerimeterArc;

    public static (int Id, int Data) Resolve(IReadOnlyList<WallStripe> runs, int at, in BucketContext ctx)
    {
        if (runs is not { Count: > 0 }) return (Blocks.Stone, 0);
        var total = 0;
        foreach (var run in runs) total += Math.Max(1, run.Width);
        var position = ((at % total) + total) % total;
        foreach (var run in runs)
        {
            var width = Math.Max(1, run.Width);
            if (position < width) return run.Material.Resolve(in ctx);
            position -= width;
        }
        return runs[^1].Material.Resolve(in ctx);
    }
}

/// <summary>
/// A diagonal wall pattern: the stripes of a <see cref="WallRunMaterial"/> sheared, by starting each course
/// <paramref name="Slope"/> cells further round the perimeter than the one beneath it.
///
/// <para>A wall-run is constant up a column, so its stripes stand vertical. Offsetting the read by the height
/// tilts them, and the angle is the ratio of the two: at a slope of one the stripe moves one cell along for
/// every course up, which on a square-blocked face is 45°. Larger slopes lay it flatter, a negative slope
/// leans it the other way, and zero is the vertical run again.</para>
///
/// <para>The height is taken from the cell's own Y rather than from the foot of the wall, so the shear is
/// continuous across a face that steps: two walls of different heights standing side by side meet with their
/// diagonals in line, because both are reading the same world courses.</para>
/// </summary>
public sealed record WallDiagonalMaterial(IReadOnlyList<WallStripe> Runs, int Slope = 1) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx) =>
        WallStripes.Resolve(Runs, WallStripes.Arc(in ctx) + ctx.Y * Slope, in ctx);

    public bool Equals(WallDiagonalMaterial? other) =>
        other is not null && Slope == other.Slope && Runs.SequenceEqual(other.Runs);

    public override int GetHashCode() => HashCode.Combine(MaterialHash.Of(Runs), Slope);
}

/// <summary>
/// A frame drawn round the wall itself: <paramref name="Edge"/> along the top and bottom courses and down the
/// shape's corners, <paramref name="Fill"/> in the panel those enclose. On a rectangle it inks the outline the
/// way a comic panel is inked.
///
/// <para><b>What counts as a corner is an angle, not a change of direction.</b> A wall exists only as squares,
/// so a boundary that is not axis-aligned is drawn as steps and its direction changes constantly — asking where
/// the direction changed would find a corner at nearly every cell of a circle and along every shallow edge. The
/// profile instead measures how far the boundary turns over a span of cells either side, which cancels the
/// staircase and leaves real curvature (<see cref="PgmStudio.Geom.Algorithms.GridBoundary.TurnAt"/>). A vertex
/// reads its exterior angle: <paramref name="Angle"/> is simply how sharp a turn has to be to be inked.</para>
///
/// <para>That one number decides the shape's whole character. A rectangle's corners turn 90° and are always
/// inked; a shallow bend in a polygon is not; a circle of any reasonable radius never reaches a usable
/// threshold, so it has <b>no corners at all</b> and the frame falls back to its top and bottom courses — a
/// layered material, which is the right answer for a shape with nothing to pick out. A very tight arc does ink,
/// which is also right, since a fillet a few blocks across is a corner.</para>
///
/// <para>The same number sets how far the ink wraps round each corner, because the measured turn ramps to a
/// vertex rather than switching on at it — a low threshold inks a broad return, a high one only the vertex.
/// <paramref name="Thickness"/> is the courses taken at the top and bottom, and a wall too short to hold two
/// of them is all edge, which is what a one-course sill should be.</para>
/// </summary>
public sealed record WallFrameMaterial(TerrainMaterial Edge, TerrainMaterial Fill, int Angle = 45,
                                       int Thickness = 1) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        var courses = Math.Max(1, Thickness);
        var framed = ctx.DepthFromTop < courses
            || ctx.HeightFromBottom < courses
            || ctx.PerimeterTurn >= Math.Max(1, Angle);
        return (framed ? Edge : Fill).Resolve(in ctx);
    }
}

/// <summary>
/// A checkerboard: two materials alternating over squares <paramref name="Size"/> blocks on a side.
///
/// <para>The board is laid <b>in the face it paints</b>, which is what keeps it a checkerboard on both a wall
/// and the ground. On the outer wall the two axes are the perimeter arc and height, so the squares tile the
/// face an author is looking at; anywhere else they are the two ground axes, so the squares tile the plane
/// underfoot. Taking world x and z everywhere would be simpler and wrong: a plane pattern gives every block in
/// a column the same answer, and a wall painted with one comes out as vertical stripes rather than squares.</para>
///
/// <para>Parity is the sum of the two square indices, floored so it does not fold at the origin — the
/// arithmetic negative of a truncating divide would put two squares of a colour together across x = 0.</para>
/// </summary>
public sealed record CheckerMaterial(int Size, TerrainMaterial Even, TerrainMaterial Odd) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
        => (Parity(in ctx, Size) == 0 ? Even : Odd).Resolve(in ctx);

    /// <summary>Which of the two squares a cell falls on, in the face the board is laid in. Shared with
    /// <see cref="LogCheckerMaterial"/>, which lays the same board and varies a log's axis over it rather than
    /// two materials — one board, described once.</summary>
    internal static int Parity(in BucketContext ctx, int size)
    {
        var side = Math.Max(1, size);
        var (along, up) = ctx.PerimeterArc >= 0 ? (ctx.PerimeterArc, ctx.Y) : (ctx.X, ctx.Z);
        return (Floor(along, side) + Floor(up, side)) & 1;
    }

    private static int Floor(int value, int side) =>
        value >= 0 ? value / side : -(((-value) + side - 1) / side);
}

/// <summary>
/// A log <b>laid along the wall</b>, everywhere — the course that reads as a beam running through the masonry.
///
/// <para>It is the log checkerboard with one of its two states taken away, and it exists for the same reason
/// that one does: a log's data nibble is its axis, so a solid cannot lie a log down and a material resolving
/// the nibble from the cell would point half of them out of the wall. The wall's own run answers it
/// (<see cref="BucketContext.PerimeterRun"/>), and the log takes the axis the wall is going, so the sawn ends
/// are buried in the neighbouring wall blocks and only bark shows.</para>
///
/// <para>At a <b>corner</b> the wall has faces on both axes and a laid log would show a sawn end on one of
/// them, so the log stands up there — which is what a corner post is, and what the checkerboard does for the
/// same reason. A beam that wants to carry on <em>past</em> the corner and show its end is a different thing
/// and belongs to the building rather than to the wall (<see cref="HouseStyle.Beams"/>).</para>
/// </summary>
public sealed record LaidLogMaterial(int Id, int Data = 0) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        var wood = Data & 3;
        if (ctx.PerimeterRun == GridBoundary.RunsBothWays) return (Id, wood);          // upright at a corner
        return (Id, wood | (ctx.PerimeterRun == GridBoundary.RunAlongZ ? 8 : 4));
    }
}

/// <summary>
/// A checkerboard of <b>one</b> log, alternating the way it is turned rather than what it is made of: standing
/// upright on one square, lying on its side on the next. The grain runs vertically on the first and across on
/// the second, so a wall reads as a woven board out of a single block — the timbering nearly every hand-built
/// house on the corpus uses (`alpine_mining_ii` does it in acacia).
///
/// <para>It is its own material rather than a <see cref="CheckerMaterial"/> over two solids because the two
/// squares are not two blocks. They are one block and two orientations, and an orientation is not something a
/// solid can carry: a log's data nibble <em>is</em> its axis, so a material that resolved the nibble from the
/// cell's coordinates would turn every log the same way and paint a flat patch of wall.</para>
///
/// <para><b>A log on its side lies along the wall, never across it.</b> The axis a log is laid on decides which
/// two of its six faces are the sawn ends, and a log laid across a wall puts one of them straight out at the
/// viewer — the one thing the pattern must not do. The wall's own run answers that
/// (<see cref="BucketContext.PerimeterRun"/>): the log takes the axis the wall is going. Off a wall there is no
/// face to protect and the flat squares read as bark against sawn end, which is what a log floor is.</para>
/// </summary>
public sealed record LogCheckerMaterial(int Size, int Id, int Data = 0) : TerrainMaterial
{
    /// <summary>The two bits of a log's data that carry its axis, above the two that carry its species.</summary>
    private const int Upright = 0, AlongX = 4, AlongZ = 8;

    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        // The data a log carries is its species in the low two bits and its axis in the two above, so the
        // author names the wood and the pattern supplies the turn — the rule a window's block already follows.
        var wood = Data & 3;
        // A corner is on two faces at right angles and no log lying down can show bark to both, so it stands —
        // which is what a corner of a timbered building is anyway.
        if (ctx.PerimeterRun == GridBoundary.RunsBothWays) return (Id, wood | Upright);
        if (CheckerMaterial.Parity(in ctx, Size) == 0) return (Id, wood | Upright);

        // Along the wall where there is one. Off a wall either axis shows bark upward, and x is taken so a
        // floor's grain is at least consistent rather than deciding itself per cell.
        var axis = ctx.PerimeterRun == GridBoundary.RunAlongZ ? AlongZ : AlongX;
        return (Id, wood | axis);
    }
}
