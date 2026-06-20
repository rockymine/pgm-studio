using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Infers the over-void build regions that knit a sketch's separate islands into one navigable plane.
/// The solid cells are split into connected components; a minimum spanning tree over nearest-cell
/// distance picks which islands to link, and each tree edge becomes a build-area rectangle spanning the
/// gap (a corridor at least <c>width</c> across that overlaps both ends). Feeding these as the map's build
/// areas makes the finished sketch traversable — the spawn↔wool chain reaches across the gaps — without
/// any hand-placed regions.
/// </summary>
public static class AutoBridge
{
    public static List<Rect> Infer(IEnumerable<(int X, int Z)> cells, double width = 12)
    {
        var comps = Components(cells);
        if (comps.Count < 2) return [];

        var bridges = new List<Rect>();
        foreach (var (i, j) in SpanningEdges(comps))
        {
            var (ax, az, bx, bz) = Nearest(comps[i], comps[j]);
            bridges.Add(Corridor(ax, az, bx, bz, width));
        }
        return bridges;
    }

    // ── 8-connected components of the solid cells ────────────────────────────────────────────────
    private static List<HashSet<(int, int)>> Components(IEnumerable<(int X, int Z)> cells)
    {
        var all = new HashSet<(int, int)>(cells);
        var seen = new HashSet<(int, int)>();
        var comps = new List<HashSet<(int, int)>>();
        foreach (var start in all)
        {
            if (!seen.Add(start)) continue;
            var comp = new HashSet<(int, int)> { start };
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var (x, z) = stack.Pop();
                for (var dx = -1; dx <= 1; dx++)
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var nb = (x + dx, z + dz);
                        if (all.Contains(nb) && seen.Add(nb)) { comp.Add(nb); stack.Push(nb); }
                    }
            }
            comps.Add(comp);
        }
        return comps;
    }

    // ── Prim's MST over components, weighted by nearest-cell distance ─────────────────────────────
    private static List<(int I, int J)> SpanningEdges(List<HashSet<(int, int)>> comps)
    {
        var n = comps.Count;
        var inTree = new bool[n];
        inTree[0] = true;
        var edges = new List<(int, int)>();
        for (var added = 1; added < n; added++)
        {
            double best = double.MaxValue;
            (int i, int j) pick = (-1, -1);
            for (var i = 0; i < n; i++)
            {
                if (!inTree[i]) continue;
                for (var j = 0; j < n; j++)
                {
                    if (inTree[j]) continue;
                    var (ax, az, bx, bz) = Nearest(comps[i], comps[j]);
                    double d = (double)(ax - bx) * (ax - bx) + (double)(az - bz) * (az - bz);
                    if (d < best) { best = d; pick = (i, j); }
                }
            }
            inTree[pick.j] = true;
            edges.Add(pick);
        }
        return edges;
    }

    // Closest pair of cells between two components (brute force; components are small).
    private static (int Ax, int Az, int Bx, int Bz) Nearest(HashSet<(int, int)> a, HashSet<(int, int)> b)
    {
        long best = long.MaxValue;
        (int, int, int, int) pair = default;
        foreach (var (ax, az) in a)
            foreach (var (bx, bz) in b)
            {
                long d = (long)(ax - bx) * (ax - bx) + (long)(az - bz) * (az - bz);
                if (d < best) { best = d; pair = (ax, az, bx, bz); }
            }
        return pair;
    }

    // Rectangle covering both endpoints + the gap between, at least `width` across in each dimension
    // (so a thin gap becomes a walkable-width corridor that overlaps both islands).
    private static Rect Corridor(int ax, int az, int bx, int bz, double width)
    {
        double minX = Math.Min(ax, bx), maxX = Math.Max(ax, bx);
        double minZ = Math.Min(az, bz), maxZ = Math.Max(az, bz);
        if (maxX - minX < width) { var c = (minX + maxX) / 2; minX = c - width / 2; maxX = c + width / 2; }
        if (maxZ - minZ < width) { var c = (minZ + maxZ) / 2; minZ = c - width / 2; maxZ = c + width / 2; }
        return new Rect(minX, minZ, maxX, maxZ);
    }
}
