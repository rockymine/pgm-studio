namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Boundary tracing over a set of integer grid cells. <see cref="TracePerimeter"/> walks the outer edge of one
/// connected landmass clockwise (Moore-neighbour tracing, Jacob's stopping criterion) and numbers each outer
/// boundary cell 0..n-1 around the loop — the arc index a caller reads to wrap something continuously around a
/// footprint, corners included. Interior cells are on no outer boundary and simply do not appear in the result.
/// <see cref="TurnAt"/> then measures how sharply the loop bends at a cell, which is what tells a corner from a
/// curve on a shape that only exists as squares.
/// </summary>
public static class GridBoundary
{
    /// <summary>The span, in perimeter cells either side, that a bend is measured over. It is the scale at which
    /// "corner" is a question at all: under about four the raster's own staircase reads as turning, and above it
    /// no arc tighter than roughly that radius can still count as curved. Five sits between the two, holding a
    /// straight shallow edge at a few degrees while leaving a right angle at ninety. Every caller measuring a
    /// ring reads the same one, so a wall and the plateau beside it answer at the same scale.</summary>
    public const int CornerWindow = 5;

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

    /// <summary>
    /// How sharply the loop bends at <paramref name="index"/>, in degrees from 0 (straight on) to 180 (doubling
    /// back), measured over a window of <paramref name="window"/> cells either side.
    ///
    /// <para><b>The window is the whole point: the turn between neighbouring cells is meaningless on a raster.</b>
    /// A Moore trace steps diagonally as well as orthogonally, so a 45° edge comes out exactly straight — that
    /// is the one slope drawn without a staircase. Every other slope alternates between the two kinds of step,
    /// and each change of kind is a quarter turn: a 1-in-2 edge measures 45° at every single cell, so
    /// cell-to-cell a perfectly straight wall reads as nothing but corners. Comparing the chord arriving at a
    /// cell against the chord leaving it cancels that, because over a window both chords lie along the true
    /// edge whatever mixture of steps drew it.</para>
    ///
    /// <para>The cancelling is exact when the window is a whole number of the slope's repeat (a 1-in-2 edge
    /// reads 0 at a window of 2, 4, 6) and otherwise leaves a residue that falls off with width — a 1-in-5 edge
    /// measures 14° at a window of 4, 9° at 6, 7° at 8. Any window from about 4 up therefore holds the raster's
    /// own noise far below the angle at which a caller would call something a corner.</para>
    ///
    /// <para>What survives the cancelling is real curvature, and it comes out scaled: a circle of radius
    /// <c>R</c> turns about <c>window / R</c> radians, so a wide arc reads as nearly straight while a tight one
    /// reads as sharp — which is right, since a fillet of a few blocks <em>is</em> a corner. A polygon vertex
    /// reads its exterior angle exactly, so a rectangle's corners give 90° and a shallow bend gives little. One
    /// threshold therefore separates a corner from a curve, and the window is the scale that question is asked
    /// at rather than a free parameter: below about 4 the staircase leaks back in, and above it no arc tighter
    /// than roughly that radius can still count as curved.</para>
    ///
    /// <para>The turn does not switch on at a vertex but ramps to it, since a window near one straddles it in
    /// part — reaching the full exterior angle only at the vertex itself and falling to zero a window away. A
    /// caller thresholding this therefore gets both the corners and a band around each, and the threshold sets
    /// how wide that band runs.</para>
    ///
    /// <para>Magnitude only: a loop bends the same amount whether it turns into the shape or out of it, and a
    /// caller marking corners wants both. The window shrinks on a loop too small to hold it, and a loop of
    /// fewer than three cells has no bend to report.</para>
    /// </summary>
    public static double TurnAt(IReadOnlyList<(int X, int Z)> loop, int index, int window)
    {
        var count = loop.Count;
        if (count < 3) return 0;

        // A window wider than half the loop would read the same cell on both sides and measure nothing.
        var reach = Math.Max(1, Math.Min(window, (count - 1) / 2));
        var here = loop[((index % count) + count) % count];
        var behind = loop[((index - reach) % count + count) % count];
        var ahead = loop[((index + reach) % count + count) % count];

        double inX = here.X - behind.X, inZ = here.Z - behind.Z;
        double outX = ahead.X - here.X, outZ = ahead.Z - here.Z;
        if ((inX == 0 && inZ == 0) || (outX == 0 && outZ == 0)) return 0;

        var cross = inX * outZ - inZ * outX;
        var dot = inX * outX + inZ * outZ;
        return Math.Abs(Math.Atan2(cross, dot)) * 180.0 / Math.PI;
    }

