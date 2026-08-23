using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Where a map's ground is actually <b>lived on</b> — and where it is dead. The distance terms say how far
/// apart the objectives stand; nothing says whether the ground <em>between and beside</em> them is part of
/// the match, and every oversized board so far has passed the distance reads while carrying whole regions no
/// player will ever enter.
///
/// <para>The model is traffic. Players move between the map's waypoints — spawns and goals — along shortest
/// walks over the navigable ground, so those walks, widened the way a path's band claims more than its own
/// centerline, are the ground the match actually uses. Around each waypoint a fight needs room, so each
/// claims its own ring. Everything else the terrain offers is then one of two things: <b>decorated</b> —
/// within reach of a prop someone placed, scenery a player at least looks at — or <b>dead</b>: ground with no
/// route through it, no objective near it and nothing on it. A destroy board may decorate its dead ground; a
/// board whose dead share is large is a board that is too big for what it plays.</para>
///
/// <para>This is a <b>measurement, not a rule</b>: it reports shares and named patches with coordinates, and
/// what refuses or scores on them is a later decision. The one verdict it takes for itself is the
/// classification, because the classes are geometry.</para>
/// </summary>
public static class GroundCoverage
{

    /// <summary>The ground a waypoint claims for itself — the ring a fight over it happens on, the same ten
    /// blocks the goal standoff keeps props out of.</summary>
    public const int PoiRadius = 10;

    /// <summary>How far one prop decorates around itself — the ground a tree or a rock makes worth looking
    /// at, which is smaller than the ground a fight needs.</summary>
    public const int PropRadius = 4;

    /// <summary>Dead patches smaller than this are slivers between corridors, not places — counted in the
    /// totals but not named.</summary>
    public const int PatchFloor = 25;

    /// <summary>Cell classes, indexed by the codes in <see cref="Result.Cells"/>.</summary>
    public static readonly string[] Classes = ["void", "reached", "decorated", "dead", "route", "waypoint"];

    /// <summary>Legend colours for the classes, shared by the stage image and any overlay.</summary>
    public static readonly Dictionary<string, string> ClassColors = new()
    {
        ["void"] = "#14161a", ["reached"] = "#2e7d32", ["decorated"] = "#f9a825",
        ["dead"] = "#c62828", ["route"] = "#a5d6a7", ["waypoint"] = "#ffffff",
    };

    public const byte Void = 0, Reached = 1, Decorated = 2, Dead = 3, Route = 4, Waypoint = 5;

    /// <summary>One contiguous stretch of dead ground: how big, where its centre sits, and how far its
    /// nearest cell stands from ground the match uses — the numbers that say whether it wants a prop, a
    /// point of interest, or deleting.</summary>
    public sealed record Patch(int Area, int CentroidX, int CentroidZ, int NearestReachedBlocks);

    /// <summary>The read: the class of every ground cell (<see cref="Cells"/>, codes above, row-major over
    /// the grid), the shares, and the dead patches worth naming, largest first.
    ///
    /// <para><see cref="Traffic"/> is how many of the <see cref="Journeys"/> cover each cell, row-major on the
    /// same grid — the number the classification throws away. Ground carried by one journey and ground carried
    /// by all of them are both <see cref="Reached"/>, and on a played map the busiest cell sees twenty to a
    /// hundred and sixty times what the quietest does, so membership is the coarse half of the answer and this
    /// is the other one. It is also what says which way round a hole is preferred and which is not.</para></summary>
    public sealed record Result(
        int MinX, int MinZ, int Width, int Height, byte[] Cells,
        int GroundCells, int ReachedCells, int DecoratedCells, int DeadCells,
        double DeadShare, IReadOnlyList<Patch> DeadPatches, int UnnamedDeadPatches, bool HaveRoutes,
        int[] Traffic, int Journeys);

