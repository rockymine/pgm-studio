using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The rules a board's structure answers, read off the ground the sketch builds rather than the plan's
/// rectangles (<see cref="SketchGround"/>): a face's front line (<c>FR8</c>, <c>FR9</c>), the narrowest run of
/// each gap beside a goal, beside a team's own ground and in a plain hole (<c>WL12</c>, <c>WL20</c>,
/// <c>LN6</c>), the strait between the two teams of a wool board (<c>CT12</c>), a build area running past the
/// last ground it docks (<c>BZ9</c>), a board with no build area (<c>SP11</c>), a wool no capturing team's spawn
/// can walk to (<c>PL9</c>) and one the front line reaches only through a spawn's room (<c>SP1</c>). Each is the
/// rule the plan is asked under the same id, measured in blocks, and an owner it names is a group or a room.
/// </summary>
public static class SketchBoardCheck
{
    /// <summary>What the board the layout and the intent build breaks; nothing where it builds no ground.</summary>
    public static Findings Check(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
        if (SketchGround.Of(layoutJson, intent) is not { } ground) return Findings.None;
        BoardStructure structure;
        try { structure = BoardDeriver.Derive(ground); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return Findings.None; }
        var rooms = ground.SpawnOwners.Concat(ground.WoolOwners).ToHashSet(StringComparer.Ordinal);
        var strait = (intent.Wools ?? []).Count > 0 && IntentCheck.Teams(intent, board) == 2;
        var zoneOf = new Dictionary<(int X, int Z), string>();
        foreach (var area in SketchGround.AuthoredAreas(intent, board))
            for (var x = (int)Math.Floor(area.MinX); x < (int)Math.Ceiling(area.MaxX); x++)
                for (var z = (int)Math.Floor(area.MinZ); z < (int)Math.Ceiling(area.MaxZ); z++)
                    zoneOf.TryAdd((x, z), area.Id);
        return new Findings([
            .. PlanValidator.BoardEdges(structure, rooms, strait), .. PlanValidator.ZoneReach(structure, zoneOf), .. Hops(structure),
            .. Routes(ground, intent),
        ]);
    }

    // SP11, PL9 and SP1: the walks a match needs to a wool, over the ground and the build cells together. Every team but
    // the wool's owner captures it, so each of their spawns has to reach its room; and the front line — the ground
    // a build area meets, and the build cells themselves — has to reach it without passing through a spawn's room.
    private static IEnumerable<Finding> Routes(BoardGround ground, MapIntent intent)
    {
        var spawns = intent.Spawns.Where(spawn => spawn.Protection.Count > 0).ToList();
        var wools = (intent.Wools ?? []).Where(wool => wool.Protection.Count > 0).ToList();
        if (ground.Build.Count == 0)
            yield return new Finding(LayoutRules.NoBuildRegion, "the map has no build area", Severity.Complaint);
        if (spawns.Count == 0 || wools.Count == 0) yield break;

        var closure = ground.Filled.Keys.Concat(ground.Build).ToHashSet();
        var reached = new Dictionary<string, HashSet<(int, int)>>(StringComparer.Ordinal);
        foreach (var spawn in spawns)
        {
            var seat = Geom.Cells.SnapToWalkable(((int)Math.Floor(spawn.Point.X), (int)Math.Floor(spawn.Point.Z)), closure, radius: 2);
            var walked = seat is { } from ? Geom.Cells.Flood([from], closure) : [];
            if (reached.TryGetValue(spawn.Team, out var team)) team.UnionWith(walked);
            else reached[spawn.Team] = walked;
        }

        // A wool's own room is where the walk ends, so it is open even where a spawn's room overlaps it.
        var spawnRooms = spawns.SelectMany(spawn => Region(spawn.Protection)).ToHashSet();
        var front = ground.Build.Concat(ground.Filled.Keys.Where(cell => Geom.Cells.N4(cell).Any(ground.Build.Contains)))
            .Where(cell => !spawnRooms.Contains(cell)).ToList();

        foreach (var wool in wools)
        {
            var room = Region(wool.Protection).Where(closure.Contains).ToHashSet();
            foreach (var (team, walked) in reached.Where(entry => entry.Key != wool.Owner))
                if (!room.Any(walked.Contains))
                    yield return new Finding(PlanRules.WoolUnreachable,
                        $"wool '{wool.Id}' of team '{wool.Owner}' has no route from the spawn of team '{team}'",
                        Subjects: [wool.Id]);
            if (ground.Build.Count == 0) continue;
            var open = closure.Where(cell => !spawnRooms.Contains(cell) || room.Contains(cell)).ToHashSet();
            if (!room.Any(Geom.Cells.Flood(front, open).Contains))
                yield return new Finding(LayoutRules.SpawnOnWoolRoute,
                    $"wool '{wool.Id}' has no route from the front line that avoids the room of a spawn", Subjects: [wool.Id]);
        }
    }