    /// <summary>The loop a <see cref="TracePerimeter"/> result describes, in walking order — arc 0 first.
    /// Inverting the map rather than returning it from the trace keeps the trace's own contract unchanged.</summary>
    public static (int X, int Z)[] Loop(IReadOnlyDictionary<(int X, int Z), int> perimeter)
    {
        var loop = new (int X, int Z)[perimeter.Count];
        foreach (var (cell, arc) in perimeter) loop[arc] = cell;
        return loop;
    }

    /// <summary>Every boundary cell mapped to its <see cref="TurnAt"/>, for a caller that wants the whole ring
    /// measured once rather than a cell at a time.</summary>
    public static Dictionary<(int X, int Z), double> Turns(IReadOnlyDictionary<(int X, int Z), int> perimeter,
                                                           int window)
    {
        var loop = Loop(perimeter);
        var turns = new Dictionary<(int X, int Z), double>(loop.Length);
        for (var index = 0; index < loop.Length; index++) turns[loop[index]] = TurnAt(loop, index, window);
        return turns;
    }

    /// <summary>
    /// Which axis the loop <b>runs along</b> where it passes this cell — <see cref="RunAlongX"/> or
    /// <see cref="RunAlongZ"/>, taken from the chord across the same window <see cref="TurnAt"/> measures its
    /// bend over.
    ///
    /// <para>The turn says how sharply the edge bends; this says which way it is going, which is the other
    /// thing a face-aware material needs. A block whose geometry has a direction — a log lying on its side —
    /// has to lie <em>along</em> the wall, because a log laid across it points its cut end at whoever is
    /// looking. On a diagonal the chord is neither axis and the dominant one is taken: a raster diagonal is a
    /// staircase of short runs, and the longer leg is the one the eye reads the wall as following.</para>
    /// </summary>
    public static int RunAt(IReadOnlyList<(int X, int Z)> loop, int index, int window)
    {
        var count = loop.Count;
        if (count < 2) return RunAlongX;

        var reach = Math.Max(1, Math.Min(window, (count - 1) / 2));
        var behind = loop[((index - reach) % count + count) % count];
        var ahead = loop[((index + reach) % count + count) % count];
        return Math.Abs(ahead.X - behind.X) >= Math.Abs(ahead.Z - behind.Z) ? RunAlongX : RunAlongZ;
    }

    /// <summary>The loop runs east–west here, so a block with a direction lies along x.</summary>
    public const int RunAlongX = 1;

    /// <summary>The loop runs north–south here, so a block with a direction lies along z.</summary>
    public const int RunAlongZ = 2;

    /// <summary>The loop turns here, so the cell has an exposed face on <em>both</em> axes — a corner. No
    /// horizontal direction can face outward on two faces at right angles, so a block with a direction of its
    /// own has to stand upright at one. <see cref="RunAt"/> never answers this: on a traced outline a corner is
    /// a staircase of short runs rather than a cell, and only a caller that knows its shape exactly (a
    /// rectangular footprint) can name one.</summary>
    public const int RunsBothWays = 3;

    /// <summary>Every boundary cell mapped to its <see cref="RunAt"/>, the companion to <see cref="Turns"/>.</summary>
    public static Dictionary<(int X, int Z), int> Runs(IReadOnlyDictionary<(int X, int Z), int> perimeter,
                                                       int window)
    {
        var loop = Loop(perimeter);
        var runs = new Dictionary<(int X, int Z), int>(loop.Length);
        for (var index = 0; index < loop.Length; index++) runs[loop[index]] = RunAt(loop, index, window);
        return runs;
    }
}
