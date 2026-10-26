using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The rules about where the intent's areas, lanes, walls and doors meet the ground the sketch builds
/// (<see cref="SketchGround"/>): a region narrower than a corridor (<c>G2</c>), a region or lane against a spawn
/// (<c>BZ5</c>), regions stitched into one rectangle (<c>BZ11</c>), a lane over ground (<c>BZ12</c>), a wall
/// against a wool's room (<c>PL13</c>) or with ground running past its end (<c>PL17</c>), the mouth a wall bars
/// (<c>ST8</c>) and a wall crowding the entrance it stands before (<c>ST11</c>), the ground ahead of a spawn's
/// door (<c>SP9</c>), and a step of two blocks or more at a spawn's door (<c>SP8</c>) or a wool room's entry
/// (<c>WL11</c>), and a wool room's door opening onto no ground (<c>WL21</c>). Each is the rule the plan is asked
/// under the same id, measured in blocks; a piece is asked once, by its authored image. A wool room's entries are
/// the sides of its region its doors open through, a wool stating none taking the one its build gives it.
/// </summary>
public static class SketchSeamCheck
{
    /// <summary>The ground a spawn's door must open onto before bare void, in blocks.</summary>
    public const int DoorGroundBlocks = 15;

    /// <summary>What the intent's pieces break against the ground the layout builds; nothing where it builds
    /// no ground.</summary>
    public static Findings Check(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
        intent = WoolDoors.Settled(intent, layoutJson);
        if (SketchGround.Of(layoutJson, intent) is not { } ground) return Findings.None;
        var areas = SketchGround.AuthoredAreas(intent, board);
        var lanes = SketchGround.AuthoredLanes(intent, board);
        var findings = new List<Finding>();

        foreach (var (zone, what) in areas.Select(area => (area, "build region")).Concat(lanes.Select(lane => (lane, "water lane"))))
        {
            var across = (int)Math.Round(Math.Min(zone.MaxX - zone.MinX, zone.MaxZ - zone.MinZ));
            if (across < ContactGraph.CorridorMin)
                findings.Add(Complaint(LayoutRules.CorridorWidth,
                    $"{what} '{zone.Id}' is {across} blocks across its shorter side, less than {ContactGraph.CorridorMin} blocks",
                    zone.Id));
            var spawns = intent.Spawns.Where(spawn => spawn.Protection.Any(region => Touches(region, zone.Rect)))
                .Select(spawn => spawn.Id).ToList();
            if (spawns.Count > 0)
                findings.Add(Complaint(LayoutRules.ZoneTouchesSpawn,
                    $"{what} '{zone.Id}' touches the region of {(spawns.Count == 1 ? "spawn" : "spawns")} {Wording.Ids(spawns)}",
                    [zone.Id, .. spawns]));
        }

        foreach (var lane in lanes)
            foreach (var owner in Cells(lane.Rect).Where(ground.Filled.ContainsKey).Select(cell => ground.Filled[cell].Owner).Distinct())
                findings.Add(Complaint(LayoutRules.WaterLaneOverGround,
                    $"water lane '{lane.Id}' overlaps the ground of {Named(owner)}", Subjects(lane.Id, owner)));

        findings.AddRange(Stitched(areas));

        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var column in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!tops.TryGetValue((column.X, column.Z), out var top) || column.YTop > top) tops[(column.X, column.Z)] = column.YTop;
        var entries = (intent.Wools ?? []).SelectMany(wool => Entries(wool).Select(side => (Wool: wool.Id, Side: side)))
            .ToList();

