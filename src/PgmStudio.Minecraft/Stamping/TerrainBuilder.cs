using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Stamping;

/// <summary>The synthesised terrain plus, per footprint cell, its surface top (the first air Y above the
/// solid column) — the reference structures rest on and spawns snap to.</summary>
/// <param name="SurfaceTop">The first air Y above each column, over the whole board — where a thing placed
/// at a cell stands when it names no layer, and what every stamper snaps to.</param>
/// <param name="SurfaceByLayer">The same read taken one layer at a time, layer id → its own cells' tops. A
/// stacked board has a surface per storey and the whole-board answer only ever names the highest, so this is
/// what a pass addressing a particular storey — painting it, seating something on it — reads instead.</param>
/// <param name="FloorByLayer">Where each layer's own span starts, cell by cell — the other end of the pair
/// above. Only a pass painting a layer that is not ground reads it: terrain's bands run from the bedrock
/// course whatever its floor, and a made thing's run over its own span.</param>
/// <param name="PropLayers">Which of those layers are made things rather than ground, so
/// <see cref="Ground"/> can leave them out. A board that names none reads the same as one that has none.</param>
public sealed record BuiltTerrain(
    VoxelWorld World,
    IReadOnlyDictionary<(int X, int Z), int> SurfaceTop,
    IReadOnlyDictionary<string, IReadOnlyDictionary<(int X, int Z), int>> SurfaceByLayer,
    IReadOnlyDictionary<string, IReadOnlyDictionary<(int X, int Z), int>> FloorByLayer,
    IReadOnlySet<string>? PropLayers = null)
{
    private IReadOnlyDictionary<(int X, int Z), int>? ground;

    /// <summary><b>Where the ground is</b> — <see cref="SurfaceTop"/> with the made things taken out, and the
    /// one answer everything that RESTS on the board reads: a room's floor, a goal's box and the plate under
    /// it, a wall, a build-region marker, the world spawn.
    ///
    /// <para><see cref="SurfaceTop"/> answers a different question — the highest thing standing at a cell —
    /// and by now that is nobody's: a cloud drawn at y78 over a car park is the top of every column under it,
    /// so a goal reading it stood on the cloud, eighty-three blocks up and over a build ceiling it had itself
    /// pushed nowhere near. Read it for the cells it covers; read this for a height.</para></summary>
    public IReadOnlyDictionary<(int X, int Z), int> Ground =>
        ground ??= PropLayers is { Count: > 0 } made ? SurfaceExcept(made) : SurfaceTop;

    /// <summary>The ground a thing placed on <paramref name="layer"/> stands on: that layer's own surfaces,
    /// or — for a placement naming no layer, and for one naming a layer this board does not have —
    /// <see cref="Ground"/>. Naming the layer is how a monument sits in a hall instead of on the deck roofing
    /// it; naming none takes the board's ground, which is where everything already authored goes.</summary>
    public IReadOnlyDictionary<(int X, int Z), int> SurfaceFor(string? layer) =>
        layer is { Length: > 0 } named && SurfaceByLayer.TryGetValue(named, out var tops) ? tops : Ground;

    /// <summary>The board's surface with <paramref name="layers"/> taken out of the read — the tops of
    /// everything else, cell by cell. A made thing is not ground: a balloon flying thirty blocks over a field
    /// is the highest thing standing at those cells and therefore <see cref="SurfaceTop"/>'s answer for them,
    /// which puts the ground under it out of reach of every pass that decorates terrain. Naming the prop
    /// layers here is what leaves that ground readable.</summary>
    public IReadOnlyDictionary<(int X, int Z), int> SurfaceExcept(IReadOnlySet<string> layers)
    {
        if (layers.Count == 0) return SurfaceTop;
        var surface = new Dictionary<(int X, int Z), int>(SurfaceTop.Count);
        foreach (var (layer, tops) in SurfaceByLayer)
        {
            if (layers.Contains(layer)) continue;
            foreach (var (cell, top) in tops)
                if (!surface.TryGetValue(cell, out var known) || top > known) surface[cell] = top;
        }
        return surface;
    }

    /// <summary>Whether <paramref name="layer"/> is a layer this board has ground on. A placement naming one
    /// it does not is a placement the world cannot seat where it was asked to.</summary>
    public bool Knows(string? layer) => layer is not { Length: > 0 } || SurfaceByLayer.ContainsKey(layer);
}

