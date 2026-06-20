namespace PgmStudio.Geom;

/// <summary>
/// Read a 1-cell-wide skeleton (from <see cref="Algorithms.ZhangSuen.Thin"/>) into its centerline graph —
/// the "twig with branches" structure of a map island. Tracing yields the lane graph: endpoints (degree-1 =
/// lane tips / dead-ends), junctions (degree ≥ 3 = where lanes branch), and the edge polylines between them;
/// pruning drops spur noise. Pure raster math (z increases downward, "north" = z−1).
/// </summary>
public static class Skeleton
{
    public sealed record Graph(
        List<(int X, int Z)> Endpoints,
        List<(int X, int Z)> Junctions,
        List<List<(int X, int Z)>> Edges);

    /// <summary>Trace a 1-cell skeleton into endpoints, junctions, and the edge polylines between them.</summary>
    public static Graph Trace(HashSet<(int X, int Z)> skel)
    {
        int Deg((int x, int z) p) => Neighbours(p).Count(n => skel.Contains(n));
        var endpoints = skel.Where(p => Deg(p) == 1).ToList();
        var junctions = skel.Where(p => Deg(p) >= 3).ToList();
        var nodes = new HashSet<(int, int)>(endpoints);
        nodes.UnionWith(junctions);

        var edges = new List<List<(int, int)>>();
        var usedDir = new HashSet<((int, int), (int, int))>();
        foreach (var node in nodes)
            foreach (var nb in Neighbours(node))
            {
                if (!skel.Contains(nb) || usedDir.Contains((node, nb))) continue;
                var path = new List<(int, int)> { node };
                var prev = node;
                var cur = nb;
                var guard = 0;
                while (guard++ < skel.Count + 2)
                {
                    path.Add(cur);
                    if (nodes.Contains(cur)) break;
                    var next = Neighbours(cur).FirstOrDefault(n => n != prev && skel.Contains(n), (int.MinValue, 0));
                    if (next.Item1 == int.MinValue) break;
                    prev = cur;
                    cur = next;
                }
                usedDir.Add((node, nb));
                if (path.Count >= 2) usedDir.Add((path[^1], path[^2]));
                edges.Add(path);
            }
        return new Graph(endpoints, junctions, edges);
    }

    /// <summary>Prune the lane graph: drop leaf edges (a branch ending in a dead-end) shorter than
    /// <paramref name="minBranchLen"/>, re-tracing after each pass so dissolved junctions merge their
    /// through-lanes. Turns the spur-noisy raw skeleton of a wide body into its few real lanes.</summary>
    public static Graph Prune(Graph g, double minBranchLen) => Prune(g, minBranchLen, new HashSet<(int, int)>(), 0);

    /// <summary>Anchor-aware prune: as <see cref="Prune(Graph,double)"/>, but a leaf branch is kept
    /// regardless of length when its dead-end is within <paramref name="anchorRadius"/> of an anchor cell.
    /// Anchors are the map's fixed points — spawn/wool/monument positions and the island↔build-region
    /// contact cells — so pruning never eats the lanes that lead to an objective or a bridge connector.</summary>
    public static Graph Prune(Graph g, double minBranchLen, IReadOnlySet<(int X, int Z)> anchorCells, double anchorRadius)
    {
        var r = (int)Math.Ceiling(anchorRadius);
        var r2 = anchorRadius * anchorRadius;
        bool Anchored((int x, int z) p)
        {
            for (var dx = -r; dx <= r; dx++)
                for (var dz = -r; dz <= r; dz++)
                    if (dx * dx + dz * dz <= r2 && anchorCells.Contains((p.x + dx, p.z + dz))) return true;
            return false;
        }

        var edges = g.Edges;
        while (true)
        {
            var degree = new Dictionary<(int, int), int>();
            foreach (var e in edges)
                if (e.Count >= 2) { Bump(degree, e[0]); Bump(degree, e[^1]); }

            var kept = edges.Where(e =>
            {
                if (e.Count < 2) return false;
                var leafA = degree.GetValueOrDefault(e[0]) == 1;
                var leafB = degree.GetValueOrDefault(e[^1]) == 1;
                if (!leafA && !leafB) return true;                  // through-lane, keep
                if (Length(e) >= minBranchLen) return true;          // long branch, keep
                var leaf = leafA ? e[0] : e[^1];
                return Anchored(leaf);                               // short branch kept only if it reaches an objective
            }).ToList();
            if (kept.Count == edges.Count) return g;

            var cells = new HashSet<(int, int)>();
            foreach (var e in kept) foreach (var p in e) cells.Add(p);
            g = Trace(cells);
            edges = g.Edges;
        }
    }

    private static void Bump(Dictionary<(int, int), int> d, (int, int) k) => d[k] = d.GetValueOrDefault(k) + 1;

    private static double Length(List<(int, int)> e)
    {
        double len = 0;
        for (var i = 1; i < e.Count; i++)
        {
            double dx = e[i].Item1 - e[i - 1].Item1, dz = e[i].Item2 - e[i - 1].Item2;
            len += Math.Sqrt(dx * dx + dz * dz);
        }
        return len;
    }

    private static IEnumerable<(int, int)> Neighbours((int x, int z) p)
    {
        for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0) yield return (p.x + dx, p.z + dz);
    }
}
