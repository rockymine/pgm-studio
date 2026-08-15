namespace PgmStudio.Geom;

/// <summary>
/// Exact union of axis-aligned integer rectangles into its boundary rings. Coordinate-compresses the
/// rectangle edges into a cell grid, marks the cells covered by any rectangle, walks the covered region's
/// boundary as directed unit edges (interior edges cancel against their reverse), then merges collinear runs
/// into minimal corner rings. Pure integer geometry: a set of grid-aligned rectangles that share full-length
/// borders unions with no rounding. Returns EVERY disjoint outer ring — a set of rectangles that falls into
/// several connected patches yields one ring per patch, so no patch is silently dropped. Holes (enclosed
/// voids) are not carried; a patch with a hole yields only its outer ring, which is what
/// <see cref="EnclosedVoids"/> is for. <see cref="Difference"/> outlines the same union with a second set of
/// rectangles taken back out.
/// </summary>
public static class RectilinearUnion
{
    /// <summary>Union the <paramref name="rects"/> (each <c>[minX, minZ, maxX, maxZ]</c>, integer, min &lt;
    /// max) into its disjoint outer boundary rings — one per connected covered patch, each an open list of
    /// <c>[x, z]</c> corner vertices (no repeated close), wound CCW, ordered largest patch first. Returns an
    /// empty list if no rectangle has area.</summary>
    public static List<List<double[]>> Outlines(IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> rects)
        => Outlines(rects, []);

    /// <summary>The same outlines for the region the <paramref name="rects"/> cover and the
    /// <paramref name="minus"/> rectangles do not — one ring per connected patch of the difference, so a
    /// subtraction that splits a patch in two yields a ring each and one that erases it yields none.</summary>
    public static List<List<double[]>> Difference(
        IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> rects,
        IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> minus)
        => Outlines(rects, minus);

    private static List<List<double[]>> Outlines(
        IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> rects,
        IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> minus)
    {
        var real = Real(rects);
        if (real.Count == 0) return [];
        var cut = Real(minus);
        var (xs, zs) = Grid(real, cut);

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
                if (!Covers(real, xs, zs, i, j) || Covers(cut, xs, zs, i, j)) continue;
                int x0 = xs[i], x1 = xs[i + 1], z0 = zs[j], z1 = zs[j + 1];
                Add(x0, z0, x1, z0); Add(x1, z0, x1, z1); Add(x1, z1, x0, z1); Add(x0, z1, x0, z0);
            }

