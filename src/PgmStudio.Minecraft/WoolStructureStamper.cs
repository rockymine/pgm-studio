using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps a complete wool structure (docs/contracts/sketch-world-export.md §2): the shell over its
/// <see cref="RoomFrame"/> (colour = the room's wool colour, doors on the frame's entry interfaces) plus
/// the corner chest loadout. Resting on the terrain surface its footprint spans.
/// </summary>
public static class WoolStructureStamper
{
    public static void Stamp(VoxelWorld world, RoomFrame frame, int floorY, int color, RoomStyle? style = null)
    {
        CubeStamper.Stamp(world, frame, floorY, color, style ?? RoomStyle.Wool);
        WoolChests.Stamp(world, frame, floorY);
    }
}
