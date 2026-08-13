using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

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
}