        // Outer boundaries wind CCW (positive signed area); hole rings wind CW — keep only the outer patches,
        // largest first.
        return ChainRings(edges)
            .Where(r => SignedArea(r) > 0)
            .OrderByDescending(SignedArea)
            .ToList();
    }

    /// <summary>The ground the union of <paramref name="rects"/> encircles but no rectangle covers — the
    /// enclosed voids an <see cref="Outlines"/> ring states as solid — as disjoint rectangles, in reading
    /// order. A void is found by connectivity, not by winding: the uncovered cells that cannot reach the
    /// outside are enclosed. <paramref name="filled"/> names rectangles that count as ground without joining
    /// the union, so a void something else lays into is not reported and a partly covered one comes back as
    /// only the part that stays open. Empty when the union encloses nothing.</summary>
    public static List<(int MinX, int MinZ, int MaxX, int MaxZ)> EnclosedVoids(
        IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> rects,
        IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)>? filled = null)
    {
        var real = Real(rects);
        if (real.Count == 0) return [];
        var ground = Real(filled ?? []);
        var (xs, zs) = Grid(real, ground);
        int nx = xs.Length - 1, nz = zs.Length - 1;

        // Flood the uncovered cells inward from the compressed grid's border. Everything the flood cannot
        // reach is walled in by the union — the grid spans the union's own extent, so every border cell is
        // outside by construction.
        var open = new bool[nx, nz];
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < nz; j++)
                open[i, j] = !Covers(real, xs, zs, i, j);

        var outside = new bool[nx, nz];
        var stack = new Stack<(int I, int J)>();
        for (var i = 0; i < nx; i++) { Seed(i, 0); Seed(i, nz - 1); }
        for (var j = 0; j < nz; j++) { Seed(0, j); Seed(nx - 1, j); }
        void Seed(int i, int j)
        {
            if (i < 0 || j < 0 || i >= nx || j >= nz || !open[i, j] || outside[i, j]) return;
            outside[i, j] = true;
            stack.Push((i, j));
        }
        while (stack.Count > 0)
        {
            var (i, j) = stack.Pop();
            Seed(i - 1, j); Seed(i + 1, j); Seed(i, j - 1); Seed(i, j + 1);
        }

        // Enclosed = open, unreached, and not standing on ground something else lays in.
        var enclosed = new bool[nx, nz];
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < nz; j++)
                enclosed[i, j] = open[i, j] && !outside[i, j] && !Covers(ground, xs, zs, i, j);

        return Merge(enclosed, xs, zs);
    }

    // Greedily merge the marked cells into disjoint rectangles: from each unclaimed cell run right while the
    // row stays marked, then down while the whole span does. An L comes back as two rectangles, a plain
    // rectangular void as one.
    private static List<(int MinX, int MinZ, int MaxX, int MaxZ)> Merge(bool[,] marked, int[] xs, int[] zs)
    {
        int nx = xs.Length - 1, nz = zs.Length - 1;
        var taken = new bool[nx, nz];
        var output = new List<(int, int, int, int)>();
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                if (!marked[i, j] || taken[i, j]) continue;
                var iEnd = i;
                while (iEnd + 1 < nx && marked[iEnd + 1, j] && !taken[iEnd + 1, j]) iEnd++;
                var jEnd = j;
                while (jEnd + 1 < nz && Enumerable.Range(i, iEnd - i + 1).All(k => marked[k, jEnd + 1] && !taken[k, jEnd + 1]))
                    jEnd++;
                for (var k = i; k <= iEnd; k++)
                    for (var m = j; m <= jEnd; m++) taken[k, m] = true;
                output.Add((xs[i], zs[j], xs[iEnd + 1], zs[jEnd + 1]));
            }
        return output;
    }

    /// <summary>The rectangles with area, as <see cref="CellRect"/> — the same exclusive-max integer
    /// convention this walk already works in, which is why the private copy it used to build was never a
    /// different shape from the one the leaf already exported.</summary>
    private static List<CellRect> Real(IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> rects)
        => [.. rects.Where(r => r.MaxX > r.MinX && r.MaxZ > r.MinZ)
                    .Select(r => CellRect.FromBounds(r.MinX, r.MinZ, r.MaxX, r.MaxZ))];

    // The compressed grid: every edge of the union, plus the cutting rectangles' edges that fall strictly
    // inside its extent — an edge beyond it cannot split a cell within it, and keeping the grid to the
    // union's own extent is what makes every border cell outside it.
    private static (int[] Xs, int[] Zs) Grid(List<CellRect> real, List<CellRect> cut)
    {
        return (Axis(r => r.X, r => r.MaxX), Axis(r => r.Z, r => r.MaxZ));

        int[] Axis(Func<CellRect, int> lo, Func<CellRect, int> hi)
        {
            var own = real.SelectMany(r => new[] { lo(r), hi(r) }).ToList();
            int min = own.Min(), max = own.Max();
            var cuts = cut.SelectMany(r => new[] { lo(r), hi(r) }).Where(v => v > min && v < max);
            return [.. own.Concat(cuts).Distinct().OrderBy(v => v)];
        }
    }

    private static bool Covers(List<CellRect> rects, int[] xs, int[] zs, int i, int j)
    {
        double mx = (xs[i] + xs[i + 1]) / 2.0, mz = (zs[j] + zs[j + 1]) / 2.0;
        return rects.Any(r => r.X < mx && mx < r.MaxX && r.Z < mz && mz < r.MaxZ);
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

    // Shoelace signed area: positive for a CCW (outer) ring, negative for a CW (hole) ring.
    private static double SignedArea(List<double[]> ring)
    {
        double a = 0;
        for (int i = 0, n = ring.Count; i < n; i++)
        {
            var p = ring[i];
            var q = ring[(i + 1) % n];
            a += p[0] * q[1] - q[0] * p[1];
        }
        return a / 2.0;
    }
}
