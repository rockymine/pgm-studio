namespace PgmStudio.Geom;

/// <summary>
/// Exact union of axis-aligned integer rectangles into a single boundary ring. Coordinate-compresses the
/// rectangle edges into a cell grid, marks the cells covered by any rectangle, walks the covered region's
/// boundary as directed unit edges (interior edges cancel against their reverse), then merges collinear runs
/// into the minimal corner ring. Pure integer geometry: a set of grid-aligned rectangles that share
/// full-length borders unions with no rounding. Returns the outer ring (largest by area); holes are not
/// carried (the callers union simply-connected piece groups).
/// </summary>
public static class RectilinearUnion
{
    /// <summary>Union the <paramref name="rects"/> (each <c>[minX, minZ, maxX, maxZ]</c>, integer, min &lt;
    /// max) into the outer boundary ring — an open list of <c>[x, z]</c> corner vertices (no repeated close).
    /// Returns an empty list if no rectangle has area.</summary>
    public static List<double[]> Outline(IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> rects)
    {
        var real = rects.Where(r => r.MaxX > r.MinX && r.MaxZ > r.MinZ).ToList();
        if (real.Count == 0) return [];

        var xs = real.SelectMany(r => new[] { r.MinX, r.MaxX }).Distinct().OrderBy(v => v).ToArray();
        var zs = real.SelectMany(r => new[] { r.MinZ, r.MaxZ }).Distinct().OrderBy(v => v).ToArray();

        // Directed CCW boundary edges: add each covered cell's four sides; an interior side meets its
        // neighbour's reverse and the two cancel, leaving only the outline edges (all consistently wound).
        var edges = new HashSet<(int, int, int, int)>();
        void Add(int ax, int az, int bx, int bz)
        {
            var rev = (bx, bz, ax, az);
            if (!edges.Remove(rev)) edges.Add((ax, az, bx, bz));
        }
        for (var i = 0; i < xs.Length - 1; i++)
            for (var j = 0; j < zs.Length - 1; j++)
            {
                double mx = (xs[i] + xs[i + 1]) / 2.0, mz = (zs[j] + zs[j + 1]) / 2.0;
                if (!real.Any(r => r.MinX < mx && mx < r.MaxX && r.MinZ < mz && mz < r.MaxZ)) continue;
                int x0 = xs[i], x1 = xs[i + 1], z0 = zs[j], z1 = zs[j + 1];
                Add(x0, z0, x1, z0); Add(x1, z0, x1, z1); Add(x1, z1, x0, z1); Add(x0, z1, x0, z0);
            }

        var rings = ChainRings(edges);
        return rings.Count == 0 ? [] : rings.MaxBy(SignedArea)!;
    }

    // Stitch the surviving directed edges head→tail into closed rings, merging collinear corners.
    private static List<List<double[]>> ChainRings(HashSet<(int, int, int, int)> edges)
    {
        var next = new Dictionary<(int, int), List<(int, int)>>();
        foreach (var (ax, az, bx, bz) in edges)
            (next.TryGetValue((ax, az), out var l) ? l : next[(ax, az)] = []).Add((bx, bz));

        var rings = new List<List<double[]>>();
        while (next.Count > 0)
        {
            var start = next.Keys.First();
            var raw = new List<(int, int)> { start };
            var cur = start;
            while (true)
            {
                var outs = next[cur];
                var nxt = outs[^1];
                outs.RemoveAt(outs.Count - 1);
                if (outs.Count == 0) next.Remove(cur);
                if (nxt == start) break;
                raw.Add(nxt);
                cur = nxt;
            }
            rings.Add(MergeCollinear(raw));
        }
        return rings;
    }

    private static List<double[]> MergeCollinear(List<(int X, int Z)> ring)
    {
        var n = ring.Count;
        var outp = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var p = ring[(i - 1 + n) % n];
            var c = ring[i];
            var q = ring[(i + 1) % n];
            var cross = (long)(c.X - p.X) * (q.Z - c.Z) - (long)(c.Z - p.Z) * (q.X - c.X);
            if (cross != 0) outp.Add([c.X, c.Z]);
        }
        return outp;
    }

    private static double SignedArea(List<double[]> ring)
    {
        double a = 0;
        for (int i = 0, n = ring.Count; i < n; i++)
        {
            var p = ring[i];
            var q = ring[(i + 1) % n];
            a += p[0] * q[1] - q[0] * p[1];
        }
        return Math.Abs(a) / 2.0;
    }
}
