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
/// <param name="Ground">Places a player stands on for nothing.</param>
/// <param name="Bridgeable">Places a player crosses by placing one block — a build zone, or buildable void.
/// A place in both sets is ground; the block is only charged where there is nothing to stand on.</param>
/// <param name="Bounds">The extent every place falls inside, which the clearance read needs to know where
/// the outside is.</param>
/// <param name="BlocksPerCell">How many blocks one cell is across.</param>
/// <param name="Water">Cells a player swims. Swimming crosses freely and costs no block; it is slower, so
/// it multiplies the distance of the cells it covers. A plan has no water and leaves this null.</param>
/// <param name="Clear">How many blocks are open above each place before the next solid one. It is what says
/// a storey is enclosed: a player builds up through open air and falls down through it, so a step between two
/// places is only a step where the span between them fits under the lower one's clearance. A place absent
/// from it has open sky, which is every place on a board with nothing stacked over it.</param>
public sealed record WalkGround(
    IReadOnlySet<WalkPlace> Ground,
    IReadOnlySet<WalkPlace> Bridgeable,
    CellRect Bounds,
    int BlocksPerCell = 1,
    IReadOnlySet<(int X, int Z)>? Water = null,
    IReadOnlyDictionary<WalkPlace, int>? Clear = null)
{
    /// <summary>Every place a route may enter — ground plus what can be bridged onto. Computed once at
    /// construction, because the solve asks it per neighbour and a union rebuilt per call would dominate the
    /// walk. It is therefore <b>copied verbatim by <c>with</c></b>: narrowing the ground for a team means
    /// constructing a new <see cref="WalkGround"/>, never copying one and replacing its sets.</summary>
    public IReadOnlySet<WalkPlace> Passable { get; } = Union(Ground, Bridgeable);

    /// <summary>The places of each cell, lowest first. A stacked column holds more than one, and this is what
    /// the step out of a cell walks: a neighbour is a place in an adjacent cell, not the adjacent cell.</summary>
    public IReadOnlyDictionary<(int X, int Z), WalkPlace[]> Stacks { get; } = Stack(Union(Ground, Bridgeable));

    /// <summary>Every cell holding at least one place — the projection a clearance read and a picture take,
    /// and the only place the storey is deliberately discarded.</summary>
    public IReadOnlySet<(int X, int Z)> Footprint { get; } =
        Union(Ground, Bridgeable).Select(place => place.Cell).ToHashSet();

    /// <summary>Where a player walking in at terrain level ends up in a cell: its lowest place, or null where
    /// the cell holds none. What a caller naming a cell and no storey means.</summary>
    public WalkPlace? Stand((int X, int Z) cell) =>
        Stacks.TryGetValue(cell, out var places) && places.Length > 0 ? places[0] : null;

    /// <summary>The place in a cell nearest a stated height — what a marker that knows its own Y means, so a
    /// spawn on a deck resolves to the deck rather than to the floor twenty blocks under it.</summary>
    public WalkPlace? Nearest((int X, int Z) cell, int y) =>
        Stacks.TryGetValue(cell, out var places) && places.Length > 0
            ? places.MinBy(place => Math.Abs(place.Y - y))
            : null;

    /// <summary>How many blocks are open over a place before the next solid one, unbounded where nothing is
    /// stacked over it.</summary>
    public int ClearAbove(WalkPlace place) => Clear?.GetValueOrDefault(place, int.MaxValue) ?? int.MaxValue;

    private static HashSet<WalkPlace> Union(IReadOnlySet<WalkPlace> ground, IReadOnlySet<WalkPlace> bridgeable)
    {
        var all = new HashSet<WalkPlace>(ground);
        all.UnionWith(bridgeable);
        return all;
    }

    private static Dictionary<(int X, int Z), WalkPlace[]> Stack(IEnumerable<WalkPlace> places) =>
        places.GroupBy(place => place.Cell)
              .ToDictionary(group => group.Key, group => group.OrderBy(place => place.Y).ToArray());

    /// <summary>The same ground restricted to <paramref name="keep"/> — what a caller wants when some cells
    /// are out of bounds for this walk in particular: a team an <c>enter</c> rule bars, or a way deliberately
    /// cut to ask whether another one exists. A cell is barred whole: an <c>enter</c> rule keeps a team out of
    /// ground, not out of one storey of it. Constructed rather than copied, because <see cref="Passable"/> is
    /// captured once and <c>with</c> would carry the wider set through.</summary>
    public WalkGround Narrowed(IReadOnlySet<(int X, int Z)> keep) => new(
        new HashSet<WalkPlace>(Ground.Where(place => keep.Contains(place.Cell))),
        new HashSet<WalkPlace>(Bridgeable.Where(place => keep.Contains(place.Cell))),
        Bounds, BlocksPerCell, Water, Clear);

    /// <summary>Ground that is just a set of cells: everything in it is stood on for nothing, nothing is
    /// bridged, and no cell states a height. What a plan is before its relief is solved — a climb it does not
    /// yet know about cannot be charged, and pretending otherwise would price a slope the author has not
    /// drawn. Every cell holds exactly one place, so a plan board is a stack of one the whole way through and
    /// needs no second reader.</summary>
    /// <param name="cells">Every cell a player may stand on.</param>
    /// <param name="blocksPerCell">How many blocks one cell is across, so the distance answers in blocks.</param>
    public static WalkGround Over(IReadOnlySet<(int X, int Z)> cells, int blocksPerCell = 1)
        => new(cells.Select(cell => new WalkPlace(cell.X, cell.Z, 0)).ToHashSet(), new HashSet<WalkPlace>(),
               cells.Count == 0 ? new CellRect(0, 0, 0, 0) : Cells.BoundingBox(cells), blocksPerCell);
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
public sealed record WalkPath(IReadOnlyList<WalkPlace> Places, WalkCost Cost)
{
    /// <summary>The route projected to the board, in order — what a picture draws and what a read that does
    /// not care which storey a step was on asks for.</summary>
    public IReadOnlyList<(int X, int Z)> Cells => [.. Places.Select(place => place.Cell)];
}

/// <summary>Which question a walk is answering, since a board can offer two different best routes and the
/// difference between them is worth reading.</summary>
public enum WalkAim
{
    /// <summary>The shortest way there, reporting what it costs to build. What a distance rule means.</summary>
    Travel,

    /// <summary>The way there that asks for the fewest placed blocks, reporting how far round it goes. What
    /// a kit budget is answered against.</summary>
    Reach,

    /// <summary>The way a player would actually take: among the routes no more than <see cref="Walk.Detour"/>
    /// blocks longer than the shortest, the one keeping furthest off an edge. What a traffic or a coverage
    /// read wants, and what a picture of a journey should draw.
    ///
    /// <para>It is a separate question rather than a stronger tie-break because standoff costs distance. A
    /// neck ten cells across is crossed at a clearance of 1 or, for 2.36 blocks more on a 120-block walk, at
    /// its widest 5; ordered strictly after distance, no tie-break can ever pay that. Ordered before it,
    /// nothing would stop a route wandering. The allowance is the bound, and it is the same number a corridor
    /// is claimed with — one quantity, three consumers.</para></summary>
    Comfort,
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
/// <para>Where two routes tie on the answers above, the one whose worst moment is furthest from the void
/// wins. That is not a fifth cost — it decides nothing a rule reads — it is what stops a route hugging a
/// border because the border is the short way round a bend. A tie-break can only buy standoff that is free,
/// though, and standoff usually costs a little distance: <see cref="WalkAim.Comfort"/> is the aim that pays
/// for it.</para>
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

    /// <summary>How many clear blocks a surface needs over it before a player can stand there. A surface
    /// without them is a floor under something — a house's ground course, a ledge under an overhang — and a
    /// column offers one <see cref="WalkPlace"/> for each surface that has them. A walled cell without this
    /// test reads at the floor inside the wall, and a route walks through the house for nothing.</summary>
    public const int Headroom = 2;

    /// <summary>Where the world stops, in blocks. A walk needs it to know whether a head fits: a column
    /// solid to the top offers a surface with nothing above it, and that is not somewhere to stand.</summary>
    public const int WorldHeight = 256;

    /// <summary>What swimming multiplies a distance by. You cross water freely and slowly.</summary>
    public const int WaterSlowdown = 2;

    /// <summary>How much room either side a route wants, in blocks, before it is treated as hugging an edge.
    /// A lateral standoff, which is a different quantity from <see cref="Detour"/>'s length budget however
    /// alike the two numbers look.</summary>
    public const int ClearanceWanted = 10;

    /// <summary>How far out of their way a player will go, in blocks: a journey may claim any cell on a walk
    /// no more than this much longer than the shortest. Since visiting a cell <c>m</c> blocks off the direct
    /// line and coming back costs about <c>2m</c>, ten blocks of allowance is a lane about ten blocks wide —
    /// and a there-and-back down some spur is charged the same way, which is what keeps a corridor off ground
    /// that leads nowhere.
    ///
    /// <para>An <b>allowance</b>, not a fraction of the distance. A 30% budget on a long walk is a hundred
    /// blocks of slack and admits nearly everything: on the traced maps the share of ground no journey covers
    /// runs 26.1% under the geodesics and <b>0% at both 15% and 30%</b>. A ratio that erases the measure is
    /// not a wider reading of it.</para>
    ///
    /// <para><b>Ten is calibrated, not assumed.</b> It is the value that reproduces the author's own reading
    /// of the one board known to carry dead ground — run 4's `wheal-hazel`, whose eighty-block neutral bar
    /// crosses a twenty-block build zone. Against the author's marks: `works-lo-w` and `west-spur` dead
    /// (100%, 100%), `works-yard` and `moor` about half (50%, 50%), the bar about two thirds (62%, and the
    /// original review measured 60.2% at block resolution). Its rebuild `wheal-hazel-v2` reads <b>0%</b>. A
    /// tolerance for going out of one's way is not the same quantity as the width of a lane a player spreads
    /// across, and it is the smaller of the two.</para></summary>
    public const int Detour = 10;

    private static readonly (int X, int Z)[] Neighbours =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>The cheapest journey from one cell to another under <paramref name="aim"/>, or null when
    /// there is none.</summary>
    public static WalkPath? Between(WalkPlace from, WalkPlace to, WalkGround ground,
        WalkAim aim = WalkAim.Travel)
    {
        if (!ground.Passable.Contains(from) || !ground.Passable.Contains(to)) return null;
        if (aim == WalkAim.Comfort) return Comfortable(from, to, ground);

        var came = Solve(from, ground, aim, to);
        if (from != to && !came.ContainsKey(to)) return null;

        var route = new List<WalkPlace> { to };
        for (var place = to; place != from; route.Add(place)) place = came[place];
        route.Reverse();
        return new WalkPath(route, Measure(route, ground));
    }

    /// <summary>The least-exposed route inside the corridor: the ribbon of cells a walk within
    /// <see cref="Detour"/> of the shortest can reach — solved again with the standoff first. The two ends
    /// are always in the ribbon, so a journey that only just connects still answers.</summary>
    private static WalkPath? Comfortable(WalkPlace from, WalkPlace to, WalkGround ground)
    {
        var outward = Field(from, ground);
        if (!outward.TryGetValue(to, out var direct)) return null;
        var homeward = Field(to, ground);

        var budget = direct.Distance + Detour;
        var ribbon = new HashSet<WalkPlace> { from, to };
        foreach (var (cell, cost) in outward)
            if (homeward.TryGetValue(cell, out var back) && cost.Distance + back.Distance <= budget)
                ribbon.Add(cell);

        var inside = ground with { Ground = Narrow(ground.Ground, ribbon), Bridgeable = Narrow(ground.Bridgeable, ribbon) };
        var came = Solve(from, inside, WalkAim.Comfort, to);
        if (from != to && !came.ContainsKey(to)) return null;

        var route = new List<WalkPlace> { to };
        for (var place = to; place != from; route.Add(place)) place = came[place];
        route.Reverse();
        return new WalkPath(route, Measure(route, ground));
    }

    private static HashSet<WalkPlace> Narrow(IReadOnlySet<WalkPlace> set, HashSet<WalkPlace> ribbon)
        => [.. set.Where(ribbon.Contains)];

    /// <summary>What every reachable cell costs from <paramref name="from"/>, under <paramref name="aim"/>.
    /// The field a caller wants when it is asking about many targets at once — a kit against every wool, a
    /// coverage read against every waypoint — rather than about one journey.</summary>
    public static Dictionary<WalkPlace, WalkCost> Field(WalkPlace from, WalkGround ground,
        WalkAim aim = WalkAim.Travel)
    {
        if (aim == WalkAim.Comfort)
            throw new ArgumentOutOfRangeException(nameof(aim),
                "a comfort route is bounded by how far the journey itself is, so it is answered between two "
                + "cells rather than as a field — ask Between, or field Travel and draw the comfort route on it");

        var costs = new Dictionary<WalkPlace, WalkCost>();
        if (!ground.Passable.Contains(from)) return costs;

        var came = Solve(from, ground, aim, null);
        costs[from] = new WalkCost(0, StandingBlocks(from, ground), 0, 0);
        foreach (var place in came.Keys)
        {
            var route = new List<WalkPlace> { place };
            for (var back = place; back != from; route.Add(back)) back = came[back];
            route.Reverse();
            costs[place] = Measure(route, ground);
        }
        return costs;
    }

    /// <summary>Every cell a player could stand on while walking a route no more than <paramref name="slack"/>
    /// blocks longer than the shortest — the <b>ribbon</b>, not one geodesic fattened. A cell is in it when
    /// <c>d(from,cell) + d(cell,to) &lt;= d(from,to) + slack</c>, both halves measured by this walk, so the
    /// corridor and the route it surrounds are stated in one unit.
    ///
    /// <para>The difference from a dilated path is not cosmetic. One geodesic must commit to one side of a
    /// hole, so the other side reads unused however many players take it; the ribbon carries both sides, which
    /// is the whole point of a board that offers a way round. Empty where the two ends do not connect.</para>
    /// </summary>
    public static HashSet<WalkPlace> Corridor(WalkPlace from, WalkPlace to, WalkGround ground,
        int slack)
    {
        var ribbon = new HashSet<WalkPlace>();
        var outward = Field(from, ground);
        if (!outward.TryGetValue(to, out var direct)) return ribbon;
        var homeward = Field(to, ground);

        var budget = direct.Distance + Math.Max(0, slack);
        foreach (var (place, near) in outward)
            if (homeward.TryGetValue(place, out var far) && near.Distance + far.Distance <= budget)
                ribbon.Add(place);
        return ribbon;
    }

    /// <summary>The same ribbon with the slack stated as a share of the journey rather than in blocks — what
    /// a reader wants when the question is how far out of their way a player goes <em>relative to</em> a walk,
    /// rather than the absolute tolerance a corridor is claimed with.</summary>
    public static HashSet<WalkPlace> Corridor(WalkPlace from, WalkPlace to, WalkGround ground,
        double slack)
    {
        var outward = Field(from, ground);
        return outward.TryGetValue(to, out var direct)
            ? Corridor(from, to, ground, (int)Math.Round(direct.Distance * Math.Max(0, slack)))
            : [];
    }

    /// <summary>What a journey along <paramref name="route"/> costs. The one place the four answers are
    /// defined: the solve orders routes, and this says what the ordered one is worth, so a route read back
    /// from a picture and a route the solver chose are priced by the same code.</summary>
    public static WalkCost Measure(IReadOnlyList<WalkPlace> route, WalkGround ground)
    {
        if (route.Count == 0) return default;

        var hundredths = 0;
        var blocks = StandingBlocks(route[0], ground);
        int drops = 0, worst = 0;

        for (var i = 1; i < route.Count; i++)
        {
            var (from, to) = (route[i - 1], route[i]);
            var step = from.X != to.X && from.Z != to.Z ? Diagonal : Straight;
            if (ground.Water?.Contains(to.Cell) == true) step *= WaterSlowdown;
            hundredths += step;

            blocks += StandingBlocks(to, ground);
            // The rise is the two places' own difference — a place is where the feet are, so nothing has to
            // be looked up to price a step between two of them.
            if (to.Y - from.Y > FreeRise) blocks += to.Y - from.Y - FreeRise;
            if (from.Y - to.Y > FreeDrop) { drops++; worst = Math.Max(worst, from.Y - to.Y); }
        }
        return new WalkCost(
            (int)Math.Round(hundredths * ground.BlocksPerCell / 100.0), blocks, drops, worst);
    }

    /// <summary>What standing in a place costs to place: nothing on ground, one block over void a player
    /// bridges. A place that is both is ground — the block is charged where there is nothing under it.</summary>
    private static int StandingBlocks(WalkPlace place, WalkGround ground)
        => ground.Ground.Contains(place) ? 0 : ground.Bridgeable.Contains(place) ? 1 : 0;

    /// <summary>Dijkstra over the eight-neighbourhood, ordered by <paramref name="aim"/>, returning each
    /// reached cell's predecessor. Stops early once <paramref name="target"/> is settled, if one is given.
    ///
    /// <para>The ordering key is lexicographic rather than a weighted sum: the aim's own quantity first and
    /// the others after it, which is what lets the walk answer every question without ever having to say how
    /// many blocks a block of walking is worth. The clearance term is the route's <b>worst</b> shortfall
    /// rather than its total, so it measures how exposed a journey gets rather than how long it is — a sum
    /// charges a longer route for its own length and would rank a safe detour below the edge it avoids.
    /// </para></summary>
    private static Dictionary<WalkPlace, WalkPlace> Solve(WalkPlace from, WalkGround ground,
        WalkAim aim, WalkPlace? target)
    {
        var comfort = Math.Max(0, ClearanceWanted / Math.Max(1, ground.BlocksPerCell));
        var clearance = comfort == 0
            ? []
            : Cells.Clearance(ground.Footprint, ground.Bounds);

        var came = new Dictionary<WalkPlace, WalkPlace>();
        var best = new Dictionary<WalkPlace, (int Distance, int Blocks, int Deficit)>
        {
            [from] = (0, StandingBlocks(from, ground), Deficit(from)),
        };
        var settled = new HashSet<WalkPlace>();
        var queue = new PriorityQueue<WalkPlace, (int, int, int)>();
        queue.Enqueue(from, Rank(best[from]));

        while (queue.TryDequeue(out var place, out _))
        {
            if (!settled.Add(place)) continue;
            if (target is { } goal && place == goal) break;
            var here = best[place];

            foreach (var (dx, dz) in Neighbours)
            {
                var side = (X: place.X + dx, Z: place.Z + dz);
                if (!ground.Stacks.TryGetValue(side, out var stack)) continue;
                // A diagonal squeezes between two cells; where both of those are void the route would be
                // cutting a corner across nothing, which is not a step a player takes.
                if (dx != 0 && dz != 0
                    && !ground.Footprint.Contains((place.X + dx, place.Z))
                    && !ground.Footprint.Contains((place.X, place.Z + dz))) continue;

                foreach (var next in stack)
                {
                    if (settled.Contains(next)) continue;
                    if (!Steps(place, next)) continue;

                    var step = dx != 0 && dz != 0 ? Diagonal : Straight;
                    if (ground.Water?.Contains(side) == true) step *= WaterSlowdown;

                    var blocks = here.Blocks + StandingBlocks(next, ground);
                    if (next.Y - place.Y > FreeRise) blocks += next.Y - place.Y - FreeRise;

                    var candidate = (here.Distance + step, blocks, Math.Max(here.Deficit, Deficit(next)));
                    if (best.TryGetValue(next, out var known) && Rank(known).CompareTo(Rank(candidate)) <= 0) continue;
                    best[next] = candidate;
                    came[next] = place;
                    queue.Enqueue(next, Rank(candidate));
                }
            }
        }
        return came;

        // Whether one place is a step from another. A player builds up through open air and falls down
        // through it, so the span between two places has to fit under the clearance of the lower one: a
        // gallery roofed sixteen blocks up is not a step from a deck twenty-six blocks over it, and the same
        // gallery where the roof is cut away is. On a board with nothing stacked over it every place has open
        // sky, so this never refuses a step and the answer is the one a flat board always had.
        bool Steps(WalkPlace a, WalkPlace b)
        {
            var span = Math.Abs(a.Y - b.Y);
            return span <= ground.ClearAbove(a.Y <= b.Y ? a : b);
        }

        int Deficit(WalkPlace place)
            => comfort == 0 ? 0 : Math.Max(0, comfort - clearance.GetValueOrDefault(place.Cell, 0));

        (int, int, int) Rank((int Distance, int Blocks, int Deficit) cost) => aim switch
        {
            WalkAim.Reach => (cost.Blocks, cost.Distance, cost.Deficit),
            WalkAim.Comfort => (cost.Deficit, cost.Distance, cost.Blocks),
            _ => (cost.Distance, cost.Blocks, cost.Deficit),
        };
    }
}
