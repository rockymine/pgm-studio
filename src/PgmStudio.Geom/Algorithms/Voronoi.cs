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
    public static (int Gx, int Gz) NearestSite(int x, int z, uint seed, int cellSize)
    {
        int size = Math.Max(1, cellSize);
        int gx = (int)Math.Floor((double)x / size), gz = (int)Math.Floor((double)z / size);
        long bestD = long.MaxValue;
        int bestGx = gx, bestGz = gz;
        for (var dgz = -1; dgz <= 1; dgz++)
        for (var dgx = -1; dgx <= 1; dgx++)
        {
            int cgx = gx + dgx, cgz = gz + dgz;
            int sx = cgx * size + (int)(PatternNoise.Unit(cgx, cgz, seed) * size);
            int sz = cgz * size + (int)(PatternNoise.Unit(cgx, cgz, seed ^ ZitterSalt) * size);
            long ddx = x - sx, ddz = z - sz, d = ddx * ddx + ddz * ddz;
            if (d < bestD) { bestD = d; bestGx = cgx; bestGz = cgz; }
        }
        return (bestGx, bestGz);
    }
}
