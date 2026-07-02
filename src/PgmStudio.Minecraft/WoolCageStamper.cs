namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps a complete wool cage (docs/contracts/sketch-world-export.md §2): the wool-cage cube shell
/// (colour = the room's wool colour) plus the corner chest loadout. Anchored on the (integer-snapped) wool
/// spawn point, resting on the terrain surface.
/// </summary>
public static class WoolCageStamper
{
    public static void Stamp(VoxelWorld world, int anchorX, int anchorZ, int floorY, int color)
    {
        CubeStamper.Stamp(world, anchorX, anchorZ, floorY, color, CubeKind.WoolCage);
        WoolCageChests.Stamp(world, anchorX, anchorZ, floorY);
    }
}
