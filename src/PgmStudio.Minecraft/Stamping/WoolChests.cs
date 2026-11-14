using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stamps the wool-room loot: a stack of two chests on each <see cref="ChestStack"/> the room holds
/// (<see cref="RoomFrames.Interior"/> — the four interior corners where its house states none;
/// docs/world-export/sketch-world-export.md §2a). The lower chest (A) holds a row of planks ×16, a row of
/// Speed I (3:00) potions, and a row of golden apples ×16; the upper chest (B) holds a row of diamond
/// leggings, a row of Power I + Infinity bows, and a row of planks ×16.
/// </summary>
public static class WoolChests
{
    private const int SpeedPotion183 = 8194;   // 1.8 Potion of Swiftness (Speed I, 3:00) metadata value
    private const int EnchPower = 48;
    private const int EnchInfinity = 51;

    public static void Stamp(VoxelWorld world, IReadOnlyList<ChestStack> chests, int floorY)
    {
        foreach (var chest in chests)
        {
            var facing = BlockGeometry.Fronting(chest.Facing);
            ChestBuilder.Place(world, chest.X, floorY + 1, chest.Z, facing, ChestA());   // resting on the bedrock floor
            ChestBuilder.Place(world, chest.X, floorY + 2, chest.Z, facing, ChestB());
        }
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
