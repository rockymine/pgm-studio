namespace PgmStudio.Geom;

/// <summary>The ground one walk runs over, at whichever fidelity the caller has.
///
/// <para>A plan states a piece's surface and a build zone's extent; a built world holds columns and a
/// buildability verdict. Those are different inputs and neither can pretend to be the other, so what is
/// shared is not a reader but the <b>answer</b> each one produces per cell: can it be stood on, can it be
/// bridged, how high is it, and is it water. A caller fills this in from what it has.</para>
///
/// <para><see cref="BlocksPerCell"/> is what makes both fidelities answer in the same unit. A plan is drawn
/// in cells of several blocks each and a world in blocks, and every number this walk returns is in
/// <b>blocks</b>.</para></summary>
/// <param name="Ground">Cells a player stands on for nothing.</param>
/// <param name="Bridgeable">Cells a player crosses by placing one block — a build zone, or buildable void.
/// A cell in both sets is ground; the block is only charged where there is nothing to stand on.</param>
/// <param name="Surface">The height under each cell, in blocks. A cell missing from it charges no climb and
/// counts no drop, because an unknown height is not a flat one.</param>
/// <param name="Bounds">The extent every cell falls inside, which the clearance read needs to know where
/// the outside is.</param>
/// <param name="BlocksPerCell">How many blocks one cell is across.</param>
/// <param name="Water">Cells a player swims. Swimming crosses freely and costs no block; it is slower, so
/// it multiplies the distance of the cells it covers. A plan has no water and leaves this null.</param>
public sealed record WalkGround(
    IReadOnlySet<(int X, int Z)> Ground,
    IReadOnlySet<(int X, int Z)> Bridgeable,
    IReadOnlyDictionary<(int X, int Z), int> Surface,
    CellRect Bounds,
    int BlocksPerCell = 1,
    IReadOnlySet<(int X, int Z)>? Water = null)
{
    /// <summary>Every cell a route may enter — ground plus what can be bridged onto.</summary>
    public IReadOnlySet<(int X, int Z)> Passable { get; } = Union(Ground, Bridgeable);

    private static HashSet<(int X, int Z)> Union(
        IReadOnlySet<(int X, int Z)> ground, IReadOnlySet<(int X, int Z)> bridgeable)
    {
        var all = new HashSet<(int X, int Z)>(ground);
        all.UnionWith(bridgeable);
        return all;
    }
}

/// <summary>What one journey costs. Four answers, each in its own unit, and none of them a score:
/// routes are not weighed against each other, so a consumer reads the field its rule is stated in.</summary>
/// <param name="Distance">How far it is, in <b>blocks</b> — the octile measure rounded, so a diagonal run
/// costs the 1.41 a player actually walks rather than the 2 an axis-aligned staircase reports.</param>
/// <param name="Blocks">How many blocks the player must <b>place</b> to make the journey: one per block of
/// climb over the free rise, and one per cell of void bridged. This is the number a kit budget is compared
/// against.</param>
/// <param name="Drops">How many times the route falls further than <see cref="Walk.FreeDrop"/>. A fall is
/// not a wall — every kit carries a water bucket — so it costs no block and refuses no route; it is counted
/// because stopping to place and drink one is a delay the distance does not show.</param>
/// <param name="WorstDrop">The deepest of those falls, in blocks. Zero when there are none.</param>
public readonly record struct WalkCost(int Distance, int Blocks, int Drops, int WorstDrop);

/// <summary>One journey: the cells it passes through, and what it cost.</summary>
public sealed record WalkPath(IReadOnlyList<(int X, int Z)> Cells, WalkCost Cost);

/// <summary>Which question a walk is answering, since a board can offer two different best routes and the
/// difference between them is worth reading.</summary>
public enum WalkAim
{
    /// <summary>The shortest way there, reporting what it costs to build. What a distance rule means.</summary>
    Travel,

    /// <summary>The way there that asks for the fewest placed blocks, reporting how far round it goes. What
    /// a kit budget is answered against.</summary>
    Reach,
}

