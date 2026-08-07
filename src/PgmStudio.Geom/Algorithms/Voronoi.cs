namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Jittered-grid Voronoi: the plane is tiled by a grid of period <c>cellSize</c> with one deterministic seed
/// point per grid cell, and every point belongs to the region whose seed point is nearest — irregular patches
/// roughly <c>cellSize</c> across, with no global precompute. <see cref="NearestSite"/> answers the region a
/// single point falls in by checking only the 3×3 grid-cell neighbourhood, which is enough because a seed point
/// never leaves its own grid cell. Site jitter and the region id are hashes of the grid cell (via
/// <see cref="PatternNoise"/>), so the tiling is stable for a given seed.
/// </summary>
public static class Voronoi
{
    // A second, independent hash stream for the z jitter, so a site's x and z offsets are uncorrelated.
    private const uint ZitterSalt = 0x9E3779B9u;

    /// <summary>The grid cell of the seed point nearest to <paramref name="x"/>,<paramref name="z"/> — the
    /// stable id of the Voronoi region that owns the point. A caller turns it into content by hashing it (e.g.
    /// <c>PatternNoise.Hash(gx, gz, seed) % paletteCount</c>).</summary>
    public static (int Gx, int Gz) NearestSite(int x, int z, uint seed, int cellSize, double jitter = 1)
    {
        var (_, _, gx, gz) = NearestTwo(x, z, seed, cellSize, jitter);
        return (gx, gz);
    }

    /// <summary>The distances to the two nearest seed points and the grid cell of the nearest one. The gap
    /// <c>D2 - D1</c> is what a cellular (honeycomb) pattern reads: it is zero on the boundary between two regions
    /// — where a point is equidistant from both sites — and grows towards a region's interior, so a small gap
    /// marks a cell wall and a large one marks the fill inside a cell. This is the Worley <c>F2 − F1</c> edge,
    /// computed from the same 3×3 grid-cell neighbourhood the nearest-site search uses.</summary>
    /// <param name="jitter">How far a site may sit from the middle of its own grid cell, as a fraction of the
    /// cell: 1 lets it land anywhere in the cell (the irregular default), 0 pins every site to the middle and
    /// the diagram degenerates to the square grid itself. Between the two the cells stay convex but even out,
    /// which is what a pattern wants when the regions should read as a fabric rather than as shards.</param>
    public static (double D1, double D2, int Gx, int Gz) NearestTwo(int x, int z, uint seed, int cellSize, double jitter = 1)
    {
        int size = Math.Max(1, cellSize);
        double spread = Math.Clamp(jitter, 0, 1);
        int gx = (int)Math.Floor((double)x / size), gz = (int)Math.Floor((double)z / size);
        long best = long.MaxValue, second = long.MaxValue;
        int bestGx = gx, bestGz = gz;
        for (var dgz = -1; dgz <= 1; dgz++)
        for (var dgx = -1; dgx <= 1; dgx++)
        {
            int cgx = gx + dgx, cgz = gz + dgz;
            int sx = cgx * size + (int)((0.5 + (PatternNoise.Unit(cgx, cgz, seed) - 0.5) * spread) * size);
            int sz = cgz * size + (int)((0.5 + (PatternNoise.Unit(cgx, cgz, seed ^ ZitterSalt) - 0.5) * spread) * size);
            long ddx = x - sx, ddz = z - sz, d = ddx * ddx + ddz * ddz;
            if (d < best) { second = best; best = d; bestGx = cgx; bestGz = cgz; }
            else if (d < second) second = d;
        }
        return (Math.Sqrt(best), Math.Sqrt(second), bestGx, bestGz);
    }
}
