using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Pgm.Derive;

/// <summary>One place a journey starts or ends, on the fanned board: a marker the plan states, in cells.
/// <see cref="K"/> is the orbit image it belongs to, so a two-team board answers each team's own copy under
/// the same <see cref="Id"/>.</summary>
public sealed record PlanPoi(string Id, string Kind, int K, int X, int Z)
{
    public (int X, int Z) Cell => (X, Z);
}

/// <summary>
/// What a plan looks like to someone walking it — the fanned board as a navigable cell set, one tier above the
/// piece graph and one below the built world.
///
/// <para>Two masks a route reads differently. <see cref="Ground"/> is terrain a player stands on: every
/// generating piece, fanned to its whole orbit. <see cref="Bridge"/> is a build zone's cells — void a player
/// crosses by placing blocks, which is a route and not a wall, so both are <see cref="Navigable"/>. A water
/// lane is neither by default: it opens late and no early journey is walked through one. A zone's declared
/// holes are void whatever covers them.</para>
///
/// <para>The point of reading a plan this way is that it costs nothing. Every gate that runs before a build
/// answers over pieces — <c>ContactGraph</c> classifies whether two rectangles touch, <c>FannedGraph</c> asks
/// whether one reaches another — and neither can say how far apart two places are, how wide the way between
/// them is, or whether there is more than one. Those are cell questions, and the cells are implied by the
/// plan already.</para>
/// </summary>
public sealed class PlanNav
{
    public IReadOnlySet<(int X, int Z)> Ground { get; }
    public IReadOnlySet<(int X, int Z)> Bridge { get; }
    public IReadOnlySet<(int X, int Z)> Navigable { get; }

    /// <summary>A water lane's cells. Not navigable by default and kept nonetheless, because a read that
    /// draws the board has to distinguish a crossing that opens late from void that never opens at all.</summary>
    public IReadOnlySet<(int X, int Z)> Lane { get; }

    /// <summary>Which piece image owns each ground cell — the name a route is read back in, so a walk can be
    /// said aloud as a piece sequence rather than as a list of coordinates.</summary>
    public IReadOnlyDictionary<(int X, int Z), string> PieceAt { get; }

    /// <summary>The ground height under each navigable cell, in blocks — a piece's own <c>surface</c> where
    /// it states one, the plan's global surface where it does not. A build zone takes the height of whatever
    /// it is bridging between, which is not stated anywhere, so it reads the global default too.
    ///
    /// <para>This is the input a flat walk throws away. A route that only counts steps climbs a scarp at the
    /// price of level ground, and on a board whose halves sit at different heights that is most of what
    /// decides where players go.</para></summary>
    public IReadOnlyDictionary<(int X, int Z), int> SurfaceAt { get; }

    /// <summary>The authored role behind each name in <see cref="PieceAt"/> — spawn, wool room, piece — so a
    /// reader can group by what a piece <em>is</em> before reading which one it is.</summary>
    public IReadOnlyDictionary<string, string> RoleOf { get; }

    /// <summary>The enclosed voids of <see cref="Navigable"/>, largest first — the holes a route may have a
    /// way round. Every one is a pocket the outside flood cannot reach, so an open bay is not one.</summary>
    public IReadOnlyList<HashSet<(int X, int Z)>> Holes { get; }

    /// <summary>Every marker the plan states, fanned.</summary>
    public IReadOnlyList<PlanPoi> Pois { get; }

    /// <summary>The board's extent in cells — every navigable cell and every hole falls inside it.</summary>
    public CellRect Bounds { get; }

    /// <summary>Blocks per cell, so a cell answer can be quoted in the unit an author states a rule in.</summary>
    public int Cell { get; }