/// <summary>
/// The one traversal every distance in the studio is measured with.
///
/// <para>It is eight-connected because a player walks diagonally, and it counts a diagonal as the
/// <see cref="Diagonal"/> hundredths of a block it is rather than as one step. It charges a climb in the
/// blocks a player places to get up it, bridges void at one block a cell, counts a fall without pricing it,
/// and slows through water. Nothing in it is weighted: there are no preference coefficients to calibrate,
/// only quantities, each in the unit its own rule is stated in.</para>
///
/// <para>Where two routes tie on the answers above, the one keeping furthest from the void wins. That is
/// not a fifth cost — it decides nothing a rule reads — it is what stops a route hugging a border because
/// the border is the short way round a bend.</para>
/// </summary>
public static class Walk
{
    /// <summary>An orthogonal step, in hundredths of a block.</summary>
    public const int Straight = 100;

    /// <summary>A diagonal step, in hundredths of a block — √2 to two places.</summary>
    public const int Diagonal = 141;

    /// <summary>How far a player steps up for nothing. One block is a walk; two is a jump onto ground you
    /// first have to make, so a rise of Δ costs Δ−1 blocks.</summary>
    public const int FreeRise = 1;

    /// <summary>How far a player falls for nothing. Four is where fall damage starts.</summary>
    public const int FreeDrop = 3;

    /// <summary>What swimming multiplies a distance by. You cross water freely and slowly.</summary>
    public const int WaterSlowdown = 2;

    /// <summary>How much room either side a route wants, in blocks, before it is treated as hugging an
    /// edge. The same number <c>GroundCoverage.CorridorAllowance</c> widens a corridor by, and deliberately:
    /// one quantity read by two consumers.</summary>
    public const int ClearanceWanted = 10;

    private static readonly (int X, int Z)[] Neighbours =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>The cheapest journey from one cell to another under <paramref name="aim"/>, or null when
    /// there is none.</summary>
    public static WalkPath? Between((int X, int Z) from, (int X, int Z) to, WalkGround ground,
        WalkAim aim = WalkAim.Travel)
    {
        if (!ground.Passable.Contains(from) || !ground.Passable.Contains(to)) return null;
        var came = Solve(from, ground, aim, to);
        if (from != to && !came.ContainsKey(to)) return null;

        var cells = new List<(int X, int Z)> { to };
        for (var cell = to; cell != from; cells.Add(cell)) cell = came[cell];
        cells.Reverse();
        return new WalkPath(cells, Measure(cells, ground));
    }

    /// <summary>What every reachable cell costs from <paramref name="from"/>, under <paramref name="aim"/>.
    /// The field a caller wants when it is asking about many targets at once — a kit against every wool, a
    /// coverage read against every waypoint — rather than about one journey.</summary>
    public static Dictionary<(int X, int Z), WalkCost> Field((int X, int Z) from, WalkGround ground,
        WalkAim aim = WalkAim.Travel)
    {
        var costs = new Dictionary<(int X, int Z), WalkCost>();
        if (!ground.Passable.Contains(from)) return costs;

        var came = Solve(from, ground, aim, null);
        costs[from] = new WalkCost(0, StandingBlocks(from, ground), 0, 0);
        foreach (var cell in came.Keys)
        {
            var cells = new List<(int X, int Z)> { cell };
            for (var back = cell; back != from; cells.Add(back)) back = came[back];
            cells.Reverse();
            costs[cell] = Measure(cells, ground);
        }
        return costs;
    }

    /// <summary>What a journey along <paramref name="cells"/> costs. The one place the four answers are
    /// defined: the solve orders routes, and this says what the ordered one is worth, so a route read back
    /// from a picture and a route the solver chose are priced by the same code.</summary>
    public static WalkCost Measure(IReadOnlyList<(int X, int Z)> cells, WalkGround ground)
    {
        if (cells.Count == 0) return default;

        var hundredths = 0;
        var blocks = StandingBlocks(cells[0], ground);
        int drops = 0, worst = 0;

        for (var i = 1; i < cells.Count; i++)
        {
            var (from, to) = (cells[i - 1], cells[i]);
            var step = from.X != to.X && from.Z != to.Z ? Diagonal : Straight;
            if (ground.Water?.Contains(to) == true) step *= WaterSlowdown;
            hundredths += step;

            blocks += StandingBlocks(to, ground);
            if (!ground.Surface.TryGetValue(from, out var was) || !ground.Surface.TryGetValue(to, out var now))
                continue;
            if (now - was > FreeRise) blocks += now - was - FreeRise;
            if (was - now > FreeDrop) { drops++; worst = Math.Max(worst, was - now); }
        }
        return new WalkCost(
            (int)Math.Round(hundredths * ground.BlocksPerCell / 100.0), blocks, drops, worst);
    }

