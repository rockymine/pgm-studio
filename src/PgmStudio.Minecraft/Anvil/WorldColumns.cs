namespace PgmStudio.Minecraft.Anvil;

/// <summary>
/// The column sets an analysis reads off a built world held in memory: which columns carry standing terrain,
/// and which carry a block at Y=0 (the layer PGM's void filter reads). The same two sets the segment scan
/// derives from region files — answered here from the <see cref="VoxelWorld"/> directly, for the headless
/// driver that has the world in hand and no scan to load.
/// </summary>
public static class WorldColumns
{
    public static (HashSet<(int X, int Z)> Surface, HashSet<(int X, int Z)> Y0) Of(VoxelWorld world)
    {
        var surface = new HashSet<(int X, int Z)>();
        var y0 = new HashSet<(int X, int Z)>();
        foreach (var ((chunkX, chunkZ), chunk) in world.Chunks)
            for (var localZ = 0; localZ < 16; localZ++)
            for (var localX = 0; localX < 16; localX++)
            {
                int x = chunkX * 16 + localX, z = chunkZ * 16 + localZ;
                for (var section = 0; section < 16 && !surface.Contains((x, z)); section++)
                {
                    var ids = chunk.Ids[section];
                    if (ids is null) continue;
                    for (var localY = 0; localY < 16; localY++)
                        if (ids[(localY << 8) | (localZ << 4) | localX] != 0)
                        {
                            surface.Add((x, z));
                            break;
                        }
                }
                if (chunk.Ids[0] is { } floor && floor[(localZ << 4) | localX] != 0) y0.Add((x, z));
            }
        return (surface, y0);
    }
}