        var woolRegions = (intent.Wools ?? []).SelectMany(wool => wool.Protection.Select(region => (wool.Id, Cells: Cells(region).ToHashSet())))
            .ToList();
        foreach (var wall in (intent.Structures?.Walls ?? []).Where(wall => Authored(wall.Stamp)))
        {
            var columns = wall.Cells().ToList();
            var against = woolRegions.FirstOrDefault(region => columns.Any(cell => region.Cells.Contains(cell) || Geom.Cells.N4(cell).Any(region.Cells.Contains)));
            if (against.Cells is not null)
            {
                findings.Add(new Finding(PlanRules.WallOnWoolRoom,
                    $"approach wall '{wall.Id}' touches the room region of wool '{against.Id}'", Subjects: [wall.Id, against.Id]));
                continue;
            }

            var line = new Line(wall);
            if (line.Length == 0) continue;
            if (line.Flank(ground) is { } flank)
                findings.Add(Complaint(PlanRules.WallAtJunction,
                    $"approach wall '{wall.Id}' has the ground of {Named(flank)} running past its end", Subjects(wall.Id, flank)));
            var mouth = line.Mouth(ground);
            if (mouth < PlanValidator.WallMouthMinBlocks || mouth > PlanValidator.WallMouthMaxBlocks)
                findings.Add(Complaint(LayoutRules.ApproachWallEdgeLength,
                    $"approach wall '{wall.Id}' bars a mouth {mouth} blocks across, "
                    + Beyond(mouth, PlanValidator.WallMouthMinBlocks, PlanValidator.WallMouthMaxBlocks), wall.Id));

            // The entrance it stands before: the nearest entry parallel to it and joined to it by unbroken ground
            // across the span they share; a wall barring a side approach defends a flank. Only the near half of
            // the seat is asked, since a wool takes a wall per approach and one farther out guards its own.
            var alongX = Math.Abs(wall.End.X - wall.Start.X) >= Math.Abs(wall.End.Z - wall.Start.Z);
            var defended = entries
                .Where(entry => entry.Side.Edge.AlongX() == alongX && Joined(wall, entry.Side, alongX, ground))
                .Select(entry => (entry.Wool, Gap: PlanValidator.SegmentGap(wall.Start.X, wall.Start.Z, wall.End.X, wall.End.Z,
                    entry.Side.X1, entry.Side.Z1, entry.Side.X2, entry.Side.Z2)))
                .OrderBy(entry => entry.Gap)
                .FirstOrDefault();
            if (defended.Wool is not null && defended.Gap < PlanValidator.WallStandoffMinBlocks)
                findings.Add(Complaint(LayoutRules.ApproachWallStandoff,
                    $"approach wall '{wall.Id}' stands {defended.Gap} blocks from the entrance of wool '{defended.Wool}', "
                    + $"less than {PlanValidator.WallStandoffMinBlocks} blocks", wall.Id, defended.Wool));
        }

        foreach (var spawn in intent.Spawns.Where(spawn => Authored(spawn.Stamp) && spawn.Protection.Count > 0))
            if (spawn.AskedDoors() is [var asked, ..] && asked.Edge is var door)
                foreach (var side in Sides(spawn.Protection).Select(side => Ahead(side, door, spawn.Protection)).OfType<Side>())
                    foreach (var (owner, step) in Steps(side, spawn.Protection, ground, tops))
                        findings.Add(Complaint(LayoutRules.SpawnExitStep,
                            $"spawn '{spawn.Id}' steps {step} blocks to the ground of {Named(owner)} ahead of its {door.Word()} door, "
                            + "more than 1 block", new Measurement(step, High: 1), Subjects(spawn.Id, owner)));

        foreach (var wool in (intent.Wools ?? []).Where(wool => Authored(wool.Stamp)))
        {
            foreach (var side in Entries(wool))
                foreach (var (owner, step) in Steps(side, wool.Protection, ground, tops))
                    findings.Add(Complaint(LayoutRules.WoolEntryStep,
                        $"the room of wool '{wool.Id}' steps {step} blocks to the ground of {Named(owner)} at its {side.Edge.Word()} "
                        + "entry, more than 1 block", new Measurement(step, High: 1), Subjects(wool.Id, owner)));
            foreach (var door in StatedDoor.Asked(wool.Doors))
                if (!Opens(wool.Protection, door, ground))
                    findings.Add(Complaint(LayoutRules.WoolDoorGround,
                        $"the room of wool '{wool.Id}' has its {door.Edge.Word()} door opening onto no ground", wool.Id));
        }

