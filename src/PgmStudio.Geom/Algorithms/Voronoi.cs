namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Jittered-grid Voronoi: the plane is tiled by a grid of period <c>cellSize</c> with one deterministic seed
/// point per grid cell, and every point belongs to the region whose seed point is nearest — irregular patches
/// roughly <c>cellSize</c> across, with no global precompute. <c>NearestSite</c> answers the region a
/// single point falls in by checking only the 3×3 grid-cell neighbourhood, which is enough because a seed point
/// never leaves its own grid cell. Site jitter and the region id are hashes of the grid cell (via
/// <see cref="PatternNoise"/>), so the tiling is stable for a given seed.
///
/// <para>Every entry point has a <b>volume</b> form that tiles space rather than the plane, at its own vertical
/// period. The plane form is not a special case of it — it searches nine sites where the volume searches
/// twenty-seven — so both are kept and a caller takes the one whose answer it needs.</para>
/// </summary>
public static class Voronoi
{
    // Independent hash streams for the jitter of each axis after the first, so a site's offsets are uncorrelated.
    private const uint ZitterSalt = 0x9E3779B9u;
    private const uint YitterSalt = 0x7FEB352Du;

    /// <summary>The grid cell of the seed point nearest to <paramref name="x"/>,<paramref name="z"/> — the
    /// stable id of the Voronoi region that owns the point. A caller turns it into content by hashing it (e.g.
    /// <c>PatternNoise.Hash(gx, gz, seed) % paletteCount</c>).</summary>
    public static (int Gx, int Gz) NearestSite(int x, int z, uint seed, int cellSize, double jitter = 1)
    {
        var (_, _, gx, gz) = NearestTwo(x, z, seed, cellSize, jitter);
        return (gx, gz);
    }

    /// <summary>The grid cell of the seed point nearest to a point in a <em>volume</em> — the three-axis
    /// <see cref="NearestSite(int,int,uint,int,double)"/>, with its own vertical period.</summary>
    public static (int Gx, int Gy, int Gz) NearestSite(int x, int y, int z, uint seed, int cellSize, int rise, double jitter = 1)
    {
        var (_, _, gx, gy, gz) = NearestTwo(x, y, z, seed, cellSize, rise, jitter);
        return (gx, gy, gz);
    }

    /// <summary>The distances to the two nearest seed points and the grid cell of the nearest one. The gap <c>D2
    /// - D1</c> is what a cellular (honeycomb) pattern reads: it is zero on the boundary between two regions —
    /// where a point is equidistant from both sites — and grows towards a region's interior, so a small gap marks
    /// a cell wall and a large one marks the fill inside a cell. This is the Worley <c>F2 − F1</c> edge, computed
    /// from the same 3×3 grid-cell neighbourhood the nearest-site search uses. <para><b>jitter</b> — How far a
    /// site may sit from the middle of its own grid cell, as a fraction of the cell: 1 lets it land anywhere in
    /// the cell (the irregular default), 0 pins every site to the middle and the diagram degenerates to the
    /// square grid itself. Between the two the cells stay convex but even out, which is what a pattern wants when
    /// the regions should read as a fabric rather than as shards.</para></summary>
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

    /// <summary>
    /// The three-axis <see cref="NearestTwo(int,int,uint,int,double)"/>: the same jittered grid extended into a
    /// volume, so the regions are cells rather than columns and <c>D2 - D1</c> falls off towards a cell's top
    /// and bottom as well as its sides. This is what gives a pattern on a wall face something other than the
    /// vertical stripes a plane diagram necessarily paints there.
    ///
    /// <para><paramref name="rise"/> is the vertical period, separate from <paramref name="cellSize"/>, because
    /// terrain is a slab: cells as tall as they are wide would put barely one layer of them in a wall. It costs
    /// a 3×3×3 neighbourhood — three times the sites of the plane search — which is why it is reached only when
    /// a caller asks for a volume.</para>
    /// </summary>
    public static (double D1, double D2, int Gx, int Gy, int Gz) NearestTwo(
        int x, int y, int z, uint seed, int cellSize, int rise, double jitter = 1)
    {
        int size = Math.Max(1, cellSize), height = Math.Max(1, rise);
        double spread = Math.Clamp(jitter, 0, 1);
        int gx = (int)Math.Floor((double)x / size), gy = (int)Math.Floor((double)y / height),
            gz = (int)Math.Floor((double)z / size);
        long best = long.MaxValue, second = long.MaxValue;
        int bestGx = gx, bestGy = gy, bestGz = gz;
        for (var dgy = -1; dgy <= 1; dgy++)
        for (var dgz = -1; dgz <= 1; dgz++)
        for (var dgx = -1; dgx <= 1; dgx++)
        {
            int cgx = gx + dgx, cgy = gy + dgy, cgz = gz + dgz;
            int sx = cgx * size + (int)((0.5 + (PatternNoise.Unit(cgx, cgy, cgz, seed) - 0.5) * spread) * size);
            int sy = cgy * height + (int)((0.5 + (PatternNoise.Unit(cgx, cgy, cgz, seed ^ YitterSalt) - 0.5) * spread) * height);
            int sz = cgz * size + (int)((0.5 + (PatternNoise.Unit(cgx, cgy, cgz, seed ^ ZitterSalt) - 0.5) * spread) * size);
            long ddx = x - sx, ddy = y - sy, ddz = z - sz;
            // The vertical axis is measured in cells rather than blocks, so a flat cell's gap grows at the same
            // rate in every direction: without it a rise of 4 under a cellSize of 10 would make every boundary
            // a horizontal one and the pattern would read as layers, not cells.
            double stretch = (double)size / height;
            long scaled = (long)Math.Round(ddy * stretch);
            long d = ddx * ddx + scaled * scaled + ddz * ddz;
            if (d < best) { second = best; best = d; bestGx = cgx; bestGy = cgy; bestGz = cgz; }
            else if (d < second) second = d;
        }
        return (Math.Sqrt(best), Math.Sqrt(second), bestGx, bestGy, bestGz);
    }
}
