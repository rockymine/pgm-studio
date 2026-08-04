using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>
/// Resolves the three things the dressing pass needs from a map rather than from a world: which recipe governs
/// each cell, which cells nothing may be placed on, and how the map is mirrored. The sibling of
/// <see cref="TerrainThemeScope"/> — dressing is scoped on the sketch geometry for the same reason paint is,
/// so reshaping a shape moves its planting with it.
/// </summary>
public static class DressingScope
{
    /// <summary>The per-cell recipe resolver. Returns a constant map-default resolver when nothing is dressed,
    /// so the common path allocates nothing per cell and a map that never opened the phase dresses nothing.</summary>
    public static Func<int, int, DressingRecipe> RecipeAt(string layoutJson)
    {
        var layout = SketchLayout.Parse(layoutJson);

        var recipes = new Dictionary<string, DressingRecipe>();
        foreach (var (id, json) in layout?.Dressings ?? new())
            recipes[id] = DressingJson.Deserialize(json.GetRawText());

        var mapDefault = layout?.MapDressing is { } md && recipes.TryGetValue(md, out var mapRecipe)
            ? mapRecipe : DressingRecipe.Bare;

        if (recipes.Count == 0) return (_, _) => mapDefault;

        var shapeRecipe = new Dictionary<string, DressingRecipe>();
        foreach (var shapes in ShapeLists(layout))
            foreach (var shape in shapes)
                if (shape.Dressing is { } id && recipes.TryGetValue(id, out var recipe)) shapeRecipe[shape.Id] = recipe;

        if (shapeRecipe.Count == 0) return (_, _) => mapDefault;

        var cellToShape = SketchRasterizer.ShapeDressingOwners(layoutJson);
        return (x, z) => cellToShape.TryGetValue((x, z), out var shapeId)
            && shapeRecipe.TryGetValue(shapeId, out var recipe) ? recipe : mapDefault;
    }

    /// <summary>The map's symmetry, as the dressing pass reads it — the frame gameplay-affecting props are
    /// generated on and fanned through.</summary>
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
        foreach (var destroyable in intent.Destroyables ?? []) Keep((int)destroyable.Anchor.X, (int)destroyable.Anchor.Z);
        foreach (var core in intent.Cores ?? []) Keep((int)core.Anchor.X, (int)core.Anchor.Z);

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
            if (top <= 1 || IsStructureBlock(world.GetBlock(cell.X, top - 1, cell.Z).Id)) Keep(cell.X, cell.Z);

        return (x, z) => blocked.Contains((x, z));
    }

    // The painter leaves only its own materials on a terrain column; anything else standing there was stamped.
    private static bool IsStructureBlock(int blockId) => blockId is Blocks.Bedrock or Blocks.Obsidian
        or Blocks.Wool or Blocks.GoldBlock or Blocks.IronBlock or Blocks.EmeraldBlock or Blocks.Chest
        or Blocks.StainedGlass or Blocks.StainedGlassPane or Blocks.Air;

    private static IEnumerable<List<SketchShape>> ShapeLists(SketchLayout? layout)
    {
        if (layout?.Layers is { Count: > 0 } layers)
            foreach (var layer in layers) yield return layer.Layout?.Shapes ?? [];
        else if (layout?.Layout is { } single) yield return single.Shapes;
    }
}