    /// <summary>The cells a region of rectangles covers.</summary>
    private static IEnumerable<(int, int)> Region(IEnumerable<Geom.Rect> rects) =>
        rects.SelectMany(rect =>
            from x in Enumerable.Range((int)Math.Floor(rect.MinX), Math.Max(0, (int)Math.Ceiling(rect.MaxX) - (int)Math.Floor(rect.MinX)))
            from z in Enumerable.Range((int)Math.Floor(rect.MinZ), Math.Max(0, (int)Math.Ceiling(rect.MaxZ) - (int)Math.Floor(rect.MinZ)))
            select (x, z));

    // G5: the void between every two landmasses one build region touches, measured on the straight span between
    // their nearest cells, where that span runs inside the region and through no third landmass. A pair is asked
    // within the authored unit's ground: a gap between two teams' ground is CT12's strait, and one inside an
    // image repeats the unit's.
    private static IEnumerable<Finding> Hops(BoardStructure board)
    {
        int Team(int island) => board.Islands[island].GroupBy(cell => board.Filled[cell].K).MaxBy(group => group.Count())!.Key;

        var seen = new HashSet<(int, int)>();
        foreach (var region in Regions(board.Build))
        {
            var touching = new Dictionary<int, List<(int X, int Z)>>();
            foreach (var cell in region)
                foreach (var near in Geom.Cells.N4(cell).Append(cell))
                    if (board.IslandOf.TryGetValue(near, out var island) && board.Roles[island] != "decorative")
                        (touching.TryGetValue(island, out var edge) ? edge : touching[island] = []).Add(near);
            var islands = touching.Keys.Order().ToList();
            for (var i = 0; i < islands.Count; i++)
                for (var j = i + 1; j < islands.Count; j++)
                {
                    var (a, b) = (islands[i], islands[j]);
                    if (Team(a) != 0 || Team(b) != 0 || !seen.Add((a, b))) continue;
                    var (from, to, span) = Nearest(touching[a], touching[b]);
                    if (span == 0 || !Open(from, to, region, board.IslandOf, a, b)) continue;
                    if (span >= GapHopBand.MinHop && span <= GapHopBand.MaxHop) continue;
                    var (sideA, sideB) = (Ground(board, a), Ground(board, b));
                    yield return new Finding(LayoutRules.VoidHop,
                        $"ground {Wording.Ids(sideA)} and ground {Wording.Ids(sideB)} have a gap of {span} blocks between them, "
                        + (span < GapHopBand.MinHop ? $"less than {GapHopBand.MinHop} blocks" : $"more than {GapHopBand.MaxHop} blocks"),
                        Severity.Complaint, Subjects: [.. sideA.Concat(sideB).Distinct()]);
                }
        }
    }

    private static IReadOnlyList<string> Ground(BoardStructure board, int island) =>
        [.. board.Islands[island].Select(cell => board.Filled[cell].Owner).Distinct().Order(StringComparer.Ordinal)];

    // The buildable cells in four-connected groups.
    private static IEnumerable<HashSet<(int X, int Z)>> Regions(IReadOnlySet<(int, int)> build)
    {
        var left = build.ToHashSet();
        while (left.Count > 0)
        {
            var start = left.First();
            var region = new HashSet<(int X, int Z)> { start };
            var queue = new Queue<(int, int)>([start]);
            left.Remove(start);
            while (queue.TryDequeue(out var cell))
                foreach (var near in Geom.Cells.N4(cell))
                    if (left.Remove(near)) { region.Add(near); queue.Enqueue(near); }
            yield return region;
        }
    }

    // The two cells, one of each side, with the least void between them: the cells clear on each axis, or where
    // both axes are clear, the diagonal they make.
    private static ((int X, int Z) From, (int X, int Z) To, int Span) Nearest(List<(int X, int Z)> sideA, List<(int X, int Z)> sideB)
    {
        var best = (From: sideA[0], To: sideB[0], Span: int.MaxValue);
        var targets = sideB.Distinct().ToList();
        foreach (var from in sideA.Distinct())
            foreach (var to in targets)
            {
                var gapX = Math.Max(0, Math.Abs(from.X - to.X) - 1);
                var gapZ = Math.Max(0, Math.Abs(from.Z - to.Z) - 1);
                var span = gapX == 0 ? gapZ : gapZ == 0 ? gapX : (int)Math.Round(Math.Sqrt(gapX * gapX + gapZ * gapZ));
                if (span < best.Span) best = (from, to, span);
            }
        return best;
    }

    // Whether the straight line between two cells' centres crosses only the region's cells and the two sides' own.
    private static bool Open((int X, int Z) from, (int X, int Z) to, HashSet<(int X, int Z)> region,
        Dictionary<(int, int), int> islandOf, int a, int b)
    {
        var steps = 4 * Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Z - from.Z));
        for (var step = 1; step < steps; step++)
        {
            var cell = ((int)Math.Floor(from.X + 0.5 + (to.X - from.X) * step / (double)steps),
                (int)Math.Floor(from.Z + 0.5 + (to.Z - from.Z) * step / (double)steps));
            if (islandOf.TryGetValue(cell, out var island)) { if (island != a && island != b) return false; continue; }
            if (!region.Contains(cell)) return false;
        }
        return true;
    }
}
