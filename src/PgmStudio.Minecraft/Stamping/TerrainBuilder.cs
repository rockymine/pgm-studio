using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Stamping;

/// <summary>The synthesised terrain plus, per footprint cell, its surface top (the first air Y above the
/// solid column) — the reference structures rest on and spawns snap to.</summary>
public sealed record BuiltTerrain(VoxelWorld World, IReadOnlyDictionary<(int X, int Z), int> SurfaceTop);

/// <summary>
/// Turns <c>SketchRasterizer.RasterizeColumns</c> output — <see cref="ColumnSegment"/> solid runs — into
/// a voxel world: a bedrock floor at y=0 under every column whose ground reaches it, stone filling each segment's
/// <c>[YFloor, YTop)</c> span above it. Materials are deliberately flat (bedrock + stone) for now. Stacked
/// disjoint segments per cell (e.g. ground + a sky bridge) each fill independently; the surface top is the
/// tallest segment's <c>YTop</c>.
/// </summary>
public static class TerrainBuilder
{
    public static BuiltTerrain Build(IEnumerable<ColumnSegment> columns)
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>();
        var grounded = new HashSet<(int X, int Z)>();

        foreach (var (x, z, yFloor, yTop, _) in columns)
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
        }

        foreach (var (x, z) in grounded) world.SetBlock(x, 0, z, Blocks.Bedrock);

        return new BuiltTerrain(world, surface);
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
