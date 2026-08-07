using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft;

// Pattern materials (docs/world-export/terrain-painting.md TP13): a TerrainMaterial that varies the block across
// a bucket's cells instead of resolving one id. A pattern only changes WHICH block a cell takes, never WHICH
// cells are in the bucket, so it plugs into the existing material seam and the geometry (profile → bands) is
// untouched. Every choice is a deterministic hash of a seed plus the cell (and, for wall-runs, the perimeter arc
// the profile computed) — never RNG, so a map exports the same pattern every time. Palette / stop / run entries
// are themselves TerrainMaterial, so a pattern nests a solid, a team tint, a layer stack, or another pattern.
// The value/fractal noise itself is pure geometry math and lives in PgmStudio.Geom.Algorithms.PatternNoise.

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
public sealed record VoronoiMaterial(uint Seed, int CellSize, IReadOnlyList<VoronoiBand> Bands) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Bands is not { Count: > 0 }) return (Blocks.Stone, 0);
        var (d1, d2, _, _) = Voronoi.NearestTwo(ctx.X, ctx.Z, Seed, CellSize);
        double depth = d2 - d1, edge = 0;
        for (var i = 0; i < Bands.Count - 1; i++)
        {
            edge += Math.Max(1, Bands[i].Depth);
            if (depth < edge) return Bands[i].Material.Resolve(in ctx);
        }
        return Bands[^1].Material.Resolve(in ctx);
    }

    // Bands by value — the generated equality would compare the list by reference (see LayeredMaterial).
    public bool Equals(VoronoiMaterial? other)
        => other is not null && Seed == other.Seed && CellSize == other.CellSize && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode() => HashCode.Combine(Seed, CellSize, MaterialHash.Of(Bands));
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
    IReadOnlyList<TerrainMaterial> Palette) : TerrainMaterial
{
    // Independent hash streams for the two warp axes, so the displacement is a direction rather than a diagonal.
    private const uint WarpX = 0x68BC21EBu;
    private const uint WarpZ = 0x02E5BE93u;

    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Palette is not { Count: > 0 }) return (Blocks.Stone, 0);
        int x = ctx.X, z = ctx.Z;
        if (Warp > 0)
        {
            int period = Math.Max(2, CellSize);
            x += (int)Math.Round((PatternNoise.Value(ctx.X, ctx.Z, Seed ^ WarpX, period) - 0.5) * 2 * Warp);
            z += (int)Math.Round((PatternNoise.Value(ctx.X, ctx.Z, Seed ^ WarpZ, period) - 0.5) * 2 * Warp);
        }
        var (gx, gz) = Voronoi.NearestSite(x, z, Seed, CellSize, Math.Clamp(Jitter, 0, 100) / 100.0);
        return Palette[(int)(PatternNoise.Hash(gx, gz, Seed) % (uint)Palette.Count)].Resolve(in ctx);
    }

    public bool Equals(CellMaterial? other)
        => other is not null && Seed == other.Seed && CellSize == other.CellSize && Jitter == other.Jitter
        && Warp == other.Warp && Palette.SequenceEqual(other.Palette);

    public override int GetHashCode() => HashCode.Combine(Seed, CellSize, Jitter, Warp, MaterialHash.Of(Palette));
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
    IReadOnlyList<TerrainMaterial> Stops) : TerrainMaterial
{
    /// <summary>How each octave is bent before the sum — the whole difference between the three.</summary>
    protected abstract PatternNoise.NoiseShape Shape { get; }

    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Stops is not { Count: > 0 }) return (Blocks.Stone, 0);
        double v = PatternNoise.Field(ctx.X, ctx.Z, Seed, Scale, Octaves, Shape);
        int idx = Math.Clamp((int)(v * Stops.Count), 0, Stops.Count - 1);
        return Stops[idx].Resolve(in ctx);
    }

    /// <summary>Value equality for the shared fields, including the stop list, and only between two of the same
    /// pattern — a turbulence and an electric with identical numbers are still different materials.</summary>
    protected bool SameField(FieldPatternMaterial? other)
        => other is not null && GetType() == other.GetType() && Seed == other.Seed && Scale == other.Scale
        && Octaves == other.Octaves && Stops.SequenceEqual(other.Stops);

    protected int FieldHash() => HashCode.Combine(GetType(), Seed, Scale, Octaves, MaterialHash.Of(Stops));
}

/// <summary>The plain fractal field: cloudy, rounded regions that fade into one another. The base every other
/// field pattern is a bend of.</summary>
public sealed record NoiseMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops)
    : FieldPatternMaterial(Seed, Scale, Octaves, Stops)
{
    protected override PatternNoise.NoiseShape Shape => PatternNoise.NoiseShape.Plain;
    public bool Equals(NoiseMaterial? other) => SameField(other);
    public override int GetHashCode() => FieldHash();
}

/// <summary>The field folded at every zero crossing, so it creases instead of fading: billowed, marbled bands
/// that swirl round one another. The same stops as a noise ramp, laid out like smoke rather than cloud.</summary>
public sealed record TurbulenceMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops)
    : FieldPatternMaterial(Seed, Scale, Octaves, Stops)
{
    protected override PatternNoise.NoiseShape Shape => PatternNoise.NoiseShape.Billow;
    public bool Equals(TurbulenceMaterial? other) => SameField(other);
    public override int GetHashCode() => FieldHash();
}

/// <summary>The fold inverted and sharpened, so the crossings become thin branching filaments and everything
/// else falls away from them — veins through a body rather than bands across one.</summary>
public sealed record ElectricMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops)
    : FieldPatternMaterial(Seed, Scale, Octaves, Stops)
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
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Runs is not { Count: > 0 }) return (Blocks.Stone, 0);
        int total = 0;
        foreach (var run in Runs) total += Math.Max(1, run.Width);
        int s = ctx.PerimeterArc < 0 ? 0 : ctx.PerimeterArc;
        int pos = ((s % total) + total) % total;
        foreach (var run in Runs)
        {
            int width = Math.Max(1, run.Width);
            if (pos < width) return run.Material.Resolve(in ctx);
            pos -= width;
        }
        return Runs[^1].Material.Resolve(in ctx);
    }

    public bool Equals(WallRunMaterial? other) => other is not null && Runs.SequenceEqual(other.Runs);

    public override int GetHashCode() => MaterialHash.Of(Runs);
}
