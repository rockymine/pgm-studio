namespace PgmStudio.Minecraft;

/// <summary>The synthesised terrain plus, per footprint cell, its surface top (the first air Y above the
/// solid column) — the reference structures rest on and spawns snap to.</summary>
public sealed record SketchTerrain(VoxelWorld World, IReadOnlyDictionary<(int X, int Z), int> SurfaceTop);

/// <summary>
/// Turns <c>SketchRasterizer.RasterizeColumns</c> output — <c>(X, Z, YFloor, YTop)</c> solid segments — into
/// a voxel world: a bedrock floor at y=0 under the whole footprint, stone filling each segment's
/// <c>[YFloor, YTop)</c> span above it. Materials are deliberately flat (bedrock + stone) for now. Stacked
/// disjoint segments per cell (e.g. ground + a sky bridge) each fill independently; the surface top is the
/// tallest segment's <c>YTop</c>.
/// </summary>
public static class SketchTerrainBuilder
{
    public static SketchTerrain Build(IEnumerable<(int X, int Z, int YFloor, int YTop)> columns)
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>();
        var footprint = new HashSet<(int X, int Z)>();

        foreach (var (x, z, yFloor, yTop) in columns)
        {
            var lo = Math.Max(1, yFloor);                          // y=0 is reserved for the bedrock floor
            var hi = Math.Min(VoxelWorld.MaxHeight, yTop);
            for (var y = lo; y < hi; y++) world.SetBlock(x, y, z, Blocks.Stone);

            footprint.Add((x, z));
            AddSurface(surface, x, z, yTop);
        }

        foreach (var (x, z) in footprint) world.SetBlock(x, 0, z, Blocks.Bedrock);

        return new SketchTerrain(world, surface);
    }

    /// <summary>Just the per-cell surface tops of <paramref name="columns"/> — the same map <see cref="Build"/>
    /// produces, without filling a world. For callers that only need to know where the terrain's surface is
    /// (structure floors), for which the voxel fill is pure cost.</summary>
    public static IReadOnlyDictionary<(int X, int Z), int> SurfaceTops(
        IEnumerable<(int X, int Z, int YFloor, int YTop)> columns)
    {
        var surface = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, yTop) in columns) AddSurface(surface, x, z, yTop);
        return surface;
    }

    private static void AddSurface(Dictionary<(int X, int Z), int> surface, int x, int z, int yTop)
    {
        var top = Math.Clamp(yTop, 1, VoxelWorld.MaxHeight);
        if (!surface.TryGetValue((x, z), out var cur) || top > cur) surface[(x, z)] = top;
    }
}
