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
/// (<c>ST8</c>), and the ground ahead of a spawn's door (<c>SP9</c>). Each is the rule the plan is asked under
/// the same id, measured in blocks; a piece is asked once, by its authored image.
/// </summary>
public static class SketchSeamCheck
{
    /// <summary>The rules asked here, which a map's plan no longer answers once its sketch is asked.</summary>
    public static readonly IReadOnlySet<string> Asks = new HashSet<string>(
    [
        LayoutRules.CorridorWidth, LayoutRules.ZoneTouchesSpawn, LayoutRules.StitchedZones,
        LayoutRules.WaterLaneOverGround, PlanRules.WallOnWoolRoom, PlanRules.WallAtJunction,
        LayoutRules.ApproachWallEdgeLength, LayoutRules.SpawnDoorGround,
    ]);

    /// <summary>The ground a spawn's door must open onto before bare void, in blocks.</summary>
    public const int DoorGroundBlocks = 15;

    /// <summary>What the intent's pieces break against the ground the layout builds; nothing where it builds
    /// no ground.</summary>
    public static Findings Check(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
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
        }

        foreach (var spawn in intent.Spawns.Where(spawn => Authored(spawn.Stamp) && spawn.Protection.Count > 0))
            if (spawn.DoorEdges() is [var door, ..] && Ahead(spawn, door, ground) is var ahead && ahead < DoorGroundBlocks)
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

    /// <summary>A wall's line, sampled in blocks along it and half a block out on each face.</summary>
    private readonly struct Line(WallStructure wall)
    {
        private readonly double x1 = wall.X1, z1 = wall.Z1, dx = wall.X2 - wall.X1, dz = wall.Z2 - wall.Z1;

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
}
