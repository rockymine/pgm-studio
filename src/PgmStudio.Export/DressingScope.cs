using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Export;

/// <summary>
/// What the dressing pass needs from a map rather than from a world: what the author placed, how the map is
/// mirrored, and which cells nothing may be placed on.
///
/// <para>Unlike <see cref="TerrainThemeScope"/>, there is no scope to resolve here — a prop is not a recipe
/// applied to a footprint, it is a thing standing at a position, so reading it is reading a list. The work
/// that remains is the mask: everything the map is played through has to come back bare.</para>
/// </summary>
public static class DressingScope
{
    /// <summary>Everything the author placed. An empty list is what a map that never opened the phase carries,
    /// and it makes the pass a no-op.</summary>
    public static IReadOnlyList<PlacedProp> PropsOf(string layoutJson)
    {
        var dressing = SketchLayout.Parse(layoutJson)?.Dressing;
        return dressing is null ? [] : DressingJson.Deserialize(dressing.Value.GetRawText()).Props;
    }

    /// <summary>The map's symmetry, as the dressing pass reads it — the frame every prop is fanned through.</summary>
    public static DressingSymmetry SymmetryOf(string layoutJson)
    {
        var setup = SketchLayout.Parse(layoutJson)?.Setup;
        return new DressingSymmetry(setup?.MirrorMode, setup?.Center?.Cx ?? 0, setup?.Center?.Cz ?? 0);
    }

    /// <summary>What the pass must leave bare, and what each cell is being kept for. Three sources, and each
    /// matters for its own reason: the <b>intent</b> names what the map is played through — spawns,
    /// objectives, the structures stamped for them — and a prop there would block a route or bury a goal; the
    /// <b>world</b> shows what is already standing, and a column whose surface is not the terrain's own is a
    /// structure the pass has no business planting on; and the <b>approach</b> in front of every door is the
    /// lane players walk out through (<see cref="ApproachAt"/>), which is part of what the door is for.
    /// <para>Read from the finished world rather than re-derived, which is the same argument that puts the pass
    /// after the painter: the answer is already there to be looked at.</para>
    /// <para>Not the map contract's <c>protection</c>. That is a region rule about what a player may enter and
    /// break; this is a keep-out with no gameplay meaning of its own, and one word for both is what invited the
    /// inference that a goal must stand somewhere protected. It <em>reads</em> a spawn's protection areas,
    /// because ground a player may not enter is ground a prop has no business standing on — but what it
    /// answers is the other thing.</para></summary>
    public static Func<int, int, KeepOut?> KeptClearAt(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, MapIntent intent, int margin = 2)
    {
        var blocked = new Dictionary<(int X, int Z), KeepOut>();

        void Keep(int x, int z, KeepOut why)
        {
            for (var dz = -margin; dz <= margin; dz++)
            for (var dx = -margin; dx <= margin; dx++)
                blocked[(x + dx, z + dz)] = why;
        }

        void KeepRect(int minX, int minZ, int maxX, int maxZ, KeepOut why)
        {
            for (var z = minZ - margin; z <= maxZ + margin; z++)
            for (var x = minX - margin; x <= maxX + margin; x++)
                blocked[(x, z)] = why;
        }

        void KeepArea(Rect rect, KeepOut why)
            => KeepRect((int)Math.Floor(rect.MinX), (int)Math.Floor(rect.MinZ),
                        (int)Math.Ceiling(rect.MaxX), (int)Math.Ceiling(rect.MaxZ), why);

        foreach (var spawn in intent.Spawns)
        {
            Keep((int)spawn.Point.X, (int)spawn.Point.Z, KeepOut.Spawn);
            foreach (var area in spawn.Protection) KeepArea(area, KeepOut.Spawn);
        }
        foreach (var wool in intent.Wools ?? [])
        {
            Keep((int)wool.Spawn.X, (int)wool.Spawn.Z, KeepOut.WoolRoom);
            foreach (var area in wool.Room) KeepArea(area, KeepOut.WoolRoom);
            foreach (var monument in wool.Monuments) Keep((int)monument.Location.X, (int)monument.Location.Z, KeepOut.WoolRoom);
        }
        // A destroyable and a core are deliberately absent from this mask. They are not ground the pass must
        // avoid — grass and flowers belong under a floating monument as much as anywhere — and what they do
        // ask for is a different and wider thing: see GoalGroundAt.

        if (intent.Structures is { } structures)
        {
            foreach (var floor in structures.RoomFloors) KeepArea(floor.Area, KeepOut.Structure);
            foreach (var cube in structures.IronCubes) KeepRect(cube.X - 2, cube.Z - 2, cube.X + 2, cube.Z + 2, KeepOut.Structure);
            foreach (var wall in structures.Walls) KeepRect(wall.MinX, wall.MinZ, wall.MaxX, wall.MaxZ, KeepOut.Structure);
            foreach (var line in structures.RedstoneLines) KeepRect(Math.Min(line.X1, line.X2), Math.Min(line.Z1, line.Z2), Math.Max(line.X1, line.X2), Math.Max(line.Z1, line.Z2), KeepOut.Structure);
        }

        // A column whose top block is not terrain is a stamp — a room floor, an approach wall, a monument. The
        // painter skips those for the same reason (TP6), and the dressing pass has even less business there.
        foreach (var (cell, top) in surfaceTop)
            if (top <= 1 || DressingPalette.IsStamp(world.GetBlock(cell.X, top - 1, cell.Z).Id)) Keep(cell.X, cell.Z, KeepOut.Built);

        // The doors' approaches, held as rects rather than expanded: a board has a handful of doored faces and
        // each covers hundreds of cells, so the question is asked per candidate cell rather than per lane block.
        var approach = ApproachAt(intent);

        return (x, z) => blocked.TryGetValue((x, z), out var why) ? why
            : approach(x, z) ? KeepOut.Approach
            : null;
    }