        foreach (var spawn in intent.Spawns.Where(spawn => Authored(spawn.Stamp) && spawn.Protection.Count > 0))
            if (spawn.AskedDoors() is [var asked, ..] && asked.Edge is var door && Ahead(spawn, door, ground) is var ahead && ahead < DoorGroundBlocks)
                findings.Add(Complaint(LayoutRules.SpawnDoorGround,
                    $"spawn '{spawn.Id}' has {ahead} blocks of ground ahead of its {door.Word()} door, less than {DoorGroundBlocks} blocks",
                    spawn.Id));
        return findings;
    }

    // Touching areas grouped, and a group of two or more whose cells fill the rectangle around them.
    private static IEnumerable<Finding> Stitched(IReadOnlyList<PieceArea> areas)
    {
        var group = Enumerable.Range(0, areas.Count).ToArray();
        int Root(int index) => group[index] == index ? index : group[index] = Root(group[index]);
        for (var i = 0; i < areas.Count; i++)
            for (var j = i + 1; j < areas.Count; j++)
                if (Touches(areas[i].Rect, areas[j].Rect)) group[Root(i)] = Root(j);

        foreach (var members in Enumerable.Range(0, areas.Count).GroupBy(Root).Select(g => g.Select(index => areas[index]).ToList()))
        {
            if (members.Count < 2) continue;
            var covered = members.SelectMany(area => Cells(area.Rect)).ToHashSet();
            var bounds = BlockRect.Around(covered);
            if (covered.Count != (bounds.MaxX - bounds.MinX) * (bounds.MaxZ - bounds.MinZ)) continue;
            var ids = members.Select(area => area.Id).ToList();
            yield return Complaint(LayoutRules.StitchedZones,
                $"build regions {Wording.Ids(ids)} touch and together fill one rectangle", [.. ids]);
        }
    }

    /// <summary>One side of a room's region: the edge it faces, its line in block corners, and each block along it
    /// paired with the block just outside.</summary>
    private sealed record Side(RoomEdge Edge, int X1, int Z1, int X2, int Z2, List<((int, int) Inside, (int, int) Outside)> Blocks);

    private static IEnumerable<Side> Sides(IEnumerable<Rect> regions)
    {
        foreach (var region in regions)
        {
            int minX = (int)Math.Floor(region.MinX), minZ = (int)Math.Floor(region.MinZ);
            int maxX = (int)Math.Ceiling(region.MaxX), maxZ = (int)Math.Ceiling(region.MaxZ);
            yield return new Side(RoomEdge.NegZ, minX, minZ, maxX, minZ,
                [.. Enumerable.Range(minX, maxX - minX).Select(x => ((x, minZ), (x, minZ - 1)))]);
            yield return new Side(RoomEdge.PosZ, minX, maxZ, maxX, maxZ,
                [.. Enumerable.Range(minX, maxX - minX).Select(x => ((x, maxZ - 1), (x, maxZ)))]);
            yield return new Side(RoomEdge.NegX, minX, minZ, minX, maxZ,
                [.. Enumerable.Range(minZ, maxZ - minZ).Select(z => ((minX, z), (minX - 1, z)))]);
            yield return new Side(RoomEdge.PosX, maxX, minZ, maxX, maxZ,
                [.. Enumerable.Range(minZ, maxZ - minZ).Select(z => ((maxX - 1, z), (maxX, z)))]);
        }
    }

    // Whether the wall and an entry overlap along their line and every block between them, across that overlap,
    // is ground.
    private static bool Joined(WallStructure wall, Side side, bool alongX, BoardGround ground)
    {
        var (lo, hi) = alongX
            ? (Math.Max(Math.Min(wall.Start.X, wall.End.X), side.X1), Math.Min(Math.Max(wall.Start.X, wall.End.X), side.X2))
            : (Math.Max(Math.Min(wall.Start.Z, wall.End.Z), side.Z1), Math.Min(Math.Max(wall.Start.Z, wall.End.Z), side.Z2));
        if (hi <= lo) return false;
        var (from, to) = alongX ? (Math.Min(wall.Start.Z, side.Z1), Math.Max(wall.Start.Z, side.Z1)) : (Math.Min(wall.Start.X, side.X1), Math.Max(wall.Start.X, side.X1));
        for (var along = lo; along < hi; along++)
            for (var across = from; across < to; across++)
                if (!ground.Filled.ContainsKey(alongX ? (along, across) : (across, along))) return false;
        return true;
    }

    // What of a side lies ahead of a spawn's door: the door's own side whole, a flank side's blocks past the
    // middle of the region toward the door, and nothing of the back.
    private static Side? Ahead(Side side, RoomEdge door, IReadOnlyList<Rect> regions)
    {
        if (side.Edge == door) return side;
        if (side.Edge.AlongX() == door.AlongX()) return null;
        var (dirX, dirZ) = door.Outward();
        var middleX = regions.Average(region => (region.MinX + region.MaxX) / 2);
        var middleZ = regions.Average(region => (region.MinZ + region.MaxZ) / 2);
        var ahead = side.Blocks.Where(pair => (pair.Inside.Item1 + 0.5 - middleX) * dirX + (pair.Inside.Item2 + 0.5 - middleZ) * dirZ > 0)
            .ToList();
        return ahead.Count == 0 ? null : side with { Blocks = ahead };
    }

    // The sides of a wool's region its doors open through: a side of the region's bounds per edge a door faces.
    private static IEnumerable<Side> Entries(WoolIntent wool)
    {
        if (wool.Protection.Count == 0) yield break;
        var edges = StatedDoor.Asked(wool.Doors).Select(door => door.Edge).ToHashSet();
        foreach (var side in Sides([Bounds(wool.Protection)]).Where(side => edges.Contains(side.Edge))) yield return side;
    }

    private static Rect Bounds(IReadOnlyList<Rect> regions) =>
        new(regions.Min(r => r.MinX), regions.Min(r => r.MinZ), regions.Max(r => r.MaxX), regions.Max(r => r.MaxZ));

    // Whether ground or a build area lies just outside a door, within a block either side of its centre.
    private static bool Opens(IReadOnlyList<Rect> regions, AskedDoor door, BoardGround ground)
    {
        var side = Sides([Bounds(regions)]).First(side => side.Edge == door.Edge);
        var middle = side.Edge.AlongX() ? (side.X1 + side.X2) / 2.0 : (side.Z1 + side.Z2) / 2.0;
        var centre = door.Along ?? middle;
        return side.Blocks.Any(pair =>
        {
            var along = side.Edge.AlongX() ? pair.Outside.Item1 + 0.5 : pair.Outside.Item2 + 0.5;
            return Math.Abs(along - centre) <= 1.5 && !Within(regions, pair.Outside)
                && (ground.Filled.ContainsKey(pair.Outside) || ground.Build.Contains(pair.Outside));
        });
    }

    // Each owner of the ground outside a side whose easiest crossing steps two blocks or more, with that step: a
    // block along the side a player crosses at one block or less is the way in, however steep the rest is.
    private static IEnumerable<(string Owner, int Step)> Steps(
        Side side, IReadOnlyList<Rect> own, BoardGround ground, IReadOnlyDictionary<(int X, int Z), int> tops) =>
        side.Blocks
            .Where(pair => !Within(own, pair.Outside) && ground.Filled.ContainsKey(pair.Outside)
                && tops.ContainsKey(pair.Inside) && tops.ContainsKey(pair.Outside))
            .GroupBy(pair => ground.Filled[pair.Outside].Owner)
            .Select(group => (group.Key, group.Min(pair => Math.Abs(tops[pair.Outside] - tops[pair.Inside]))))
            .Where(owner => owner.Item2 >= 2);

    private static bool Within(IReadOnlyList<Rect> regions, (int X, int Z) cell) =>
        regions.Any(region => cell.X + 0.5 > region.MinX && cell.X + 0.5 < region.MaxX
            && cell.Z + 0.5 > region.MinZ && cell.Z + 0.5 < region.MaxZ);

    // Steps out of the spawn's own region along its door, then counts ground and buildable cells until bare void.
    private static int Ahead(SpawnIntent spawn, RoomEdge door, BoardGround ground)
    {
        var (dirX, dirZ) = door.Outward();
        var (x, z) = (spawn.Point.X, spawn.Point.Z);
        bool Inside(Rect region) => x >= region.MinX && x < region.MaxX && z >= region.MinZ && z < region.MaxZ;
        while (spawn.Protection.Any(Inside)) { x += dirX; z += dirZ; }
        var ahead = 0;
        while (ahead < DoorGroundBlocks)
        {
            var cell = ((int)Math.Floor(x), (int)Math.Floor(z));
            if (!ground.Filled.ContainsKey(cell) && !ground.Build.Contains(cell)) break;
            ahead++; x += dirX; z += dirZ;
        }
        return ahead;
    }

    /// <summary>The line between a wall's ends, sampled in blocks along it and half a block out on each face. A wall
    /// that bends bars the approach its ends span, so that line is what the seam is asked of.</summary>
    private readonly struct Line(WallStructure wall)
    {
        private readonly double x1 = wall.Start.X, z1 = wall.Start.Z, dx = wall.End.X - wall.Start.X, dz = wall.End.Z - wall.Start.Z;

        public double Length => Math.Sqrt(dx * dx + dz * dz);

        /// <summary>The cell at <paramref name="along"/> blocks from the first end and <paramref name="across"/>
        /// blocks to its left.</summary>
        private (int, int) At(double along, double across)
        {
            var (ux, uz) = (dx / Length, dz / Length);
            return ((int)Math.Floor(x1 + ux * along - uz * across), (int)Math.Floor(z1 + uz * along + ux * across));
        }

        /// <summary>The owner of the first ground one block beyond either end, on either face; null where both
        /// ends stand over void.</summary>
        public string? Flank(BoardGround ground)
        {
            foreach (var along in new[] { -0.5, Length + 0.5 })
                foreach (var across in new[] { -0.5, 0.5 })
                    if (ground.Filled.TryGetValue(At(along, across), out var cell)) return cell.Owner;
            return null;
        }

        /// <summary>How many blocks of the wall stand with ground on both faces: the mouth it bars.</summary>
        public int Mouth(BoardGround ground)
        {
            var mouth = 0;
            for (var step = 0; step < (int)Math.Round(Length); step++)
                if (ground.Filled.ContainsKey(At(step + 0.5, -0.5)) && ground.Filled.ContainsKey(At(step + 0.5, 0.5))) mouth++;
            return mouth;
        }
    }

    private static bool Authored(StampId stamp) => string.IsNullOrEmpty(stamp.Kind) || stamp.Image == 0;

    // Two rectangles touch when they overlap or share a stretch of edge; meeting at a corner is not touching.
    private static bool Touches(Rect a, Rect b)
    {
        var acrossX = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        var acrossZ = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        return acrossX >= 0 && acrossZ >= 0 && !(acrossX == 0 && acrossZ == 0);
    }

    private static IEnumerable<(int, int)> Cells(Rect rect)
    {
        for (var x = (int)Math.Floor(rect.MinX); x < (int)Math.Ceiling(rect.MaxX); x++)
            for (var z = (int)Math.Floor(rect.MinZ); z < (int)Math.Ceiling(rect.MaxZ); z++)
                yield return (x, z);
    }

    // A piece and the owner of the ground it meets, where a group or a room owns it.
    private static string[] Subjects(string piece, string owner) => owner == SketchGround.GroundOwner ? [piece] : [piece, owner];

    private static string Named(string owner) => owner == SketchGround.GroundOwner ? "ground no group draws" : $"'{owner}'";

    private static string Beyond(int value, int low, int high) =>
        value < low ? $"less than {low} blocks" : $"more than {high} blocks";

    private static Finding Complaint(string rule, string message, params string[] subjects) =>
        new(rule, message, Severity.Complaint, Subjects: subjects);

    private static Finding Complaint(string rule, string message, Measurement measured, params string[] subjects) =>
        new(rule, message, Severity.Complaint, Subjects: subjects, Measured: measured);
}
