namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Connected components (flood fill) over a set of integer grid cells — the one place the 4- and
/// 8-neighbourhoods and the BFS that grows a component live. A component grows across a neighbour when it is in
/// the set and an optional <c>canJoin</c> predicate allows the link; passing no predicate joins
/// every in-set neighbour (a plain landmass), while a predicate joins conditionally (equal height, a walkable
/// step). Seeds are taken in raster order (x then z) and components are returned in discovery order, so ids
/// derived from the result are reproducible whatever order the caller supplied the cells in.
/// </summary>
public static class GridComponents
{
    /// <summary>The four orthogonal neighbours.</summary>
    public static readonly (int dx, int dz)[] N4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>The eight orthogonal-plus-diagonal neighbours.</summary>
    public static readonly (int dx, int dz)[] N8 =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>
    /// The cells partitioned into connected components. <paramref name="connectivity"/> is 4 (orthogonal) or 8
    /// (orthogonal + diagonal). Two in-set neighbours join when <paramref name="canJoin"/> is null or returns
    /// true for the (current, neighbour) pair — so a height/step-aware caller passes the predicate and a plain
    /// landmass omits it. Components come back in the order their seeds are first reached (raster: x then z).
    /// </summary>
    public static List<List<(int X, int Z)>> Label(
        IEnumerable<(int X, int Z)> cells,
        int connectivity = 4,
        Func<(int X, int Z), (int X, int Z), bool>? canJoin = null)
    {
        var deltas = connectivity == 8 ? N8 : N4;
        var remaining = new HashSet<(int, int)>(cells);
        var seeds = remaining.OrderBy(c => c.Item1).ThenBy(c => c.Item2).ToList();
        var components = new List<List<(int X, int Z)>>();

        foreach (var seed in seeds)
        {
            if (!remaining.Remove(seed)) continue;
            var comp = new List<(int X, int Z)>();
            var queue = new Queue<(int X, int Z)>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                comp.Add(cell);
                foreach (var (dx, dz) in deltas)
                {
                    var nb = (cell.X + dx, cell.Z + dz);
                    // Unconditional joining claims the neighbour with the lookup that tests it — Remove
                    // reports whether it was there. With a predicate the membership test has to come first,
                    // because a neighbour the predicate rejects must stay available to another component.
                    if (canJoin is null)
                    {
                        if (remaining.Remove(nb)) queue.Enqueue(nb);
                    }
                    else if (remaining.Contains(nb) && canJoin(cell, nb))
                    {
                        remaining.Remove(nb);
                        queue.Enqueue(nb);
                    }
                }
            }
            components.Add(comp);
        }
        return components;
    }
}
