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
            var top = Math.Clamp(yTop, 1, VoxelWorld.MaxHeight);
            if (!surface.TryGetValue((x, z), out var cur) || top > cur) surface[(x, z)] = top;
        }

        foreach (var (x, z) in footprint) world.SetBlock(x, 0, z, Blocks.Bedrock);

        return new SketchTerrain(world, surface);
    }
}
