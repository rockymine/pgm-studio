using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps the wool-cage loot: two stacked chests in each of the four interior corners of the cube
/// (docs/contracts/sketch-world-export.md §2a). The lower chest (A) holds a row of planks ×16, a row of
/// Speed I (3:00) potions, and a row of golden apples ×16; the upper chest (B) holds a row of diamond
/// leggings, a row of Power I + Infinity bows, and a row of planks ×16. Anchored on the same
/// <c>(anchorX, anchorZ)</c> / <c>floorY</c> as <see cref="CubeStamper"/>.
/// </summary>
public static class WoolCageChests
{
    private const int SpeedPotion183 = 8194;   // 1.8 Potion of Swiftness (Speed I, 3:00) metadata value
    private const int EnchPower = 48;
    private const int EnchInfinity = 51;

    public static void Stamp(VoxelWorld world, int anchorX, int anchorZ, int floorY)
    {
        var x0 = anchorX - 4;
        var z0 = anchorZ - 4;

        // Interior corners (local 1..6 is the hollow; its corners are (1,1),(1,6),(6,1),(6,6)).
        foreach (var (lx, lz) in (ReadOnlySpan<(int, int)>)[(1, 1), (1, 6), (6, 1), (6, 6)])
        {
            var wx = x0 + lx;
            var wz = z0 + lz;
            var bottomY = floorY + 1;   // resting on the bedrock floor
            var topY = floorY + 2;

            PlaceChest(world, wx, bottomY, wz, ChestA());
            PlaceChest(world, wx, topY, wz, ChestB());
        }
    }

    private static void PlaceChest(VoxelWorld world, int x, int y, int z, IEnumerable<(int, NbtCompound)> items)
    {
        world.SetBlock(x, y, z, Blocks.Chest, 2);   // data 2 = facing north; functional regardless
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
