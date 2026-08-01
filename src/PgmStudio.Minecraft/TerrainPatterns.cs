namespace PgmStudio.Minecraft;

// Pattern materials (docs/world-export/terrain-painting.md TP13): a TerrainMaterial that varies the block across
// a bucket's cells instead of resolving one id. A pattern only changes WHICH block a cell takes, never WHICH
// cells are in the bucket, so it plugs into the existing material seam and the geometry (profile → bands) is
// untouched. Every choice is a deterministic hash of a seed plus the cell (and, for wall-runs, the perimeter arc
// the profile computed) — never RNG, so a map exports the same pattern every time. Palette / stop / run entries
// are themselves TerrainMaterial, so a pattern nests a solid, a team tint, a layer stack, or another pattern.

/// <summary>Deterministic value/fractal noise and lattice hashing shared by the pattern materials. Pure: the
/// same coordinates and seed always give the same result, on any machine.</summary>
internal static class PatternNoise
{
    public static uint Hash(int x, int z, uint seed)
    {
        unchecked
        {
            uint h = seed + 0x9E3779B9u;
            h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19); h *= 0xC2B2AE3Du;
            h ^= (uint)z * 0x27D4EB2Fu; h = (h << 15) | (h >> 17); h *= 0x165667B1u;
            h ^= h >> 16; return h;
        }
    }

    /// <summary>A hashed unit value in [0,1) for a lattice point.</summary>
    public static double Unit(int x, int z, uint seed) => (Hash(x, z, seed) & 0xFFFFFF) / (double)0x1000000;

    /// <summary>Smooth (smoothstep-interpolated) value noise at a scale, in [0,1).</summary>
    public static double Value(int x, int z, uint seed, int scale)
    {
        double fx = (double)x / scale, fz = (double)z / scale;
        int x0 = (int)Math.Floor(fx), z0 = (int)Math.Floor(fz);
        double tx = fx - x0, tz = fz - z0;
        double v00 = Unit(x0, z0, seed), v10 = Unit(x0 + 1, z0, seed), v01 = Unit(x0, z0 + 1, seed), v11 = Unit(x0 + 1, z0 + 1, seed);
        double sx = tx * tx * (3 - 2 * tx), sz = tz * tz * (3 - 2 * tz);
        double a = v00 + (v10 - v00) * sx, b = v01 + (v11 - v01) * sx;
        return a + (b - a) * sz;
    }

    /// <summary>Fractional Brownian motion: <paramref name="octaves"/> octaves of value noise summed at halving
    /// scale and amplitude, normalised to [0,1). One octave is plain value noise; more octaves add finer detail.</summary>
    public static double Fbm(int x, int z, uint seed, int scale, int octaves)
    {
        double sum = 0, amp = 1, norm = 0; int sc = Math.Max(1, scale);
        for (var o = 0; o < Math.Max(1, octaves); o++)
        {
            sum += amp * Value(x, z, seed + (uint)o * 7919u, sc);
            norm += amp; amp *= 0.5; sc = Math.Max(1, sc / 2);
        }
        return norm > 0 ? sum / norm : 0;
    }
}

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
        if (Palette.Count == 0) return (Blocks.Stone, 0);
        int size = Math.Max(1, CellSize);
        int gx = (int)Math.Floor((double)ctx.X / size), gz = (int)Math.Floor((double)ctx.Z / size);
        long bestD = long.MaxValue; int bestGX = gx, bestGZ = gz;
        for (var dgz = -1; dgz <= 1; dgz++)
        for (var dgx = -1; dgx <= 1; dgx++)
        {
            int cgx = gx + dgx, cgz = gz + dgz;
            int sx = cgx * size + (int)(PatternNoise.Unit(cgx, cgz, Seed) * size);
            int sz = cgz * size + (int)(PatternNoise.Unit(cgx, cgz, Seed ^ 0x9E3779B9u) * size);
            long ddx = ctx.X - sx, ddz = ctx.Z - sz, d = ddx * ddx + ddz * ddz;
            if (d < bestD) { bestD = d; bestGX = cgx; bestGZ = cgz; }
        }
        int idx = (int)(PatternNoise.Hash(bestGX, bestGZ, Seed) % (uint)Palette.Count);
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
        if (Stops.Count == 0) return (Blocks.Stone, 0);
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
        if (Runs.Count == 0) return (Blocks.Stone, 0);
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