    /// <summary>Read the coverage of a map: its document for the waypoints and rules, the built world's
    /// surface and Y=0 columns for the ground, and the dressing's prop cells for the decorated class.</summary>
    /// <param name="declared">Goals the document cannot carry — see <see cref="NavPoints.Of"/>. Without them
    /// a board whose goals are not placed yet traces spawn to spawn and calls everything else dead.</param>
    public static Result Read(Dict data, HashSet<(int, int)> surfaceColumns, HashSet<(int, int)>? y0Columns,
        IReadOnlyList<(int X, int Z)> propCells, (int, int, int, int)? bbox = null, int margin = 16,
        IReadOnlyList<NavPoint>? declared = null)
    {
        var ground = Traversability.Ground(data, surfaceColumns, y0Columns, bbox, margin);
        var (minX, minZ, nx, nz) = (ground.MinX, ground.MinZ, ground.Nx, ground.Nz);
        var navigable = ground.Set();

        bool InGrid(int x, int z) => x >= minX && x < minX + nx && z >= minZ && z < minZ + nz;
        int At(int x, int z) => (z - minZ) * nx + (x - minX);

        // The waypoints, snapped onto the navigable set the way the traversability gate snaps its points.
        var waypoints = new List<(int X, int Z)>();
        foreach (var point in NavPoints.Of(data, (minX, minZ, minX + nx, minZ + nz), declared))
            if (Cells.SnapToWalkable((point.X, point.Z), navigable, 3) is { } seat) waypoints.Add(seat);

        // The traffic skeleton: a corridor between every pair of waypoints. All pairs rather than a curated
        // set, because defenders travel to defend, attackers rotate goal to goal, and mid fights happen
        // between spawns — every pair is a journey someone makes, and every one aims at a place the map
        // actually has rather than at open ground.
        //
        // A corridor and not a fattened line. One shortest path must commit to one side of a hole, so the
        // other side reads unused however many players take it — and going round a hole is the single most
        // valuable thing a shape does. The ribbon carries both, and carries them in proportion.
        var routeCells = new HashSet<(int X, int Z)>();
        var traffic = new Dictionary<(int X, int Z), int>();
        var journeys = 0;
        for (var i = 0; i < waypoints.Count; i++)
            for (var j = i + 1; j < waypoints.Count; j++)
            {
                var ribbon = Cells.Corridor(waypoints[i], waypoints[j], navigable, Walk.Detour);
                if (ribbon.Count == 0) continue;
                journeys++;
                foreach (var cell in ribbon) traffic[cell] = traffic.GetValueOrDefault(cell) + 1;
                if (Cells.ShortestPath(waypoints[i], waypoints[j], navigable) is { } path)
                    foreach (var cell in path) routeCells.Add(cell);
            }

        // The ground the match uses: every cell some journey's corridor covers, plus each waypoint's own ring.
        // The COUNT is kept rather than the union — a cell one journey clips and a cell every journey runs
        // down are both "reached", and which of the two a piece of ground is is most of what a coverage read
        // was wanted for.
        var used = new HashSet<(int X, int Z)>(traffic.Keys);
        used.UnionWith(Dilated([.. waypoints], PoiRadius));

        // Classify every ground cell. Ground is standing terrain only — a bridgeable gap is crossable, but
        // it is not ground that can be dead.
        var cells = new byte[nx * nz];
        foreach (var (x, z) in surfaceColumns)
        {
            if (!InGrid(x, z)) continue;
            cells[At(x, z)] = used.Contains((x, z)) ? Reached : Dead;
        }
        foreach (var (x, z) in propCells)
        {
            if (!InGrid(x, z)) continue;
            for (var dz = -PropRadius; dz <= PropRadius; dz++)
            for (var dx = -PropRadius; dx <= PropRadius; dx++)
                if (InGrid(x + dx, z + dz) && cells[At(x + dx, z + dz)] == Dead)
                    cells[At(x + dx, z + dz)] = Decorated;
        }

        int reached = 0, decorated = 0, dead = 0;
        foreach (var cell in cells)
        {
            if (cell == Reached) reached++;
            else if (cell == Decorated) decorated++;
            else if (cell == Dead) dead++;
        }
        var groundCells = reached + decorated + dead;

        // The dead patches, named with coordinates so each can be checked in-game — and measured against the
        // reached ground, so "how far off the match is this" is a number rather than an impression.
        var distances = DistanceToReached(cells, nx, nz);
        var deadCells = new List<(int X, int Z)>();
        for (var i = 0; i < cells.Length; i++)
            if (cells[i] is Dead or Decorated) deadCells.Add((i % nx, i / nx));
        var patches = new List<Patch>();
        var unnamed = 0;
        foreach (var component in GridComponents.Label(
                     [.. deadCells.Where(cell => cells[cell.Z * nx + cell.X] == Dead)], connectivity: 4))
        {
            if (component.Count < PatchFloor) { unnamed++; continue; }
            var nearest = component.Min(cell => distances[cell.Z * nx + cell.X]);
            patches.Add(new Patch(
                component.Count,
                minX + (int)component.Average(cell => cell.X),
                minZ + (int)component.Average(cell => cell.Z),
                nearest));
        }
        patches.Sort((a, b) => b.Area.CompareTo(a.Area));

        // The overlay's two annotation classes, painted last so they read over the fill.
        foreach (var (x, z) in routeCells) cells[At(x, z)] = Route;
        foreach (var (x, z) in waypoints)
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
                if (InGrid(x + dx, z + dz)) cells[At(x + dx, z + dz)] = Waypoint;

        var trafficGrid = new int[nx * nz];
        foreach (var (cell, count) in traffic)
            if (InGrid(cell.X, cell.Z)) trafficGrid[At(cell.X, cell.Z)] = count;

        return new Result(minX, minZ, nx, nz, cells,
            groundCells, reached, decorated, dead,
            groundCells == 0 ? 0 : (double)dead / groundCells,
            patches, unnamed, routeCells.Count > 0, trafficGrid, journeys);

        static HashSet<(int X, int Z)> Dilated(IReadOnlyCollection<(int X, int Z)> seeds, int radius)
        {
            var grown = new HashSet<(int X, int Z)>();
            foreach (var (x, z) in seeds)
                for (var dz = -radius; dz <= radius; dz++)
                for (var dx = -radius; dx <= radius; dx++)
                    grown.Add((x + dx, z + dz));
            return grown;
        }
    }

    /// <summary>Per-cell 4-connected distance over the ground to the nearest reached cell — one multi-source
    /// BFS, so a patch's remoteness is the walk a player would actually make to it, not a line.</summary>
    private static int[] DistanceToReached(byte[] cells, int nx, int nz)
    {
        var distances = new int[cells.Length];
        Array.Fill(distances, int.MaxValue);
        var queue = new Queue<int>();
        for (var i = 0; i < cells.Length; i++)
            if (cells[i] == Reached) { distances[i] = 0; queue.Enqueue(i); }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            int x = at % nx, z = at / nx;
            foreach (var (neighborX, neighborZ) in ((int, int)[])[(x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1)])
            {
                if (neighborX < 0 || neighborX >= nx || neighborZ < 0 || neighborZ >= nz) continue;
                var next = neighborZ * nx + neighborX;
                if (cells[next] == Void || distances[next] != int.MaxValue) continue;
                distances[next] = distances[at] + 1;
                queue.Enqueue(next);
            }
        }
        // A dead patch the reached ground cannot walk to at all is remote beyond measuring; cap it so a
        // caller sorting by remoteness is not sorting infinities.
        for (var i = 0; i < distances.Length; i++)
            if (distances[i] == int.MaxValue) distances[i] = nx + nz;
        return distances;
    }
}
