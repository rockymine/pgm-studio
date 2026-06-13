namespace PgmStudio.Analysis;

/// <summary>
/// Side-view depth projection (B5, port of <c>_build_depth_map</c> in routes/build_regions.py).
/// Projects vertical solid segments onto a 2D (primary × y) grid; each cell holds the nearest
/// depth index normalised to 0–255 (0 = nearest), or -1 for empty. Feeds the Build-Regions
/// side-view canvas (C7). For <c>axis="z"</c> (looking along Z) primary=x, depth=z; for
/// <c>axis="x"</c> primary=z, depth=x.
/// </summary>
public static class SideView
{
    public sealed record DepthMap(string Axis, int PrimaryMin, int PrimaryCount, int YMin, int YCount, short[] Depth);

    /// <summary>Build the depth map, or null when there are no segments.</summary>
    public static DepthMap? Build(IEnumerable<(int x, int z, int ys, int ye)> segments, string axis)
    {
        var segs = segments as IList<(int x, int z, int ys, int ye)> ?? segments.ToList();
        if (segs.Count == 0) return null;

        // primary runs across the view; depth is the axis we look along (nearest wins).
        static int Primary((int x, int z, int ys, int ye) s, string a) => a == "z" ? s.x : s.z;
        static int Depth((int x, int z, int ys, int ye) s, string a) => a == "z" ? s.z : s.x;

        int pMin = int.MaxValue, pMax = int.MinValue, dMin = int.MaxValue, dMax = int.MinValue,
            yMin = int.MaxValue, yMax = int.MinValue;
        foreach (var s in segs)
        {
            var p = Primary(s, axis); var d = Depth(s, axis);
            if (p < pMin) pMin = p; if (p > pMax) pMax = p;
            if (d < dMin) dMin = d; if (d > dMax) dMax = d;
            if (s.ys < yMin) yMin = s.ys; if (s.ye > yMax) yMax = s.ye;
        }

        int P = pMax - pMin + 1, H = yMax - yMin + 1, D = dMax - dMin + 1;

        // front[p, y] = nearest depth index; D = empty sentinel.
        var front = new int[P * H];
        Array.Fill(front, D);
        foreach (var s in segs)
        {
            var pi = Primary(s, axis) - pMin;
            var di = Depth(s, axis) - dMin;
            var sY = s.ys - yMin;
            var eY = s.ye - yMin + 1;          // exclusive, matches numpy s:e slice
            var rowBase = pi * H;
            for (var y = sY; y < eY; y++)
                if (di < front[rowBase + y]) front[rowBase + y] = di;
        }

        // Normalise to 0–255 (float32 path, matching numpy), empty → -1.
        var depth = new short[P * H];
        for (var i = 0; i < depth.Length; i++)
        {
            if (front[i] >= D) { depth[i] = -1; continue; }
            depth[i] = D > 1 ? (short)((float)front[i] / (D - 1) * 255f + 0.5f) : (short)0;
        }

        return new DepthMap(axis, pMin, P, yMin, H, depth);
    }
}
