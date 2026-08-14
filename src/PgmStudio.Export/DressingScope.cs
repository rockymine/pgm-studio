using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

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

    /// <summary>Cells the pass must leave bare. Two sources, and both matter for different reasons: the
    /// <b>intent</b> names what the map is played through — spawns, objectives, the structures stamped for them
    /// — and a prop there would block a route or bury a goal; the <b>world</b> shows what is already standing,
    /// and a column whose surface is not the terrain's own is a structure the pass has no business planting on.
    /// <para>Read from the finished world rather than re-derived, which is the same argument that puts the pass
    /// after the painter: the answer is already there to be looked at.</para></summary>
    public static Func<int, int, bool> ProtectedAt(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, MapIntent intent, int margin = 2)
    {
        var blocked = new HashSet<(int X, int Z)>();

        void Keep(int x, int z)
        {
            for (var dz = -margin; dz <= margin; dz++)
            for (var dx = -margin; dx <= margin; dx++)
                blocked.Add((x + dx, z + dz));
        }

        void KeepRect(int minX, int minZ, int maxX, int maxZ)
        {
            for (var z = minZ - margin; z <= maxZ + margin; z++)
            for (var x = minX - margin; x <= maxX + margin; x++)
                blocked.Add((x, z));
        }

        void KeepArea(Rect rect)
            => KeepRect((int)Math.Floor(rect.MinX), (int)Math.Floor(rect.MinZ),
                        (int)Math.Ceiling(rect.MaxX), (int)Math.Ceiling(rect.MaxZ));

        foreach (var spawn in intent.Spawns)
        {
            Keep((int)spawn.Point.X, (int)spawn.Point.Z);
            foreach (var area in spawn.Protection) KeepArea(area);
        }
        foreach (var wool in intent.Wools ?? [])
        {
            Keep((int)wool.Spawn.X, (int)wool.Spawn.Z);
            foreach (var area in wool.Room) KeepArea(area);
            foreach (var monument in wool.Monuments) Keep((int)monument.Location.X, (int)monument.Location.Z);
        }
        // A destroyable and a core are deliberately absent from this mask. They are not ground the pass must
        // avoid — grass and flowers belong under a floating monument as much as anywhere — and what they do
        // ask for is a different and wider thing: see GoalGroundAt.

        if (intent.Structures is { } structures)
        {
            foreach (var floor in structures.RoomFloors) KeepArea(floor);
            foreach (var cube in structures.IronCubes) KeepRect(cube.X - 2, cube.Z - 2, cube.X + 2, cube.Z + 2);
            foreach (var wall in structures.Walls) KeepRect(wall.MinX, wall.MinZ, wall.MaxX, wall.MaxZ);
            foreach (var line in structures.RedstoneLines) KeepRect(Math.Min(line.X1, line.X2), Math.Min(line.Z1, line.Z2), Math.Max(line.X1, line.X2), Math.Max(line.Z1, line.Z2));
        }

        // A column whose top block is not terrain is a stamp — a room floor, an approach wall, a monument. The
        // painter skips those for the same reason (TP6), and the dressing pass has even less business there.
        foreach (var (cell, top) in surfaceTop)
            if (top <= 1 || DressingPalette.IsStamp(world.GetBlock(cell.X, top - 1, cell.Z).Id)) Keep(cell.X, cell.Z);

        return (x, z) => blocked.Contains((x, z));
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
    /// <para>Separate from <see cref="ProtectedAt"/> because it answers a different question. That mask says
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

    /// <summary>OB19 — the rule id carried on a goal-clearance finding, so a caller can act on the id rather
    /// than parse the sentence (docs/world-export/decoration.md §3.1).</summary>
    public const string Rule = "OB19";

    /// <summary>Every tree, boulder or building whose footprint reaches into a goal's clearance
    /// (<see cref="GoalGroundAt"/>), fanned across the map's own symmetry exactly as <see cref="Decorator"/>
    /// places it. These three are refused rather than dropped, because they are authored: a caller needs the
    /// one offending prop and the goal it collides with, not a silently discarded placement. Ground cover
    /// crosses this ground freely and is never checked here — only the tall kind turns away from it, and only
    /// inside <see cref="Decorator"/> itself.</summary>
    public static List<(string Kind, string PropId, int X, int Z)> GoalClearanceViolations(
        string layoutJson, MapIntent goals)
    {
        var isGoalGround = GoalGroundAt(goals);
        var symmetry = SymmetryOf(layoutJson);
        var violations = new List<(string Kind, string PropId, int X, int Z)>();

        foreach (var prop in PropsOf(layoutJson))
        {
            var kind = ClearanceKind(prop);
            if (kind is null) continue;
            if (FirstClearanceHit(prop, symmetry, isGoalGround) is { } cell)
                violations.Add((kind, prop.Id, cell.X, cell.Z));
        }
        return violations;
    }

    // The three prop kinds this refusal covers; everything else (a path, a channel, a flower field) is
    // generated rather than authored the same way, and flora's own clearance rule already lives in Decorator.
    private static string? ClearanceKind(PlacedProp prop) => prop switch
    {
        TreeProp => "tree",
        BoulderProp => "boulder",
        HouseProp => "building",
        _ => null,
    };

    private static (int X, int Z)? FirstClearanceHit(
        PlacedProp prop, DressingSymmetry symmetry, Func<int, int, bool> isGoalGround)
    {
        for (var image = 0; image < symmetry.Order; image++)
            foreach (var cell in ClearanceFootprint(prop, symmetry, image))
                if (isGoalGround(cell.X, cell.Z))
                    return cell;
        return null;
    }

    /// <summary>Every dressing-placed building's <b>stamped extent</b> — the wall footprint grown by
    /// <see cref="HouseStamper.StampedExtent"/>, not the wall rectangle alone — fanned across the map's
    /// symmetry orbit, for the build's provenance record (<c>SketchWorldBuilder</c>) to claim as structure
    /// regardless of what the house is built from. A roof's overhang and a verge lay past the walls by design,
    /// and a column the stamper never claimed reads by material alone, which is exactly the eaves reading as
    /// foliage when a style's verge is a log. Trees, boulders and flora are absent on purpose: their
    /// material already reads unambiguously (a log, a leaf, a liquid), so provenance has nothing to correct
    /// there — it exists for the Ground/Structure pair a material test can get wrong.</summary>
    public static IEnumerable<(int X, int Z)> StructureFootprints(string layoutJson)
    {
        var symmetry = SymmetryOf(layoutJson);
        foreach (var prop in PropsOf(layoutJson))
        {
            if (prop is not HouseProp house) continue;
            for (var image = 0; image < symmetry.Order; image++)
                foreach (var cell in StampedFootprint(house, symmetry, image))
                    yield return cell;
        }
    }

    /// <summary>One image of a house's stamped extent — the wall rectangle turned round the orbit and then
    /// grown by <see cref="HouseStamper.StampedExtent"/>, in that order, so a quarter turn swaps width and
    /// depth before the margin is added and a non-square house's eaves come out right on every image.</summary>
    private static IEnumerable<(int X, int Z)> StampedFootprint(HouseProp house, DressingSymmetry symmetry, int image)
    {
        var corners = symmetry.ImageRing(house.Points, image);
        if (new HouseProp { Points = corners }.Footprint() is not { } wall) yield break;
        var (minX, minZ, maxX, maxZ) = HouseStamper.StampedExtent(
            (wall.MinX, wall.MinZ, wall.MinX + wall.Width - 1, wall.MinZ + wall.Depth - 1), house.Style);
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            yield return (x, z);
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
                var corners = symmetry.ImageRing(house.Points, image);
                if (new HouseProp { Points = corners }.Footprint() is { } footprint)
                    for (var z = footprint.MinZ; z < footprint.MinZ + footprint.Depth; z++)
                    for (var x = footprint.MinX; x < footprint.MinX + footprint.Width; x++)
                        yield return (x, z);
                break;
        }
    }
}