    /// <summary>The cells the map is <b>played between</b>: every spawn, every wool room and its monuments,
    /// and every destroyable and core at its anchor. What the ways through the board are measured between
    /// (<c>DR-WAY</c>), and deliberately the same set a coverage read walks its journeys over — a corridor is
    /// only a corridor because someone travels it, so the two must not disagree about who travels.</summary>
    public static IReadOnlyList<(int X, int Z)> WaypointsOf(MapIntent intent)
    {
        var cells = new List<(int X, int Z)>();
        void At(double x, double z) => cells.Add(((int)Math.Floor(x), (int)Math.Floor(z)));

        foreach (var spawn in intent.Spawns) At(spawn.Point.X, spawn.Point.Z);
        foreach (var wool in intent.Wools ?? [])
        {
            At(wool.Spawn.X, wool.Spawn.Z);
            foreach (var monument in wool.Monuments) At(monument.Location.X, monument.Location.Z);
        }
        foreach (var destroyable in intent.Destroyables ?? []) At(destroyable.Anchor.X, destroyable.Anchor.Z);
        foreach (var core in intent.Cores ?? []) At(core.Anchor.X, core.Anchor.Z);
        return [.. cells.Distinct()];
    }

    /// <summary>How far cover must stay off a goal, beyond the ground the structure itself covers. An
    /// objective is the one thing on a map that wants its approach legible — a defender needs to see what is
    /// coming and an attacker needs to pay something visible for arriving — so the clearance is what makes the
    /// goal a place rather than a thing hidden in a wood.</summary>
    public const int GoalClearance = 4;

    /// <summary>The ground read against a destroyable or a core: every block its structure covers, grown by
    /// <see cref="GoalClearance"/>.
    ///
    /// <para>Every rect comes from the box the stamper wrote where there is one, for the same reason the
    /// emitted region does (OB8) — the ground kept open is then the ground the structure occupies, by
    /// construction rather than by two derivations agreeing. <see cref="ObjectiveFootprint"/> answers for an
    /// intent that has not been through the world build, which is the plan-side and preview case.</para>
    ///
    /// <para>Separate from <see cref="KeptClearAt"/> because it answers a different question. That mask says
    /// what may not be <em>placed</em>; this says what may not be <em>hidden behind</em>, so ground cover
    /// crosses it freely and only cover is turned away.</para></summary>
    public static Func<int, int, bool> GoalGroundAt(MapIntent intent, int clearance = GoalClearance)
    {
        var rects = new List<(int MinX, int MinZ, int MaxX, int MaxZ)>();

        foreach (var destroyable in intent.Destroyables ?? [])
        {
            DestroyableStyles.TryParse(destroyable.Style, out var style);
            var (width, _, depth) = ObjectiveFootprint.Destroyable(style);
            rects.Add(Ground(destroyable.Box, destroyable.Anchor, width, depth));
        }
        foreach (var core in intent.Cores ?? [])
        {
            var (width, depth) = ObjectiveFootprint.Core(core.Size > 0 ? core.Size : ObjectiveDefaults.CoreSize);
            rects.Add(Ground(core.Box, core.Anchor, width, depth));
        }

        // Held as rects rather than expanded into a cell set: a goal's clearance is a handful of boxes, and the
        // question is asked once per candidate cell rather than per block of ground.
        return (x, z) => rects.Any(r => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ);

        (int, int, int, int) Ground(BlockBox? stamped, Pt anchor, int width, int depth)
        {
            var (minX, minZ, maxX, maxZ) = stamped is { } box
                ? (box.MinX, box.MinZ, box.MaxX, box.MaxZ)
                : ObjectiveFootprint.Centred(anchor.X, anchor.Z, width, depth);
            return (minX - clearance, minZ - clearance, maxX + clearance, maxZ + clearance);
        }
    }

