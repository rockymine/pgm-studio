using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What a broken block leaves in an inventory that a map removes (<see cref="BlockDrops"/>): every non-full block
/// and every item that is not a block, never a full block dropping itself.
/// </summary>
public sealed class BlockDropsTests
{
    /// <summary>The non-full blocks that leave no clutter: fire, piston heads and the moving piece, a cake, the
    /// end portal and its frame and the dragon egg drop nothing, and an ender chest drops obsidian, a full
    /// block.</summary>
    private static readonly HashSet<int> Dropless = [34, 36, 51, 92, 119, 120, 122, 130];

    [Test]
    public async Task A_full_block_dropping_itself_is_never_clutter()
    {
        // stone, grass, dirt, cobblestone, planks, log, wool, sandstone, stone brick, double slab
        foreach (var id in new[] { 1, 2, 3, 4, 5, 17, 35, 24, 98, 43 })
            await Assert.That(BlockDrops.Clutter(id).Any()).IsFalse();
    }

    [Test]
    public async Task Every_non_full_block_is_clutter_unless_it_drops_nothing()
    {
        var missing = Enumerable.Range(1, 197)
            .Where(id => !BlockRoles.IsFullCube(id) && !BlockRoles.IsLiquid(id) && !Dropless.Contains(id))
            .Where(id => !BlockDrops.Clutter(id).Any())
            .ToList();
        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task A_full_block_dropping_an_item_yields_the_item()
    {
        await Assert.That(BlockDrops.Clutter(13)).IsEquivalentTo(new[] { "flint" });           // gravel
        await Assert.That(BlockDrops.Clutter(18)).IsEquivalentTo(new[] { "sapling", "apple" }); // oak leaves
        await Assert.That(BlockDrops.Clutter(73)).IsEquivalentTo(new[] { "redstone" });         // redstone ore
    }

    [Test]
    public async Task A_world_lists_each_item_once()
    {
        // tall grass and wheat both drop seeds; two signs, two diodes
        var clutter = BlockDrops.Clutter([31, 59, 63, 68, 93, 94, 1, 3]);
        await Assert.That(clutter.Count).IsEqualTo(clutter.Distinct().Count());
        await Assert.That(clutter).Contains("seeds");
        await Assert.That(clutter).Contains("sign");
        await Assert.That(clutter).Contains("diode");
    }
}
