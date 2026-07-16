namespace PgmStudio.Geom;

/// <summary>
/// Rectilinear cell-set substrate — the shared 4-connected raster primitives (neighbour iteration, flood fill,
/// connected components, enclosed-void detection, reflex-corner counting, fold detection, bounding box, min run
/// width) that the shape classifier, the lane read, and the board deriver all read cell topology through.
/// Pure integer-grid geometry over <c>(x, z)</c> cells; references nothing above <c>Geom</c>.
/// </summary>
public static class Cells
{
    /// <summary>The four orthogonal neighbours of <paramref name="c"/> (4-connectivity).</summary>
    public static IEnumerable<(int, int)> N4((int, int) c)
    {
        yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2);
        yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1);
    }

    /// <summary>Inclusive bounding box of a non-empty cell set.</summary>
    public static (int MinX, int MinZ, int MaxX, int MaxZ) BoundingBox(IReadOnlyCollection<(int, int)> cells)
    {
        int mnx = int.MaxValue, mnz = int.MaxValue, mxx = int.MinValue, mxz = int.MinValue;
        foreach (var (x, z) in cells)
        {
            if (x < mnx) mnx = x; if (x > mxx) mxx = x;
            if (z < mnz) mnz = z; if (z > mxz) mxz = z;
        }
        return (mnx, mnz, mxx, mxz);
    }

    /// <summary>The 4-connected component of <paramref name="within"/> reachable from <paramref name="seeds"/>
    /// (seeds not in <paramref name="within"/> are ignored). Returns the component's cells.</summary>
    public static HashSet<(int, int)> Flood(IEnumerable<(int, int)> seeds, IReadOnlySet<(int, int)> within)
    {
        var comp = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        foreach (var s in seeds) if (within.Contains(s) && comp.Add(s)) q.Enqueue(s);
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (within.Contains(n) && comp.Add(n)) q.Enqueue(n); }
        return comp;
    }

    /// <summary>The shortest 4-connected path from <paramref name="from"/> to <paramref name="to"/> through
    /// <paramref name="within"/> — cardinal steps only, so the path is rectilinear (no diagonal shortcut) and
    /// routes around any cell not in the set, hugging its border. Returns the cell sequence including both ends,
    /// or null when either end is outside the set or <paramref name="to"/> is unreachable. BFS, so the first
    /// path reaching the target is a shortest one.</summary>
    public static List<(int, int)>? ShortestPath((int, int) from, (int, int) to, IReadOnlySet<(int, int)> within)
    {
        if (!within.Contains(from) || !within.Contains(to)) return null;
        if (from == to) return [from];

        var prev = new Dictionary<(int, int), (int, int)> { [from] = from };
        var q = new Queue<(int, int)>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in N4(c))
            {
                if (!within.Contains(n) || !prev.TryAdd(n, c)) continue;
                if (n == to)
                {
                    var path = new List<(int, int)>();
                    for (var p = to; ; p = prev[p]) { path.Add(p); if (p == from) break; }
                    path.Reverse();
                    return path;
                }
                q.Enqueue(n);
            }
        }
        return null;
    }

    /// <summary>The step count of the shortest 4-connected path (cells − 1), or null when unreachable. This is
    /// the rectilinear traversal distance in cells — Manhattan in open space, longer where it detours a gap.</summary>
    public static int? PathLength((int, int) from, (int, int) to, IReadOnlySet<(int, int)> within) =>
        ShortestPath(from, to, within) is { } path ? path.Count - 1 : null;

    /// <summary>Number of 4-connected components of a cell set.</summary>
    public static int Components(IReadOnlySet<(int, int)> cells)
    {
        var seen = new HashSet<(int, int)>();
        int n = 0;
        foreach (var c in cells)
        {
            if (!seen.Add(c)) continue;
            n++;
            var q = new Queue<(int, int)>(); q.Enqueue(c);
            while (q.Count > 0) { var d = q.Dequeue(); foreach (var m in N4(d)) if (cells.Contains(m) && seen.Add(m)) q.Enqueue(m); }
        }
        return n;
    }

    /// <summary>True when the footprint encloses a background cell unreachable from outside its bounding box
    /// (a hole). A one-cell margin around the bounding box seeds the outside flood.</summary>
    public static bool HasEnclosedVoid(IReadOnlySet<(int, int)> fill)
    {
        int mnx = fill.Min(c => c.Item1) - 1, mxx = fill.Max(c => c.Item1) + 1, mnz = fill.Min(c => c.Item2) - 1, mxz = fill.Max(c => c.Item2) + 1;
        var outside = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>(); q.Enqueue((mnx, mnz)); outside.Add((mnx, mnz));
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (n.Item1 >= mnx && n.Item1 <= mxx && n.Item2 >= mnz && n.Item2 <= mxz && !fill.Contains(n) && outside.Add(n)) q.Enqueue(n); }
        for (var x = mnx; x <= mxx; x++) for (var z = mnz; z <= mxz; z++) if (!fill.Contains((x, z)) && !outside.Contains((x, z))) return true;
        return false;
    }

    /// <summary>The number of reflex (concave) corners of a cell set's outline — the bend count, read
    /// width-independently (a lane and the same lane widened uniformly turn the same number of times).</summary>
    public static int ReflexCorners(IReadOnlySet<(int, int)> cells)
    {
        int mnx = cells.Min(c => c.Item1), mxx = cells.Max(c => c.Item1) + 1, mnz = cells.Min(c => c.Item2), mxz = cells.Max(c => c.Item2) + 1, r = 0;
        for (var x = mnx; x <= mxx; x++)
            for (var z = mnz; z <= mxz; z++)
            {
                int cnt = 0;
                if (cells.Contains((x, z))) cnt++; if (cells.Contains((x - 1, z))) cnt++;
                if (cells.Contains((x, z - 1))) cnt++; if (cells.Contains((x - 1, z - 1))) cnt++;
                if (cnt == 3) r++;
            }
        return r;
    }

    /// <summary>True when the cell set doubles back on itself: some grid row or column meets it in two or more
    /// runs (the set is not orthogonally convex). A fold wrapping a concavity always puts two separate runs on
    /// the lines crossing that concavity; a staircase never does. A property of the cells alone, width-
    /// independent — unlike a bounding-box-edge test, the verdict cannot flip merely because added neighbour
    /// cells extend the bounding box.</summary>
    public static bool HasFold(IReadOnlySet<(int, int)> cells)
    {
        if (cells.Count == 0) return false;
        var (mnx, mnz, mxx, mxz) = BoundingBox(cells);
        for (var z = mnz; z <= mxz; z++)
        {
            int runs = 0; var inRun = false;
            for (var x = mnx; x <= mxx; x++)
            {
                if (cells.Contains((x, z))) { if (!inRun) { runs++; inRun = true; } }
                else inRun = false;
            }
            if (runs >= 2) return true;
        }
        for (var x = mnx; x <= mxx; x++)
        {
            int runs = 0; var inRun = false;
            for (var z = mnz; z <= mxz; z++)
            {
                if (cells.Contains((x, z))) { if (!inRun) { runs++; inRun = true; } }
                else inRun = false;
            }
            if (runs >= 2) return true;
        }
        return false;
    }

    /// <summary>The minimum corridor cross-section (cells) at <paramref name="seeds"/>, measured as the smaller
    /// of each seed's horizontal and vertical run within <paramref name="cells"/>, clamped to [2, 6] (the lane
    /// width convention). Runs never fall below 1, so the clamp's lower bound is the effective floor.</summary>
    public static int MinRunWidth(IReadOnlySet<(int, int)> cells, IEnumerable<(int, int)> seeds)
    {
        int Hrun((int, int) c) { int n = 1; for (var x = c.Item1 - 1; cells.Contains((x, c.Item2)); x--) n++; for (var x = c.Item1 + 1; cells.Contains((x, c.Item2)); x++) n++; return n; }
        int Vrun((int, int) c) { int n = 1; for (var z = c.Item2 - 1; cells.Contains((c.Item1, z)); z--) n++; for (var z = c.Item2 + 1; cells.Contains((c.Item1, z)); z++) n++; return n; }
        return Math.Clamp(seeds.Min(c => Math.Min(Hrun(c), Vrun(c))), 2, 6);
    }
}