    private PlanNav(HashSet<(int X, int Z)> ground, HashSet<(int X, int Z)> bridge,
        HashSet<(int X, int Z)> lane, Dictionary<(int X, int Z), string> pieceAt,
        Dictionary<string, string> roleOf, Dictionary<(int X, int Z), int> surfaceAt,
        List<HashSet<(int X, int Z)>> holes, List<PlanPoi> pois, CellRect bounds, int cell)
    {
        SurfaceAt = surfaceAt;
        Ground = ground;
        Bridge = bridge;
        Lane = lane;
        RoleOf = roleOf;
        var navigable = new HashSet<(int X, int Z)>(ground);
        navigable.UnionWith(bridge);
        Navigable = navigable;
        PieceAt = pieceAt;
        Holes = holes;
        Pois = pois;
        Bounds = bounds;
        Cell = cell;
    }

    /// <summary>The ground a <see cref="Walk"/> runs over at the plan fidelity: pieces to stand on, build
    /// zones to bridge, and each cell's stated surface. A plan has no relief, no boulder and no pond, so it
    /// has no water and its heights are the ones an author typed — which is the whole of what this tier can
    /// answer, and it can answer it before a world exists.</summary>
    public WalkGround Walkable() => new(Seat(Ground), Seat(Bridge), Bounds, Cell);

    /// <summary>The cells at their stated surface. A plan states one storey per cell, so a cell names exactly
    /// one place and the board is a stack of one the whole way through.</summary>
    private HashSet<WalkPlace> Seat(IEnumerable<(int X, int Z)> cells)
        => [.. cells.Select(cell => new WalkPlace(cell.X, cell.Z, SurfaceAt.GetValueOrDefault(cell)))];

    /// <param name="lanesNavigable">Whether a water lane counts as a crossing. Off by default: a lane opens
    /// partway through a match, so a route walked through one is not a route the opening is played on.</param>
    public static PlanNav Of(PlanModel plan, bool lanesNavigable = false)
    {
        var scene = PlanBoardScene.Build(plan);
        var ground = new HashSet<(int X, int Z)>();
        var bridge = new HashSet<(int X, int Z)>();
        var lane = new HashSet<(int X, int Z)>();
        var pieceAt = new Dictionary<(int X, int Z), string>();
        var roleOf = new Dictionary<string, string>();
        var surfaceAt = new Dictionary<(int X, int Z), int>();
        var pois = new List<PlanPoi>();
        var surfaceOf = plan.Pieces.ToDictionary(p => p.Id, p => p.Surface ?? plan.Globals.Surface);

        if (scene is null)
            return new PlanNav(ground, bridge, lane, pieceAt, roleOf, surfaceAt, [], pois,
                new CellRect(0, 0, 0, 0), plan.Globals.Cell);

        foreach (var piece in scene.Pieces)
        {
            var name = piece.K == 0 ? piece.Id : $"{piece.Id}#{piece.K}";
            roleOf[name] = piece.Role;
            var height = surfaceOf.GetValueOrDefault(piece.Id, plan.Globals.Surface);
            foreach (var cell in CellsOf(piece.Rect))
            {
                ground.Add(cell);
                if (pieceAt.TryAdd(cell, name)) surfaceAt[cell] = height;
            }
        }

        foreach (var zone in scene.Zones)
        {
            foreach (var cell in CellsOf(zone.Rect))
            {
                if (zone.Lane) lane.Add(cell);
                if (zone.Lane && !lanesNavigable) continue;
                if (!ground.Contains(cell) && bridge.Add(cell)) surfaceAt.TryAdd(cell, plan.Globals.Surface);
            }
        }

        // A declared hole is void however it is covered — the zone rect that contains it is not a crossing
        // there, and the ground under it was never stated.
        foreach (var cell in scene.ZoneHoles)
        { bridge.Remove(cell); ground.Remove(cell); lane.Remove(cell); pieceAt.Remove(cell); surfaceAt.Remove(cell); }

        foreach (var marker in scene.Markers)
            pois.Add(new PlanPoi(marker.Id, marker.Kind, marker.K,
                (int)Math.Floor(marker.X), (int)Math.Floor(marker.Z)));

        var navigable = new HashSet<(int X, int Z)>(ground);
        navigable.UnionWith(bridge);
        var bounds = navigable.Count == 0 ? new CellRect(0, 0, 0, 0) : Cells.BoundingBox([.. navigable]);

        var holes = new List<HashSet<(int X, int Z)>>();
        foreach (var pocket in Pockets(navigable, bounds)) holes.Add(pocket);
        holes.Sort((a, b) => b.Count.CompareTo(a.Count));

        return new PlanNav(ground, bridge, lane, pieceAt, roleOf, surfaceAt, holes, pois, bounds, plan.Globals.Cell);
    }