    /// <summary>How far a tree, boulder or building keeps from a goal's <b>marker</b>, beyond the
    /// footprint-grown <see cref="GoalClearance"/> that turns cover away: ten blocks (the author's number).
    /// A goal is a small floating thing, so the ground that makes it playable is the ring a fight happens
    /// on, and that ring is measured from the marker rather than from however wide the structure under it
    /// happens to be.</summary>
    public const int GoalStandoff = 10;

    /// <summary>How far the ground in front of a spawn room's door stays clear of props: twenty blocks out
    /// from the stamped building's own face (the author's number) — the building, deliberately not the
    /// protection region projected around it. The lane players walk out through is part of what the door is
    /// for.</summary>
    public const int SpawnApproach = 20;

    /// <summary>The wool room's version of <see cref="SpawnApproach"/>: ten blocks out from each entry face
    /// (the author's number) — shorter because a wool's approach is meant to be contested, and what is being
    /// kept is legibility rather than a safe walk.</summary>
    public const int WoolApproach = 10;

    /// <summary>The ground no prop may take in front of a door: one rectangle per doored face, the face's own
    /// width, reaching <see cref="SpawnApproach"/> out from a spawn room and <see cref="WoolApproach"/> from
    /// each wool entry. Read from the same room resolution the stamper builds
    /// (<see cref="WorldBuilder.SpawnRoom"/>/<see cref="WorldBuilder.WoolFrame"/>), so the approach
    /// is measured from the building that actually stands, not from a second derivation.
    /// <para>Part of <see cref="KeptClearAt"/>, which is what makes it a keep-out rather than a refusal: a prop
    /// authored into a lane does not land, and the pass says so. Every kind is turned away, boulders included —
    /// a boulder is tall enough that the low-cover sightline argument does not hold for it.</para></summary>
    public static Func<int, int, bool> ApproachAt(MapIntent intent)
    {
        var rects = new List<(int MinX, int MinZ, int MaxX, int MaxZ)>();

        foreach (var spawn in intent.Spawns)
            AddFrontages(rects, WorldBuilder.SpawnRoom(spawn).Frame, SpawnApproach);
        foreach (var wool in intent.Wools ?? [])
            AddFrontages(rects, WorldBuilder.WoolFrame(wool), WoolApproach);

        return (x, z) => rects.Any(r => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ);
    }

    // One frontage rect per doored face: the face's full width, `reach` blocks outward. Per face rather than
    // per door, because the approach is the lane in front of the wall, not a corridor the width of the cut.
    private static void AddFrontages(
        List<(int MinX, int MinZ, int MaxX, int MaxZ)> rects, RoomFrame frame, int reach)
    {
        foreach (var edge in frame.Doors.Select(door => door.Edge).Distinct())
            rects.Add(edge switch
            {
                RoomEdge.PosX => (frame.MaxX + 1, frame.MinZ, frame.MaxX + reach, frame.MaxZ),
                RoomEdge.NegX => (frame.MinX - reach, frame.MinZ, frame.MinX - 1, frame.MaxZ),
                RoomEdge.PosZ => (frame.MinX, frame.MaxZ + 1, frame.MaxX, frame.MaxZ + reach),
                _ => (frame.MinX, frame.MinZ - reach, frame.MaxX, frame.MinZ - 1),
            });
    }

    /// <summary>The ground a placed prop may not rest on: a goal's own ground (<see cref="GoalGroundAt"/>)
    /// plus the marker's <see cref="GoalStandoff"/> square. Wider than the cover mask and asked of different
    /// things — a tree, a boulder or a building standing here is <c>OB19</c> and is declined by the pass, so
    /// the finding names the prop that dropped and the map still exports. Ground cover crosses this ground
    /// freely: only the tall kind turns away, and only at the narrower <see cref="GoalClearance"/>.</summary>
    public static Func<int, int, bool> GoalClearanceAt(MapIntent goals)
        => EitherOf(GoalGroundAt(goals), GoalDiscsAt(goals));

