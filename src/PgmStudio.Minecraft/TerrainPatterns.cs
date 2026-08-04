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

/// <summary>
/// A voronoi area pattern (TP13): the footprint is tiled by a jittered grid of period <paramref name="CellSize"/>,
/// one deterministic seed point per grid cell, and each block takes the material of the region whose seed point
/// is nearest — irregular patches roughly <paramref name="CellSize"/> across. Pure per cell (nearest of the 3×3
/// grid-cell neighbourhood), no global precompute. The region hashes into <paramref name="Palette"/>, which may
/// hold any number of materials.
/// </summary>
public sealed record VoronoiMaterial(uint Seed, int CellSize, IReadOnlyList<TerrainMaterial> Palette) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Palette is not { Count: > 0 }) return (Blocks.Stone, 0);
        var (gx, gz) = Voronoi.NearestSite(ctx.X, ctx.Z, Seed, CellSize);
        int idx = (int)(PatternNoise.Hash(gx, gz, Seed) % (uint)Palette.Count);
        return Palette[idx].Resolve(in ctx);
    }
}

/// <summary>
/// A noise area pattern (TP13): a fractal-noise field over the footprint mapped through an ordered ramp of
/// <paramref name="Stops"/> — the value in [0,1) selects a band, so <c>n</c> stops give <c>n</c> materials.
/// <paramref name="Octaves"/> = 1 is single-octave value noise; more octaves give the cloudier fractal look.
/// Same seam as voronoi, a smoothly varying field instead of hard cells.
/// </summary>
public sealed record NoiseMaterial(uint Seed, int Scale, int Octaves, IReadOnlyList<TerrainMaterial> Stops) : TerrainMaterial
{
    public override (int Id, int Data) Resolve(in BucketContext ctx)
    {
        if (Stops is not { Count: > 0 }) return (Blocks.Stone, 0);
        double v = PatternNoise.Fbm(ctx.X, ctx.Z, Seed, Scale, Octaves);
        int idx = Math.Clamp((int)(v * Stops.Count), 0, Stops.Count - 1);
        return Stops[idx].Resolve(in ctx);
    }
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
}
