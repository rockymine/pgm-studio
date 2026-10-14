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
/// <param name="Lifts">The ladders, vines and water columns of each cell (<see cref="WalkLift"/>): a step may
/// leave a place from anywhere on the lift it stands on and arrive anywhere on the lift of the place it
/// reaches, for the blocks climbed. A plan has none and leaves this null.</param>
public sealed record WalkGround(
    IReadOnlySet<WalkPlace> Ground,
    IReadOnlySet<WalkPlace> Bridgeable,
    CellRect Bounds,
    int BlocksPerCell = 1,
    IReadOnlySet<(int X, int Z)>? Water = null,
    IReadOnlyDictionary<WalkPlace, int>? Clear = null,
    IReadOnlyDictionary<(int X, int Z), WalkLift[]>? Lifts = null)
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

    /// <summary>Whether this walk takes running jumps (<see cref="Jump"/>): true on ground read from a world's
    /// own blocks, where a cell with no place is a gap; false on a plan's cells, where it may be a wall.</summary>
    public bool Jumps { get; init; }

    /// <summary>The ground a world has, cell by cell, before any walk narrowed it — what a jump asks whether a
    /// cell is a gap of. A cell an <c>enter</c> rule bars a team from is still ground underfoot, and a build
    /// zone over the void is still a gap.</summary>
    public IReadOnlyDictionary<(int X, int Z), WalkPlace[]> Floors { get; init; } = Stack(Ground);

    /// <summary>Every solid span of each cell, as <c>(floor, top)</c> inclusive, where the ground was read from
    /// a world's blocks — what a jump's line has to pass clear of, a crown over a gap included. Null on ground
    /// stated without its blocks, where a jump reads the places instead.</summary>
    public IReadOnlyDictionary<(int X, int Z), (int Floor, int Top)[]>? Solid { get; init; }

    private HashSet<WalkPlace>? _rims;

    /// <summary>The places a jump is taken from: those beside a cell with nowhere to stand at their own height
    /// or one under it, so a jump is only ever tried over a gap. Worked out on first ask, and carried by
    /// <c>with</c> like the sets above.</summary>
    public IReadOnlySet<WalkPlace> Rims => _rims ??= [.. Passable.Where(place =>
        Around.Any(side => !HasFloorFrom((place.X + side.X, place.Z + side.Z), place.Y - 1)))];

    private static readonly (int X, int Z)[] Around =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>Whether a cell holds ground at <paramref name="y"/> or above (<see cref="Floors"/>) — ground a
    /// player at that height walks onto rather than jumps across.</summary>
    public bool HasFloorFrom((int X, int Z) cell, int y)
        => Floors.TryGetValue(cell, out var places) && places.Any(place => place.Y >= y);

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

    /// <summary>Whether one place is a step from another. A player builds up through open air and falls down
    /// through it, so the span between two places has to fit under the clearance of the lower one: a gallery
    /// roofed sixteen blocks up is not a step from a deck twenty-six blocks over it, and the same gallery
    /// where the roof is cut away is. On a board with nothing stacked over it every place has open sky, so
    /// this never refuses a step.</summary>
    public bool Steps(WalkPlace from, WalkPlace to) => Fits(from.Y, to.Y, from, to);

    /// <summary>Whether a move between heights <paramref name="leave"/> and <paramref name="arrive"/> fits
    /// under the clearance of the lower of the two places it joins — <see cref="Steps"/> for a step taken off
    /// or onto a lift, where the heights moved between are not the places' own.</summary>
    public bool Fits(int leave, int arrive, WalkPlace from, WalkPlace to)
        => Math.Abs(leave - arrive) <= ClearAbove(from.Y <= to.Y ? from : to);

    /// <summary>The lift a place stands on, or null where its cell has none that holds it.</summary>
    public WalkLift? LiftAt(WalkPlace place)
    {
        if (Lifts is null || !Lifts.TryGetValue(place.Cell, out var lifts)) return null;
        foreach (var lift in lifts)
            if (lift.Holds(place.Y)) return lift;
        return null;
    }

    private static HashSet<WalkPlace> Union(IReadOnlySet<WalkPlace> ground, IReadOnlySet<WalkPlace> bridgeable)
    {
        var all = new HashSet<WalkPlace>(ground);
        all.UnionWith(bridgeable);
        return all;
    }

    private static Dictionary<(int X, int Z), WalkPlace[]> Stack(IEnumerable<WalkPlace> places) =>
        places.GroupBy(place => place.Cell)
              .ToDictionary(group => group.Key, group => group.OrderBy(place => place.Y).ToArray());

    /// <summary>The same ground restricted to the cells in <paramref name="keep"/>, every storey of each — a
    /// way deliberately cut to ask whether another one exists.</summary>
    public WalkGround Narrowed(IReadOnlySet<(int X, int Z)> keep) => Narrowed(place => keep.Contains(place.Cell));

    /// <summary>The same ground restricted to the places <paramref name="keep"/> admits — what a caller wants
    /// when some are out of bounds for this walk in particular, such as the storeys an <c>enter</c> rule bars
    /// a team from. Constructed rather than copied, because <see cref="Passable"/> is captured once and
    /// <c>with</c> would carry the wider set through.</summary>
    public WalkGround Narrowed(Func<WalkPlace, bool> keep) => new(
        new HashSet<WalkPlace>(Ground.Where(keep)),
        new HashSet<WalkPlace>(Bridgeable.Where(keep)),
        Bounds, BlocksPerCell, Water, Clear, Lifts) { Jumps = Jumps, Floors = Floors, Solid = Solid };

    /// <summary>The board a set of solid spans makes: every place a player can stand in them, with the room
    /// over each. Nothing is bridgeable — a caller that knows where a block may be placed adds that itself —
    /// so this is the ground as the world's own geometry states it.</summary>
    /// <param name="columns">Every solid span, as <c>(x, z, floor, top)</c>. A cell may appear more than
    /// once, and a cell whose spans offer nowhere to stand contributes no place.</param>
    /// <param name="props">Every span a prop stamped, in the same shape: solid over the ground it stands in,
    /// and never a place of its own (<see cref="Walk.Standing"/>).</param>
    /// <param name="lifts">Every ladder, vine and water column, by cell.</param>
    public static WalkGround OfSpans(
        IEnumerable<(int X, int Z, int YFloor, int YTop)> columns,
        IEnumerable<(int X, int Z, int YFloor, int YTop)> props,
        IEnumerable<(int X, int Z, WalkLift Lift)>? lifts = null)
    {
        var stacks = new Dictionary<(int X, int Z), List<(int Floor, int Top)>>();
        foreach (var (x, z, floor, top) in columns)
        {
            if (!stacks.TryGetValue((x, z), out var spans)) stacks[(x, z)] = spans = [];
            spans.Add((floor, top));
        }
        var propStacks = new Dictionary<(int X, int Z), List<(int Floor, int Top)>>();
        foreach (var (x, z, floor, top) in props)
        {
            if (!propStacks.TryGetValue((x, z), out var spans)) propStacks[(x, z)] = spans = [];
            spans.Add((floor, top));
        }

        var ground = new HashSet<WalkPlace>();
        var clear = new Dictionary<WalkPlace, int>();
        foreach (var (cell, spans) in stacks)
            foreach (var (top, room) in Walk.Standing(spans, propStacks.GetValueOrDefault(cell) ?? []))
            {
                var place = new WalkPlace(cell.X, cell.Z, top);
                ground.Add(place);
                if (room != int.MaxValue) clear[place] = room;
            }

        var footprint = ground.Select(place => place.Cell).ToHashSet();
        return new WalkGround(ground, new HashSet<WalkPlace>(),
            footprint.Count == 0 ? new CellRect(0, 0, 0, 0) : Cells.BoundingBox(footprint), 1, null, clear,
            ByCell(lifts))
        {
            Jumps = true,
            Solid = stacks.Keys.Union(propStacks.Keys).ToDictionary(cell => cell, cell =>
                (stacks.GetValueOrDefault(cell) ?? []).Concat(propStacks.GetValueOrDefault(cell) ?? []).ToArray()),
        };
    }

    /// <summary>Lifts gathered by cell, or null where there are none.</summary>
    public static IReadOnlyDictionary<(int X, int Z), WalkLift[]>? ByCell(IEnumerable<(int X, int Z, WalkLift Lift)>? lifts)
    {
        var byCell = (lifts ?? []).GroupBy(entry => (entry.X, entry.Z))
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Lift).ToArray());
        return byCell.Count == 0 ? null : byCell;
    }

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
/// slows through water, climbs a ladder, a vine or a water column (<see cref="WalkLift"/>) for the blocks
/// climbed, and takes a running jump across a gap the sprint jump clears (<see cref="Jump"/>). Nothing in it is weighted: there are no preference coefficients to calibrate,
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

    /// <summary>The tallest step crossed by placing one block and climbing it: a scramble. Above it a step is
    /// a face, whatever the walk charges to build up it. The game's number, not a tuning choice, and the one
    /// every read that classes a step reads — the relief read's tiers, a transect's words, a slope grid's
    /// characters — so a step is the same kind of step wherever it is counted.</summary>
    public const int ScrambleStep = 2;

    /// <summary>The word for a signed step between two neighbouring places: <c>walk</c> up to
    /// <see cref="FreeRise"/> either way, <c>scramble</c> up to <see cref="ScrambleStep"/>, <c>barrier</c>
    /// above it, and <c>drop</c> for a fall past <see cref="FreeDrop"/>, where damage starts.</summary>
    public static string StepWord(int delta) =>
        delta > ScrambleStep ? "barrier"
        : delta > FreeRise ? "scramble"
        : delta < -FreeDrop ? "drop"
        : "walk";

    /// <summary>The tallest rise that still reads as ground rather than as a wall: more than five blocks is a
    /// face a player goes round rather than up (author). A climb inside the bound is a slope, a bank or a
    /// flight of steps; past it the two sides are separate places however much clearance stands over each.
    ///
    /// <para>Distinct from <see cref="FreeRise"/>, which is what a step costs — one block is free and the rest
    /// is blocks placed. This is what a step <b>is</b>, and it is the bound a picture of where a player can
    /// walk is drawn with: a house is not walked over.</para></summary>
    public const int WallRise = 5;


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

    /// <summary>Every place a player can stand over one column's solid spans, with how many blocks are open
    /// over each before the next solid one. A surface qualifies when <see cref="Headroom"/> blocks above it
    /// are clear and the world has room for a head there; a column offering none answers empty, and a column
    /// with two answers twice.
    ///
    /// <para>The one definition of where a player stands, so a world the studio built and the same world
    /// scanned back cannot disagree about it. Answers lowest first.</para>
    ///
    /// <para><b>A prop's volume is out of the walk.</b> A tree's trunk and crown and a boulder are solid —
    /// they take a place's headroom and roof it like any block — but their tops are never a place to stand, so
    /// a crown hanging over the void leaves the column void and a crown over a field leaves the field.</para>
    /// </summary>
    /// <param name="spans">The column's solid runs, as <c>(floor, top)</c> inclusive, in any order.</param>
    /// <param name="props">The column's runs a prop stamped, as <c>(floor, top)</c> inclusive: solid, and never
    /// stood on.</param>
    public static IEnumerable<(int Top, int Clear)> Standing(
        IReadOnlyCollection<(int Floor, int Top)> spans, IReadOnlyCollection<(int Floor, int Top)> props)
    {
        var solid = spans.Concat(props).ToList();
        foreach (var (_, top) in spans.OrderBy(span => span.Top))
        {
            var stand = top + 1;
            if (stand + Headroom > WorldHeight) continue;
            if (solid.Any(span => span.Floor <= stand + Headroom - 1 && stand <= span.Top)) continue;

            // The lowest solid floor at or above this surface is its ceiling; nothing above it is sky.
            var ceiling = solid.Select(span => span.Floor)
                               .Where(floor => floor >= stand).DefaultIfEmpty(int.MaxValue).Min();
            yield return (stand, ceiling == int.MaxValue ? int.MaxValue : Math.Max(0, ceiling - stand));
        }
    }

    private static readonly (int X, int Z)[] Sides =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>One step and what it costs: the distance in hundredths of a block, and the height gained and
    /// lost crossing from one cell to the next. <see cref="Rise"/> and <see cref="Fall"/> are measured
    /// between the heights the player crosses at, which are the two places' own unless a lift carried them —
    /// the blocks climbed on a lift are distance, not rise.</summary>
    private readonly record struct WalkStep(WalkPlace Next, bool Diagonal, int Hundredths, int Rise, int Fall)
    {
        /// <summary>The blocks a player places to make the step: every block of rise over the free one.</summary>
        public int Blocks => Math.Max(0, Rise - FreeRise);
    }

    /// <summary>Every place one step from <paramref name="place"/>, priced. The one definition of an edge in
    /// this walk: eight-connected, a diagonal refused where it would cut a corner across nothing, a storey
    /// refused where the span between the two places does not fit under the lower one's clearance, and a
    /// running jump from a gap's rim (<see cref="Leap"/>).</summary>
    private static IEnumerable<WalkStep> Steps(WalkPlace place, WalkGround ground)
    {
        foreach (var (dx, dz) in Sides)
        {
            var side = (X: place.X + dx, Z: place.Z + dz);
            if (!ground.Stacks.TryGetValue(side, out var stack)) continue;
            // A diagonal squeezes between two cells, and a player walks it only where there is ground on
            // both. One void side is a corner cut over a drop: the two ends are joined by the L through the
            // solid side, which is the route and costs what it costs.
            if (dx != 0 && dz != 0
                && (!ground.Footprint.Contains((place.X + dx, place.Z))
                    || !ground.Footprint.Contains((place.X, place.Z + dz)))) continue;

            foreach (var next in stack)
                if (Priced(place, next, dx != 0 && dz != 0, ground) is { } step) yield return step;
        }

        if (!Leaps(place, ground)) yield break;
        foreach (var offset in JumpOffsets.Values)
        {
            if (!ground.Stacks.TryGetValue((place.X + offset.X, place.Z + offset.Z), out var landing)) continue;
            foreach (var next in landing)
                if (Leap(place, next, offset, ground) is { } step) yield return step;
        }
    }

    /// <summary>One jump's offset: how far across it lands, the gap it clears edge to edge, and the cells the
    /// line between the two passes over.</summary>
    private readonly record struct JumpOffset(int X, int Z, double Gap, (int X, int Z)[] Over);

    /// <summary>Every offset a jump of up to <see cref="Jump.MaxGap"/> blocks lands at, by offset.</summary>
    private static readonly Dictionary<(int X, int Z), JumpOffset> JumpOffsets = Offsets();

    private static Dictionary<(int X, int Z), JumpOffset> Offsets()
    {
        var offsets = new Dictionary<(int X, int Z), JumpOffset>();
        var reach = Jump.MaxGap + 1;
        for (var dx = -reach; dx <= reach; dx++)
            for (var dz = -reach; dz <= reach; dz++)
            {
                var gap = Math.Sqrt(Math.Pow(Math.Max(0, Math.Abs(dx) - 1), 2) + Math.Pow(Math.Max(0, Math.Abs(dz) - 1), 2));
                if (gap < 1 || gap > Jump.MaxGap) continue;
                var samples = Math.Max(Math.Abs(dx), Math.Abs(dz)) * 3;
                var over = Enumerable.Range(1, samples - 1)
                    .Select(sample => ((int)Math.Round(dx * sample / (double)samples), (int)Math.Round(dz * sample / (double)samples)))
                    .Where(cell => cell != (0, 0) && cell != (dx, dz)).Distinct().ToArray();
                offsets[(dx, dz)] = new JumpOffset(dx, dz, gap, over);
            }
        return offsets;
    }

    /// <summary>Whether a jump is tried from a place at all: on ground that takes them, from the rim of a gap,
    /// with room over it to leave the ground.</summary>
    private static bool Leaps(WalkPlace place, WalkGround ground)
        => ground.Jumps && ground.Rims.Contains(place) && ground.ClearAbove(place) >= Jump.Headroom;

    /// <summary>A running jump from ground to ground across a gap, or null where it is not one: landing at most
    /// a block higher and inside the sprint jump's reach for that rise (<see cref="Jump.Clears"/>), over
    /// cells with nowhere to stand at or a block under the lower end, and with nothing solid along the line
    /// from there up to the player's body over the higher end. It places no block and costs the straight-line
    /// distance across.</summary>
    private static WalkStep? Leap(WalkPlace from, WalkPlace to, JumpOffset offset, WalkGround ground)
    {
        var rise = to.Y - from.Y;
        // A jump is from ground to ground. A cell a player would bridge is air until the block is there, and a
        // crossing bridged part way and jumped the rest is priced as the bridge.
        if (rise > 1 || !ground.Ground.Contains(from) || !ground.Ground.Contains(to) || !Jump.Clears(offset.Gap, rise))
            return null;
        var low = Math.Min(from.Y, to.Y) - 1;
        var body = Math.Max(from.Y, to.Y) + Jump.Headroom;
        foreach (var (dx, dz) in offset.Over)
        {
            var cell = (from.X + dx, from.Z + dz);
            if (ground.HasFloorFrom(cell, low)) return null;
            if (ground.Solid is { } solid)
            {
                // A block one under the lower end is ground a place at that end's own height would stand on.
                if (solid.TryGetValue(cell, out var spans) && spans.Any(span => span.Floor < body && span.Top >= low - 1))
                    return null;
            }
            else if (ground.Floors.TryGetValue(cell, out var under)
                && under.Any(place => ground.ClearAbove(place) is var room and not int.MaxValue && place.Y + room < body))
                return null;
        }
        var across = (int)Math.Round(Math.Sqrt(offset.X * offset.X + offset.Z * offset.Z) * Straight);
        return new WalkStep(to, offset.X != 0 && offset.Z != 0, across, Math.Max(0, rise), Math.Max(0, -rise));
    }

    /// <summary>The step a route takes between two of its places, priced as the walk prices it: a step to a
    /// neighbouring cell, or a jump to one further off.</summary>
    private static WalkStep? StepOf(WalkPlace from, WalkPlace to, WalkGround ground)
    {
        var (dx, dz) = (to.X - from.X, to.Z - from.Z);
        if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1) return Priced(from, to, dx != 0 && dz != 0, ground);
        return Leaps(from, ground) && JumpOffsets.TryGetValue((dx, dz), out var offset) ? Leap(from, to, offset, ground) : null;
    }

    /// <summary>The step from one place to a neighbouring one, or null where it does not fit under the
    /// clearance. Off a lift a player leaves from any height on it, and onto one arrives at any height on it
    /// and climbs to the place: of those, the step taken is the one placing fewest blocks, then falling least
    /// past <see cref="FreeDrop"/>, then climbing least. Where neither place is on a lift it is the plain
    /// step between their own heights.</summary>
    private static WalkStep? Priced(WalkPlace from, WalkPlace to, bool diagonal, WalkGround ground)
    {
        var flat = (diagonal ? Diagonal : Straight) * (ground.Water?.Contains(to.Cell) == true ? WaterSlowdown : 1);
        var leave = ground.LiftAt(from);
        var enter = ground.LiftAt(to);
        if (leave is null && enter is null)
            return ground.Steps(from, to)
                ? new WalkStep(to, diagonal, flat, Math.Max(0, to.Y - from.Y), Math.Max(0, from.Y - to.Y))
                : null;

        var (leaveLow, leaveHigh) = leave is { } off ? (Math.Min(off.Low, from.Y), off.High) : (from.Y, from.Y);
        var (enterLow, enterHigh) = enter is { } on ? (Math.Min(on.Low, to.Y), on.High) : (to.Y, to.Y);
        WalkStep? best = null;
        (int Blocks, int Drop, int Climbed) bestKey = default;
        for (var height = leaveLow; height <= leaveHigh; height++)
        {
            // Arrive inside the free band where the lift allows it, nearest the place; else at its nearer end.
            var arrive = enterHigh < height - FreeDrop ? enterHigh
                : enterLow > height + FreeRise ? enterLow
                : Math.Clamp(to.Y, Math.Max(enterLow, height - FreeDrop), Math.Min(enterHigh, height + FreeRise));
            if (!ground.Fits(height, arrive, from, to)) continue;

            var climbed = Math.Abs(height - from.Y) + Math.Abs(arrive - to.Y);
            var step = new WalkStep(to, diagonal, flat + climbed * Straight,
                Math.Max(0, arrive - height), Math.Max(0, height - arrive));
            var key = (step.Blocks, step.Fall > FreeDrop ? step.Fall : 0, climbed);
            if (best is null || key.CompareTo(bestKey) < 0) (best, bestKey) = (step, key);
        }
        return best;
    }

    /// <summary>Which places can reach each other, as a place → component id map with ids from 1. Two places
    /// share an id exactly when a route runs between them, so a deck and the gallery under it are one component
    /// only where something joins them. <para>The flood is over the same edges <see cref="Between"/> solves
    /// across, which is what stops a connectivity verdict and a distance disagreeing about whether there is a
    /// way.</para>
    /// <para><paramref name="rise"/> is what separates two questions a board is asked. Unbounded — the default —
    /// a step is any the clearance admits, however high, because a player carries blocks and pays for the climb:
    /// that is whether anyone <b>can</b> get there, which is what a gate refusing an unreachable objective means.
    /// Bounded to <see cref="FreeRise"/> it is whether the ground itself <b>joins</b>, since a rise the player
    /// has to build is a way nobody drew. The bound applies to a fall too: a cliff a player can only drop off is
    /// not a connection between the two sides of it.</para>
    /// <para><b>rise</b> — The tallest step between two places, up or down, that counts as joining
    /// them.</para></summary>
    public static Dictionary<WalkPlace, int> Components(WalkGround ground, int rise = int.MaxValue)
    {
        var labels = new Dictionary<WalkPlace, int>();
        var id = 0;
        var frontier = new Queue<WalkPlace>();
        foreach (var start in ground.Passable)
        {
            if (labels.ContainsKey(start)) continue;
            labels[start] = ++id;
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var place = frontier.Dequeue();
                foreach (var step in Steps(place, ground))
                {
                    if (step.Rise > rise || step.Fall > rise) continue;
                    if (labels.TryAdd(step.Next, id)) frontier.Enqueue(step.Next);
                }
            }
        }
        return labels;
    }

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

        // A place's route is its predecessor's route and one step more, so each tally extends the one before
        // it rather than re-walking the route from the origin.
        var tallies = new Dictionary<WalkPlace, Tally>(came.Count + 1) { [from] = Tally.Start(from, ground) };
        var unmeasured = new Stack<WalkPlace>();
        foreach (var place in came.Keys)
        {
            for (var back = place; !tallies.ContainsKey(back); back = came[back]) unmeasured.Push(back);
            while (unmeasured.TryPop(out var step)) tallies[step] = tallies[came[step]].Then(came[step], step, ground);
            costs[place] = tallies[place].Cost(ground);
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
        int slack) => Corridor(Field(from, ground), Field(to, ground), to, slack);

    /// <summary>The same ribbon over two fields the caller already holds. A field costs a solve plus a
    /// measured route to every reachable place, and a caller asking about many pairs of the same waypoints —
    /// a coverage read walks every pair — asks for each waypoint's field once per pair it appears in. Taking
    /// the fields as arguments is what lets it be asked once per waypoint instead.</summary>
    public static HashSet<WalkPlace> Corridor(
        IReadOnlyDictionary<WalkPlace, WalkCost> outward,
        IReadOnlyDictionary<WalkPlace, WalkCost> homeward, WalkPlace to, int slack)
    {
        var ribbon = new HashSet<WalkPlace>();
        if (!outward.TryGetValue(to, out var direct)) return ribbon;

        var budget = direct.Distance + Math.Max(0, slack);
        foreach (var (place, near) in outward)
            if (homeward.TryGetValue(place, out var far) && near.Distance + far.Distance <= budget)
                ribbon.Add(place);
        return ribbon;
    }

    /// <summary>
    /// Where two sides arrive together, as <b>one seat per way across</b> — the places a player is already
    /// standing when the other side reaches them, which is an origin rather than a destination.
    ///
    /// <para>Nothing names the middle of a board, so it is derived: the places both sides reach at the same
    /// cost, within <paramref name="slack"/>, are the line they meet on. That line breaks into one stretch per
    /// way across, and each stretch answers with its <b>widest</b> cell — the crossing players use, rather
    /// than the corner where the line clips a wall. A stretch under <paramref name="floor"/> cells is the line
    /// clipping something in passing and is not a way across.</para>
    ///
    /// <para><paramref name="slack"/> and <paramref name="floor"/> are the caller's because they are stated in
    /// the caller's grid: a cell is a block at one fidelity and several at another, so a shared number would
    /// mean two different distances.</para>
    ///
    /// <para>Two sides, because that is what a middle is. A caller with one has no meeting line and a caller
    /// with four has six of them, none of which describes one crossing.</para>
    /// </summary>
    public static List<WalkPlace> Crossings(IEnumerable<WalkPlace> near, IEnumerable<WalkPlace> far,
        WalkGround ground, int slack, int floor)
    {
        var seats = new List<WalkPlace>();
        var reachNear = Cheapest(near, ground);
        var reachFar = Cheapest(far, ground);
        var meeting = new Dictionary<(int X, int Z), WalkPlace>();
        foreach (var (place, fromNear) in reachNear)
            if (reachFar.TryGetValue(place, out var fromFar) && Math.Abs(fromNear - fromFar) <= slack)
                // One seat a cell: the storey the two sides meet lowest on is the one they meet on.
                if (!meeting.TryGetValue(place.Cell, out var known) || place.Y < known.Y)
                    meeting[place.Cell] = place;
        if (meeting.Count == 0) return seats;

        var clearance = Cells.Clearance(ground.Footprint, ground.Bounds);
        var line = meeting.Keys.ToHashSet();
        var seen = new HashSet<(int X, int Z)>();
        foreach (var cell in line)
        {
            if (!seen.Add(cell)) continue;
            var stretch = Cells.Flood([cell], line);
            seen.UnionWith(stretch);
            if (stretch.Count < floor) continue;
            seats.Add(meeting[stretch.OrderByDescending(one => clearance.GetValueOrDefault(one, 0))
                .ThenBy(one => one.Item2).ThenBy(one => one.Item1).First()]);
        }
        return seats;
    }

    /// <summary>The cheapest walk to each place from any of <paramref name="origins"/>, in blocks — one field
    /// per origin, kept at the best each place answers.</summary>
    private static Dictionary<WalkPlace, double> Cheapest(IEnumerable<WalkPlace> origins, WalkGround ground)
    {
        var best = new Dictionary<WalkPlace, double>();
        foreach (var origin in origins)
            foreach (var (place, cost) in Field(origin, ground))
                if (cost.Distance < best.GetValueOrDefault(place, double.MaxValue)) best[place] = cost.Distance;
        return best;
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

        var tally = Tally.Start(route[0], ground);
        for (var i = 1; i < route.Count; i++) tally = tally.Then(route[i - 1], route[i], ground);
        return tally.Cost(ground);
    }

    /// <summary>A route's cost as it is walked: the distance still in hundredths of a cell, so it is rounded
    /// once, at the end, however many steps it was summed over.</summary>
    private readonly record struct Tally(int Hundredths, int Blocks, int Drops, int WorstDrop)
    {
        public static Tally Start(WalkPlace place, WalkGround ground) => new(0, StandingBlocks(place, ground), 0, 0);

        /// <summary>This tally, one step further — from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public Tally Then(WalkPlace from, WalkPlace to, WalkGround ground)
        {
            var diagonal = from.X != to.X && from.Z != to.Z;
            // A route read back from a picture may join two places no step joins; it is priced as the plain
            // step between their heights rather than refused.
            var step = StepOf(from, to, ground)
                ?? new WalkStep(to, diagonal, diagonal ? Diagonal : Straight,
                    Math.Max(0, to.Y - from.Y), Math.Max(0, from.Y - to.Y));
            var (drops, worst) = step.Fall > FreeDrop
                ? (Drops + 1, Math.Max(WorstDrop, step.Fall))
                : (Drops, WorstDrop);
            return new Tally(Hundredths + step.Hundredths, Blocks + StandingBlocks(to, ground) + step.Blocks,
                drops, worst);
        }

        public WalkCost Cost(WalkGround ground)
            => new((int)Math.Round(Hundredths * ground.BlocksPerCell / 100.0), Blocks, Drops, WorstDrop);
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

            foreach (var step in Steps(place, ground))
            {
                var next = step.Next;
                if (settled.Contains(next)) continue;
                var blocks = here.Blocks + StandingBlocks(next, ground) + step.Blocks;
                var candidate = (here.Distance + step.Hundredths, blocks, Math.Max(here.Deficit, Deficit(next)));
                if (best.TryGetValue(next, out var known) && Rank(known).CompareTo(Rank(candidate)) <= 0) continue;
                best[next] = candidate;
                came[next] = place;
                queue.Enqueue(next, Rank(candidate));
            }
        }
        return came;

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