    // The marker-centred half of a goal's prop keep-out: a GoalStandoff square around each anchor.
    private static Func<int, int, bool> GoalDiscsAt(MapIntent intent)
    {
        var rects = new List<(int MinX, int MinZ, int MaxX, int MaxZ)>();
        foreach (var destroyable in intent.Destroyables ?? [])
            rects.Add(Disc(destroyable.Anchor));
        foreach (var core in intent.Cores ?? [])
            rects.Add(Disc(core.Anchor));
        return (x, z) => rects.Any(r => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ);

        static (int, int, int, int) Disc(Pt anchor)
        {
            int anchorX = (int)Math.Floor(anchor.X), anchorZ = (int)Math.Floor(anchor.Z);
            return (anchorX - GoalStandoff, anchorZ - GoalStandoff, anchorX + GoalStandoff, anchorZ + GoalStandoff);
        }
    }

    private static Func<int, int, bool> EitherOf(Func<int, int, bool> first, Func<int, int, bool> second)
        => (x, z) => first(x, z) || second(x, z);

    // The three prop kinds a clearance turns away; everything else (a path, a channel, a flower field) is
    // generated rather than authored the same way, and flora's own clearance rule lives in Decorator.
    private static string? ClearanceKind(PlacedProp prop) => prop switch
    {
        TreeProp => "tree",
        BoulderProp => "boulder",
        HouseProp => "building",
        _ => null,
    };

    /// <summary>Every dressing-placed tree's anchor and measured canopy radius, fanned across the map's
    /// symmetry orbit — the authored points the point-and-radius foliage render draws (§6,
    /// <c>docs/world-export/decoration.md</c>) rather than the leaf mass a build writes. The radius is
    /// <see cref="Decorator.CanopyRadius"/>: the same deterministic generation the stamp itself runs, read for
    /// its geometry, so a caller here never re-derives a shape a real build could still disagree with — and
    /// never needs a built world at all, since a tree's crown is a pure function of its own fields and seed.</summary>
    public static IReadOnlyList<(int X, int Z, double Radius)> TreeFootprints(string layoutJson)
    {
        var symmetry = SymmetryOf(layoutJson);
        var points = new List<(int X, int Z, double Radius)>();
        foreach (var prop in PropsOf(layoutJson))
        {
            if (prop is not TreeProp tree) continue;
            var radius = Decorator.CanopyRadius(tree);
            for (var image = 0; image < symmetry.Order; image++)
            {
                var (x, z) = symmetry.ImageCell(tree.X, tree.Z, image);
                points.Add((x, z, radius));
            }
        }
        return points;
    }

    /// <summary>Every cell the dressing decorates, fanned across the orbit — tree and boulder anchors and
    /// building footprints, the same cells the clearance checks read. What the coverage measure calls
    /// decorated ground: scenery a player at least looks at, whether or not a route passes it.</summary>
    public static IReadOnlyList<(int X, int Z)> DecorCells(string layoutJson)
    {
        var symmetry = SymmetryOf(layoutJson);
        var cells = new List<(int X, int Z)>();
        foreach (var prop in PropsOf(layoutJson))
        {
            if (ClearanceKind(prop) is null) continue;
            for (var image = 0; image < symmetry.Order; image++)
                cells.AddRange(ClearanceFootprint(prop, symmetry, image));
        }
        return cells;
    }

    // The footprint each prop kind roots or covers, turned to one image of the orbit: a single point for a
    // tree or a boulder — the cell a trunk stands on is what matters here, the same resting cell Decorator's
    // own Seats reads, not the crown that may freely overhang a goal — and the whole rectangle for a building,
    // since its floor covers every column beneath it. Deliberately the wall rectangle alone rather than
    // <see cref="HouseStamper.StampedExtent"/>: OB19 asks whether a building stands too close to a goal, which
    // is a question about the room someone drew, not about how far its eaves happen to overhang it.
    private static IEnumerable<(int X, int Z)> ClearanceFootprint(PlacedProp prop, DressingSymmetry symmetry, int image)
    {
        switch (prop)
        {
            case TreeProp tree:
                yield return symmetry.ImageCell(tree.X, tree.Z, image);
                break;
            case BoulderProp boulder:
                yield return symmetry.ImageCell(boulder.X, boulder.Z, image);
                break;
            case HouseProp house:
                if (house.Plan() is { } plan)
                    foreach (var cell in Decorator.TurnedFootprint(plan, symmetry, image).Cells())
                        yield return cell;
                break;
        }
    }
}
