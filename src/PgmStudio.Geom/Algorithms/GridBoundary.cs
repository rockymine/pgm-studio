namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Boundary tracing over a set of integer grid cells. <see cref="TracePerimeter"/> walks the outer edge of one
/// connected landmass clockwise (Moore-neighbour tracing, Jacob's stopping criterion) and numbers each outer
/// boundary cell 0..n-1 around the loop — the arc index a caller reads to wrap something continuously around a
/// footprint, corners included. Interior cells are on no outer boundary and simply do not appear in the result.
/// </summary>
public static class GridBoundary
{
    // Clockwise 8-neighbour offsets starting due north (z−1), so a "turn right" is +1 around the ring.
    private static readonly (int dx, int dz)[] Cw =
        [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    /// <summary>
    /// Clockwise Moore-neighbour boundary trace of one landmass, returning each outer boundary cell mapped to
    /// its 0-based arc index. Numbering starts at the top-left boundary cell (entered from the west) and runs
    /// clockwise; a cell revisited (a thin neck) keeps its first index. An empty or single-cell input yields at
    /// most that one cell.
    /// </summary>
    public static Dictionary<(int X, int Z), int> TracePerimeter(IEnumerable<(int X, int Z)> landmass)
    {
        var cells = landmass as HashSet<(int, int)> ?? new HashSet<(int, int)>(landmass);
        var arc = new Dictionary<(int X, int Z), int>();
        if (cells.Count == 0) return arc;

        bool Solid(int x, int z) => cells.Contains((x, z));
        static int Idx(int dx, int dz) { for (var i = 0; i < 8; i++) if (Cw[i].dx == dx && Cw[i].dz == dz) return i; return -1; }

        var start = cells.OrderBy(c => c.Item2).ThenBy(c => c.Item1).First();   // on the boundary, entered from the west
        var p = start;
        int backIdx = Idx(-1, 0), s = 0;
        // The walk is fully determined by its state — the cell it stands on plus the direction it came from —
        // so the first repeated state is the point where the loop has closed and nothing new can be found.
        // That is the termination condition: it is reached in at most 8 steps per cell, and it needs no
        // agreement about which state counts as "back at the beginning". (Jacob's criterion — stop on
        // re-entering the start cell from the original direction — was the earlier rule here, and on a plain
        // filled square it never fired at all: every trace ran to a millionth-iteration backstop instead, a
        // flat ~110 ms per landmass whatever its size, while still returning the correct ring.)
        var walked = new HashSet<((int, int) Cell, int Back)>();
        if (!arc.ContainsKey(start)) arc[start] = s++;
        while (walked.Add((p, backIdx)))
        {
            int found = -1;
            for (var k = 1; k <= 8; k++)
            {
                int idx = (backIdx + k) % 8;
                if (Solid(p.Item1 + Cw[idx].dx, p.Item2 + Cw[idx].dz)) { found = idx; break; }
            }
            if (found < 0) break;                                       // isolated cell
            var c = (p.Item1 + Cw[found].dx, p.Item2 + Cw[found].dz);
            int prevIdx = (found - 1 + 8) % 8;                          // last background checked = new backtrack
            int newBack = Idx(p.Item1 + Cw[prevIdx].dx - c.Item1, p.Item2 + Cw[prevIdx].dz - c.Item2);
            if (!arc.ContainsKey(c)) arc[c] = s++;
            p = c; backIdx = newBack;
        }
        return arc;
    }
}
