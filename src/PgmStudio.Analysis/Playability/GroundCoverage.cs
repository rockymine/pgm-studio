using PgmStudio.Analysis.Scan;
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
/// <para>The model is traffic. Players move between the map's waypoints — the spawns and goals, plus one seat
/// on each way across the middle, since the route that decides a match starts there rather than at a place a
/// team defends — along shortest walks over the navigable ground, so those walks, widened the way a path's
/// band claims more than its own centerline, are the ground the match actually uses. Around each waypoint a fight needs room, so each
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

    /// <summary>How close two teams' walks have to agree for a cell to count as ground they arrive at
    /// together. One octile diagonal, so a meeting line stays connected across a diagonal run.</summary>
    public const int MeetingSlack = 2;

    /// <summary>A meeting stretch smaller than this is the line clipping a corner, not a way across.</summary>
    public const int CrossingFloor = 8;

    /// <summary>Cell classes, indexed by the codes in <see cref="Result.Cells"/>.</summary>
    public static readonly string[] Classes = ["void", "reached", "decorated", "dead", "route"];

    /// <summary>Legend colours for the classes, shared by the stage image and any overlay.</summary>
    public static readonly Dictionary<string, string> ClassColors = new()
    {
        ["void"] = "#14161a", ["reached"] = "#2e7d32", ["decorated"] = "#f9a825",
        ["dead"] = "#c62828", ["route"] = "#a5d6a7",
    };

    public const byte Void = 0, Reached = 1, Decorated = 2, Dead = 3, Route = 4;

    /// <summary>How far a marker's cell may be off the ground and still be read as standing on it.</summary>
    private const int SnapRadius = 3;

    /// <summary>One contiguous stretch of dead ground: how big, where its centre sits, and how far its
    /// nearest cell stands from ground the match uses — the numbers that say whether it wants a prop, a
    /// point of interest, or deleting.</summary>
    public sealed record Patch(int Area, int CentroidX, int CentroidZ, int NearestReachedBlocks);

    /// <summary>A waypoint the journeys were walked between, at the cell it was snapped to. <see cref="Kind"/>
    /// is a <c>NavPoint.Kind</c> or <c>crossing</c> for a derived middle seat, so a picture can colour a
    /// spawn apart from a core instead of drawing every origin the same.</summary>
    public sealed record Marker(string Kind, int X, int Z);

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
        int[] Traffic, int Journeys, IReadOnlyList<Marker> Markers);

    /// <summary>Read the coverage of a map: its document for the waypoints and rules, the scanned columns for the
    /// ground a walk runs on, and the dressing's prop cells for the decorated class. <para>The picture is one
    /// pixel a cell, so a stacked column is drawn once however many storeys it carries — but the journeys under
    /// it are walked storey by storey, which is what decides whether the ground is used at all.</para>
    /// <para><b>declared</b> — Goals the document cannot carry — see <see cref="NavPoints.Of"/>. Without them a
    /// board whose goals are not placed yet traces spawn to spawn and calls everything else
    /// dead.</para></summary>
    public static Result Read(Dict data, SegmentIndex? segments,
        IReadOnlyList<(int X, int Z)> propCells, (int, int, int, int)? bbox = null, int margin = 16,
        IReadOnlyList<NavPoint>? declared = null)
    {
        var walked = WorldWalk.Ground(data, segments, margin, bbox);
        var box = walked.Bounds;
        var (minX, minZ, nx, nz) = (box.X, box.Z, box.Width, box.Height);
        var standing = walked.Ground.Select(place => place.Cell).ToHashSet();

        bool InGrid(int x, int z) => x >= minX && x < minX + nx && z >= minZ && z < minZ + nz;
        int At(int x, int z) => (z - minZ) * nx + (x - minX);

        // The waypoints, seated on the storey each names the way the traversability gate seats its points.
        var points = NavPoints.Of(data, (minX, minZ, minX + nx, minZ + nz), declared);
        var origins = new List<WalkPlace>();
        var markers = new List<Marker>();
        foreach (var point in points)
        {
            if (Cells.SnapToWalkable(point.Cell, walked.Footprint, SnapRadius) is not { } cell) continue;
            if ((point with { X = cell.X, Z = cell.Z }).Seat(walked) is not { } seat) continue;
            markers.Add(new Marker(point.Kind, seat.X, seat.Z));
            origins.Add(seat);
        }
        foreach (var seat in Crossings(points, walked))
        {
            markers.Add(new Marker("crossing", seat.X, seat.Z));
            origins.Add(seat);
        }
        var waypoints = markers.Select(marker => (marker.X, marker.Z)).ToList();

        // The traffic skeleton: a corridor between every pair of waypoints. All pairs rather than a curated
        // set, because defenders travel to defend, attackers rotate goal to goal, and mid fights happen
        // between spawns — every pair is a journey someone makes, and every one aims at a place the map
        // actually has rather than at open ground. The crossings are in that set too, so an attacker's
        // approach from the middle is walked and not only the defender's walk to their own objective.
        //
        // A corridor and not a fattened line. One shortest path must commit to one side of a hole, so the
        // other side reads unused however many players take it — and going round a hole is the single most
        // valuable thing a shape does. The ribbon carries both, and carries them in proportion.
        var routeCells = new HashSet<(int X, int Z)>();
        var traffic = new Dictionary<(int X, int Z), int>();
        var journeys = 0;
        for (var i = 0; i < origins.Count; i++)
            for (var j = i + 1; j < origins.Count; j++)
            {
                var ribbon = Walk.Corridor(origins[i], origins[j], walked, Walk.Detour);
                if (ribbon.Count == 0) continue;
                journeys++;
                // A ribbon carries places; two storeys of one column both covered is one cell covered once,
                // because the picture has one pixel for them and a journey does not use the cell twice.
                foreach (var cell in ribbon.Select(place => place.Cell).Distinct())
                    traffic[cell] = traffic.GetValueOrDefault(cell) + 1;
                if (Walk.Between(origins[i], origins[j], walked) is { } path)
                    foreach (var cell in path.Cells) routeCells.Add(cell);
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
        foreach (var (x, z) in standing)
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

        // The route, painted last so it reads over the fill, and only where the read already found ground: a
        // route runs over the navigable set, which carries bridgeable void the classification does not, and
        // an annotation may not add a cell to the picture. A waypoint is not a class — it is a place, and
        // Markers names each one with the kind a class code cannot carry.
        foreach (var (x, z) in routeCells)
            if (InGrid(x, z) && cells[At(x, z)] != Void) cells[At(x, z)] = Route;

        var trafficGrid = new int[nx * nz];
        foreach (var (cell, count) in traffic)
            if (InGrid(cell.X, cell.Z)) trafficGrid[At(cell.X, cell.Z)] = count;

        return new Result(minX, minZ, nx, nz, cells,
            groundCells, reached, decorated, dead,
            groundCells == 0 ? 0 : (double)dead / groundCells,
            patches, unnamed, routeCells.Count > 0, trafficGrid, journeys, markers);

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

    /// <summary>Where the two teams meet, as one seat per crossing — the origins an attacker's route starts
    /// from rather than ends at.
    ///
    /// <para>A goal-to-goal demand set only ever walks between places a team defends, and the route that
    /// decides a match is the one crossing the middle. At this tier no document field names the middle, so it
    /// is derived: the cells a walk reaches from both teams' spawns at the <b>same</b> cost, within
    /// <see cref="MeetingSlack"/> blocks, are the line the two sides arrive at together. That line breaks into
    /// one stretch per way across, and each stretch answers with its <b>widest</b> cell, which is the crossing
    /// players use rather than the corner where the line clips a wall.</para>
    ///
    /// <para>Empty unless exactly two teams hold spawns: with one team there is no middle, and with four the
    /// pairwise middles are six lines that no longer describe one crossing each.</para></summary>
    private static List<WalkPlace> Crossings(IReadOnlyList<NavPoint> points, WalkGround walked)
    {
        var seats = new List<WalkPlace>();
        var byTeam = points.Where(point => point.Kind == "spawn" && point.Owner.Length > 0)
            .GroupBy(point => point.Owner).ToList();
        if (byTeam.Count != 2) return seats;

        var sides = byTeam.Select(team => Reach(team, walked)).ToList();
        var meeting = new Dictionary<(int X, int Z), WalkPlace>();
        foreach (var (place, near) in sides[0])
            if (sides[1].TryGetValue(place, out var far) && Math.Abs(near - far) <= MeetingSlack)
                // One seat a cell: the storey the two sides meet lowest on is the one they meet on.
                if (!meeting.TryGetValue(place.Cell, out var known) || place.Y < known.Y)
                    meeting[place.Cell] = place;
        if (meeting.Count == 0) return seats;

        var footprint = meeting.Keys.ToHashSet();
        var clearance = Cells.Clearance(walked.Footprint, walked.Bounds);
        foreach (var stretch in Stretches(footprint))
            seats.Add(meeting[stretch.OrderByDescending(cell => clearance.GetValueOrDefault(cell, 0))
                .ThenBy(cell => cell.Z).ThenBy(cell => cell.X).First()]);
        return seats;
    }

    /// <summary>The cheapest walk to each place from any of <paramref name="from"/>, in blocks.</summary>
    private static Dictionary<WalkPlace, double> Reach(IEnumerable<NavPoint> from, WalkGround walked)
    {
        var best = new Dictionary<WalkPlace, double>();
        foreach (var point in from)
        {
            if (Cells.SnapToWalkable(point.Cell, walked.Footprint, SnapRadius) is not { } cell) continue;
            if ((point with { X = cell.X, Z = cell.Z }).Seat(walked) is not { } start) continue;
            foreach (var (place, cost) in Walk.Field(start, walked))
                if (cost.Distance < best.GetValueOrDefault(place, double.MaxValue)) best[place] = cost.Distance;
        }
        return best;
    }

    /// <summary>The connected stretches of a cell set, largest first, keeping only those wide enough to be a
    /// way across rather than a cell the meeting line clips in passing.</summary>
    private static List<HashSet<(int X, int Z)>> Stretches(HashSet<(int X, int Z)> cells)
    {
        var stretches = new List<HashSet<(int X, int Z)>>();
        var seen = new HashSet<(int X, int Z)>();
        foreach (var cell in cells)
        {
            if (!seen.Add(cell)) continue;
            var stretch = Cells.Flood([cell], cells);
            seen.UnionWith(stretch);
            if (stretch.Count >= CrossingFloor) stretches.Add(stretch);
        }
        return [.. stretches.OrderByDescending(stretch => stretch.Count)];
    }

    /// <summary>Per-cell cardinal step count over the ground to the nearest reached cell — one multi-source
    /// BFS, so a patch's remoteness is measured round what stands between, not as a line across it.</summary>
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