    /// <summary>What standing on a cell costs to place: nothing on ground, one block over void a player
    /// bridges. A cell that is both is ground — the block is charged where there is nothing under it.</summary>
    private static int StandingBlocks((int X, int Z) cell, WalkGround ground)
        => ground.Ground.Contains(cell) ? 0 : ground.Bridgeable.Contains(cell) ? 1 : 0;

    /// <summary>Dijkstra over the eight-neighbourhood, ordered by <paramref name="aim"/>, returning each
    /// reached cell's predecessor. Stops early once <paramref name="target"/> is settled, if one is given.
    ///
    /// <para>The ordering key is lexicographic rather than a weighted sum: the aim's own quantity first, the
    /// other second, and the clearance shortfall last as a tie-break. That is what lets the walk answer both
    /// questions without ever having to say how many blocks a block of walking is worth.</para></summary>
    private static Dictionary<(int X, int Z), (int X, int Z)> Solve((int X, int Z) from, WalkGround ground,
        WalkAim aim, (int X, int Z)? target)
    {
        var comfort = Math.Max(0, ClearanceWanted / Math.Max(1, ground.BlocksPerCell));
        var clearance = comfort == 0
            ? []
            : Cells.Clearance(ground.Passable, ground.Bounds);

        var came = new Dictionary<(int X, int Z), (int X, int Z)>();
        var best = new Dictionary<(int X, int Z), (int Distance, int Blocks, int Deficit)>
        {
            [from] = (0, StandingBlocks(from, ground), Deficit(from)),
        };
        var settled = new HashSet<(int X, int Z)>();
        var queue = new PriorityQueue<(int X, int Z), (int, int, int)>();
        queue.Enqueue(from, Rank(best[from]));

        while (queue.TryDequeue(out var cell, out _))
        {
            if (!settled.Add(cell)) continue;
            if (cell == target) break;
            var here = best[cell];

            foreach (var (dx, dz) in Neighbours)
            {
                var next = (X: cell.X + dx, Z: cell.Z + dz);
                if (settled.Contains(next) || !ground.Passable.Contains(next)) continue;
                // A diagonal squeezes between two cells; where both of those are void the route would be
                // cutting a corner across nothing, which is not a step a player takes.
                if (dx != 0 && dz != 0
                    && !ground.Passable.Contains((cell.X + dx, cell.Z))
                    && !ground.Passable.Contains((cell.X, cell.Z + dz))) continue;

                var step = dx != 0 && dz != 0 ? Diagonal : Straight;
                if (ground.Water?.Contains(next) == true) step *= WaterSlowdown;

                var blocks = here.Blocks + StandingBlocks(next, ground);
                if (ground.Surface.TryGetValue(cell, out var was) && ground.Surface.TryGetValue(next, out var now)
                    && now - was > FreeRise)
                    blocks += now - was - FreeRise;

                var candidate = (here.Distance + step, blocks, here.Deficit + Deficit(next));
                if (best.TryGetValue(next, out var known) && Rank(known).CompareTo(Rank(candidate)) <= 0) continue;
                best[next] = candidate;
                came[next] = cell;
                queue.Enqueue(next, Rank(candidate));
            }
        }
        return came;

        int Deficit((int X, int Z) cell)
            => comfort == 0 ? 0 : Math.Max(0, comfort - clearance.GetValueOrDefault(cell, 0));

        (int, int, int) Rank((int Distance, int Blocks, int Deficit) cost) => aim == WalkAim.Travel
            ? (cost.Distance, cost.Blocks, cost.Deficit)
            : (cost.Blocks, cost.Distance, cost.Deficit);
    }
}
