using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stamps the wool-cage loot: two stacked chests in each of the four interior corners of the room
/// (docs/world-export/sketch-world-export.md §2a). The lower chest (A) holds a row of planks ×16, a row of
/// Speed I (3:00) potions, and a row of golden apples ×16; the upper chest (B) holds a row of diamond
/// leggings, a row of Power I + Infinity bows, and a row of planks ×16. Corners come from the room's
/// <see cref="RoomFrame"/>, so the loadout rides any footprint.
///
/// <para>Each corner touches two walls at once, so opening away from just one of them is not enough — a
/// chest that ignores the second wall still fronts it half the time. The room's own door breaks the tie:
/// every chest opens along the door's axis, away from whichever of its two walls sits on that axis, which
/// is never the door wall itself and always the wall the room was entered past.</para>
/// </summary>
public static class WoolChests
{
    private const int SpeedPotion183 = 8194;   // 1.8 Potion of Swiftness (Speed I, 3:00) metadata value
    private const int EnchPower = 48;
    private const int EnchInfinity = 51;

    public static void Stamp(VoxelWorld world, RoomFrame frame, int floorY)
    {
        var doorEdge = frame.Doors.Count > 0 ? frame.Doors[0].Edge : RoomEdge.NegZ;
        foreach (var (cornerX, cornerZ) in RoomFrames.InteriorCorners(frame))
        {
            var facing = CornerFacing(frame, doorEdge, cornerX, cornerZ);
            PlaceChest(world, cornerX, floorY + 1, cornerZ, facing, ChestA());   // resting on the bedrock floor
            PlaceChest(world, cornerX, floorY + 2, cornerZ, facing, ChestB());
        }
    }

    // The wall a corner touches on the door's own axis decides the facing: away from that wall is always
    // open room on the other side, whichever of the corner's two walls it turns out to be. A door wall that
    // runs along x is a door whose own axis is z, so that is the axis the chest opens on.
    private static int CornerFacing(RoomFrame frame, RoomEdge doorEdge, int cornerX, int cornerZ)
    {
        if (doorEdge.AlongX())
            return BlockGeometry.Fronting(cornerZ == frame.InteriorMinZ ? RoomEdge.PosZ : RoomEdge.NegZ);
        return BlockGeometry.Fronting(cornerX == frame.InteriorMinX ? RoomEdge.PosX : RoomEdge.NegX);
    }

    private static void PlaceChest(VoxelWorld world, int x, int y, int z, int facing, IEnumerable<(int, NbtCompound)> items)
    {
        world.SetBlock(x, y, z, Blocks.Chest, facing);
        world.AddTileEntity(x, z, ChestBuilder.Chest(x, y, z, items));
    }

    private static IEnumerable<(int, NbtCompound)> ChestA() =>
    [
        .. ChestBuilder.Row(0, () => ChestBuilder.Item("minecraft:planks", 16)),
        .. ChestBuilder.Row(1, () => ChestBuilder.Item("minecraft:potion", 1, SpeedPotion183)),
        .. ChestBuilder.Row(2, () => ChestBuilder.Item("minecraft:golden_apple", 16)),
    ];

    private static IEnumerable<(int, NbtCompound)> ChestB() =>
    [
        .. ChestBuilder.Row(0, () => ChestBuilder.Item("minecraft:diamond_leggings", 1)),
        .. ChestBuilder.Row(1, () => ChestBuilder.EnchantedBow((EnchPower, 1), (EnchInfinity, 1))),
        .. ChestBuilder.Row(2, () => ChestBuilder.Item("minecraft:planks", 16)),
    ];
}