/// <summary>
/// Turns <c>SketchRasterizer.RasterizeColumns</c> output — <see cref="ColumnSegment"/> solid runs — into
/// a voxel world: a bedrock floor at y=0 under every column whose ground reaches it, stone filling each segment's
/// <c>[YFloor, YTop)</c> span above it. Materials are deliberately flat (bedrock + stone) for now. Stacked
/// disjoint segments per cell (e.g. ground + a sky bridge) each fill independently; the surface top is the
/// tallest segment's <c>YTop</c>.
/// </summary>
public static class TerrainBuilder
{
    public static BuiltTerrain Build(IEnumerable<ColumnSegment> columns, IReadOnlySet<string>? propLayers = null)
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>();
        var byLayer = new Dictionary<string, Dictionary<(int X, int Z), int>>();
        var floorOf = new Dictionary<string, Dictionary<(int X, int Z), int>>();
        var grounded = new HashSet<(int X, int Z)>();

        foreach (var (x, z, yFloor, yTop, layer) in columns)
        {
            var lo = Math.Max(1, yFloor);                          // y=0 is reserved for the bedrock floor
            var hi = Math.Min(VoxelWorld.MaxHeight, yTop);
            for (var y = lo; y < hi; y++) world.SetBlock(x, y, z, Blocks.Stone);

            // The floor goes under a column that rests on it — one whose own floor is the bedrock course or
            // the first block over it — and nowhere else. A slab across open void, a bridge over a strait or
            // a deck overhanging a court stands on nothing, so plating it would put a floor under the fall
            // and add the column to the Y0 set a void filter reads. A one-thick slab at floor 0 writes no
            // stone at all and the bedrock is its whole ground, which is why this reads the floor rather
            // than what the fill wrote.
            if (yFloor <= 1) grounded.Add((x, z));
            AddSurface(surface, x, z, yTop);

            if (!byLayer.TryGetValue(layer, out var tops)) byLayer[layer] = tops = [];
            AddSurface(tops, x, z, yTop);

            if (!floorOf.TryGetValue(layer, out var floors)) floorOf[layer] = floors = [];
            var start = Math.Clamp(yFloor, 0, VoxelWorld.MaxHeight);
            if (!floors.TryGetValue((x, z), out var known) || start < known) floors[(x, z)] = start;
        }

        foreach (var (x, z) in grounded) world.SetBlock(x, 0, z, Blocks.Bedrock);

        return new BuiltTerrain(world, surface,
            byLayer.ToDictionary(entry => entry.Key,
                                 entry => (IReadOnlyDictionary<(int X, int Z), int>)entry.Value),
            floorOf.ToDictionary(entry => entry.Key,
                                 entry => (IReadOnlyDictionary<(int X, int Z), int>)entry.Value),
            propLayers);
    }

    /// <summary>Just the per-cell surface tops of <paramref name="columns"/> — the same map <see cref="Build"/>
    /// produces, without filling a world. For callers that only need to know where the terrain's surface is
    /// (structure floors), for which the voxel fill is pure cost.</summary>
    public static IReadOnlyDictionary<(int X, int Z), int> SurfaceTops(IEnumerable<ColumnSegment> columns)
    {
        var surface = new Dictionary<(int X, int Z), int>();
        foreach (var segment in columns) AddSurface(surface, segment.X, segment.Z, segment.YTop);
        return surface;
    }

    private static void AddSurface(Dictionary<(int X, int Z), int> surface, int x, int z, int yTop)
    {
        var top = Math.Clamp(yTop, 1, VoxelWorld.MaxHeight);
        if (!surface.TryGetValue((x, z), out var cur) || top > cur) surface[(x, z)] = top;
    }
}
