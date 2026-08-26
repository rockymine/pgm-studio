using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// Whether the board still joins up with a prop standing on it (DR-WAY).
///
/// <para>A prop's standoff is measured to a <b>declared</b> route, so a building dropped across a corridor
/// nobody drew a stroke on passes every local test there is: the ground beside it is wide enough, no claim
/// collides, and the objectives still connect by some long way round. What that misses is the way the board
/// was drawn to have. This asks it directly — walk the ground between every pair of waypoints, then walk it
/// again with the prop's footprint taken out, and compare.</para>
///
/// <para><b>Two verdicts, and both are the prop's fault rather than the board's.</b> A pair that had a route
/// and now has none is a way closed. A pair whose route survives but goes further round than
/// <see cref="Walk.Detour"/> is the same fault at a lesser degree — the allowance is how far out of their way
/// a player will go, so a prop spending more than that has moved the route rather than been walked past.</para>
///
/// <para>The board is the <b>terrain surface</b>, one place a column, which is the ground a prop is planted
/// on and the ground a route between objectives runs over. Props accumulate: each admitted footprint stays
/// out of the ground the next candidate is judged on, so two buildings that each leave a way and together
/// leave none are caught at the second.</para>
/// </summary>
public sealed class WayThrough
{
    /// <summary>How far a waypoint may be moved to find ground to stand on, in blocks. A goal floats over its
    /// own terrain and a spawn sits in a room, so the cell a marker names is not always a cell to walk from.
    /// </summary>
    private const int SnapRadius = 8;

    private sealed class Route(WalkPlace from, WalkPlace to, int bare, HashSet<(int X, int Z)> cells)
    {
        public WalkPlace From { get; } = from;
        public WalkPlace To { get; } = to;
        public int Bare { get; } = bare;
        public HashSet<(int X, int Z)> Cells { get; set; } = cells;
    }

    private readonly WalkGround board;
    private readonly List<Route> routes;
    private readonly HashSet<(int X, int Z)> standing = [];

    private WayThrough(WalkGround board, List<Route> routes)
    {
        this.board = board;
        this.routes = routes;
    }

    /// <summary>Whether any pair of waypoints has a route to protect. A board with one waypoint, or one whose
    /// waypoints do not connect in the first place, has nothing this can measure.</summary>
    public bool HasRoutes => routes.Count > 0;

    /// <summary>Read the bare board: the walk over its terrain surface, and the shortest route between every
    /// pair of waypoints that has one.</summary>
    /// <param name="surfaceTop">The terrain's per-cell column height — a player stands at that height.</param>
    /// <param name="waypoints">The cells the map is played between: spawns, monuments, goals.</param>
    public static WayThrough Of(IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
                                IReadOnlyList<(int X, int Z)> waypoints)
    {
        var board = WalkGround.OfSpans(
            surfaceTop.Where(column => column.Value > 0)
                      .Select(column => (column.Key.X, column.Key.Z, 0, column.Value - 1)));

        var seats = new List<WalkPlace>();
        foreach (var waypoint in waypoints)
        {
            if (Cells.SnapToWalkable(waypoint, board.Footprint, SnapRadius) is not { } cell) continue;
            if (board.Stand(cell) is { } seat && !seats.Contains(seat)) seats.Add(seat);
        }

        var routes = new List<Route>();
        for (var i = 0; i < seats.Count; i++)
            for (var j = i + 1; j < seats.Count; j++)
            {
                if (Walk.Between(seats[i], seats[j], board) is not { } path) continue;
                routes.Add(new Route(seats[i], seats[j], path.Cost.Distance, [.. path.Cells]));
            }
        return new WayThrough(board, routes);
    }

    /// <summary>Put a prop on the board. Null where it may stand — and the board then holds its footprint, so
    /// the next candidate is judged with this one in the way. A finding where it closes a way or sends one
    /// further round than a player would go, and the board is left as it was.</summary>
    /// <param name="propId">What the finding names.</param>
    /// <param name="footprint">Every cell the prop occupies, across the whole orbit.</param>
    public Finding? Admit(string propId, IReadOnlyCollection<(int X, int Z)> footprint)
    {
        var covered = new HashSet<(int X, int Z)>(footprint);

        // A prop off every current route changes no route: the shortest way it does not touch is still there,
        // at the price it already cost. So the walk is only re-run for the routes it actually stands on.
        var crossed = routes.Where(route => route.Cells.Overlaps(covered)).ToList();
        if (crossed.Count == 0)
        {
            standing.UnionWith(covered);
            return null;
        }

        var narrowed = board.Narrowed(
            board.Footprint.Where(cell => !standing.Contains(cell) && !covered.Contains(cell)).ToHashSet());

        var walked = new List<(Route Route, WalkPath Path)>(crossed.Count);
        foreach (var route in crossed)
        {
            if (Walk.Between(route.From, route.To, narrowed) is not { } again)
                return new Finding(DressingRules.WayThrough,
                    $"'{propId}' closes the only way between ({route.From.X}, {route.From.Z}) and "
                    + $"({route.To.X}, {route.To.Z})",
                    Severity.Decline, Subjects: [propId]);

            var further = again.Cost.Distance - route.Bare;
            if (further > Walk.Detour)
                return new Finding(DressingRules.WayThrough,
                    $"'{propId}' sends the way between ({route.From.X}, {route.From.Z}) and "
                    + $"({route.To.X}, {route.To.Z}) {further} blocks further round, past the "
                    + $"{Walk.Detour} a player will go out of their way",
                    Severity.Decline, Subjects: [propId]);

            walked.Add((route, again));
        }

        standing.UnionWith(covered);
        foreach (var (route, path) in walked) route.Cells = [.. path.Cells];
        return null;
    }
}