    /// <summary>Snap a cell onto the navigable set — a marker sits on a piece by construction, but a place an
    /// author names ("the west edge of the mid stone") need not, and a walk from a cell that is not walkable
    /// answers nothing rather than answering about the nearest place that is.</summary>
    public (int X, int Z)? Snap((int X, int Z) cell, int radius = 4) =>
        Cells.SnapToWalkable(cell, Navigable, radius);

    /// <summary>A fanned piece name back to the id the plan states (<c>piece-7#1</c> → <c>piece-7</c>) — what
    /// a cell of <see cref="PieceAt"/> is called in the document, as opposed to which orbit image it is.</summary>
    public static string BaseId(string name)
    {
        var hash = name.IndexOf('#');
        return hash < 0 ? name : name[..hash];
    }

    /// <summary>The named POIs, one entry per distinct marker id and orbit image, in a stable order — the
    /// demand set a coverage read walks over, before any arbitrary origin is added to it.</summary>
    public IEnumerable<PlanPoi> Waypoints() =>
        Pois.Where(p => p.Kind is "spawn" or "wool" or "destroyable" or "core")
            .OrderBy(p => p.Kind, StringComparer.Ordinal).ThenBy(p => p.K).ThenBy(p => p.Id, StringComparer.Ordinal);

    private static IEnumerable<(int X, int Z)> CellsOf(CellRect rect)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
            for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                yield return (x, z);
    }

    // The enclosed void components of a cell set: flood the outside from a one-cell margin, and every empty
    // cell the flood never reached is trapped. Same rule the closure measurement states, read here over the
    // navigable mask rather than over the solid one, because a build zone is a crossing and closes a hole.
    private static List<HashSet<(int X, int Z)>> Pockets(IReadOnlySet<(int X, int Z)> filled, CellRect bounds)
    {
        var pockets = new List<HashSet<(int X, int Z)>>();
        if (filled.Count == 0) return pockets;

        int minX = bounds.X - 1, minZ = bounds.Z - 1, maxX = bounds.MaxX, maxZ = bounds.MaxZ;
        bool Inside(int x, int z) => x >= minX && x <= maxX && z >= minZ && z <= maxZ;

        var outside = new HashSet<(int X, int Z)> { (minX, minZ) };
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue((minX, minZ));
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var neighbour in Cells.N4(cell))
                if (Inside(neighbour.Item1, neighbour.Item2) && !filled.Contains(neighbour) && outside.Add(neighbour))
                    queue.Enqueue(neighbour);
        }

        var seen = new HashSet<(int X, int Z)>();
        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
            {
                var start = (x, z);
                if (filled.Contains(start) || outside.Contains(start) || !seen.Add(start)) continue;
                var pocket = new HashSet<(int X, int Z)> { start };
                var walk = new Queue<(int X, int Z)>();
                walk.Enqueue(start);
                while (walk.Count > 0)
                {
                    var cell = walk.Dequeue();
                    foreach (var neighbour in Cells.N4(cell))
                        if (Inside(neighbour.Item1, neighbour.Item2) && !filled.Contains(neighbour)
                            && !outside.Contains(neighbour) && seen.Add(neighbour))
                        { pocket.Add(neighbour); walk.Enqueue(neighbour); }
                }
                pockets.Add(pocket);
            }
        return pockets;
    }
}
