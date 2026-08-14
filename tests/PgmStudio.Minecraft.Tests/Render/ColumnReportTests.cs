using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The textual section read-back: one vertical column, top block to bottom, over an in-memory
/// <see cref="VoxelWorld"/> via <see cref="AnvilRegion.FromWorld"/>.</summary>
public sealed class ColumnReportTests
{
    [Test]
    public async Task A_column_reads_top_to_bottom_with_every_solid_block_named()
    {
        var world = new VoxelWorld();
        world.SetBlock(5, 0, 5, Blocks.Bedrock);
        world.SetBlock(5, 1, 5, Blocks.Stone);
        world.SetBlock(5, 2, 5, Blocks.Dirt);
        world.SetBlock(5, 3, 5, Blocks.Grass);
        // air above — nothing recorded past y3

        var result = ColumnReport.Render(AnvilRegion.FromWorld(world), [(5, 5)]);

        var stack = result[(5, 5)];
        await Assert.That(stack.Count).IsEqualTo(4);
        await Assert.That(stack[0].Y).IsEqualTo(3);   // highest block first
        await Assert.That(stack[^1].Y).IsEqualTo(0);  // bedrock last
        for (var index = 1; index < stack.Count; index++)
            await Assert.That(stack[index].Y).IsLessThan(stack[index - 1].Y);   // strictly descending
        await Assert.That(stack[0].Name).IsEqualTo("Grass Block");
        await Assert.That(stack[^1].Name).IsEqualTo("Bedrock");
    }

    [Test]
    public async Task A_column_with_no_blocks_at_all_is_still_reported_as_an_empty_entry()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);   // some other column, so the world is not empty

        var result = ColumnReport.Render(AnvilRegion.FromWorld(world), [(9, 9)]);

        // A void column is a real finding (a hole nothing covers), not the absence of one — the caller must
        // be able to tell "asked for and found nothing" from "never asked".
        await Assert.That(result.ContainsKey((9, 9))).IsTrue();
        await Assert.That(result[(9, 9)].Count).IsEqualTo(0);
    }

    [Test]
    public async Task Only_the_requested_columns_are_reported()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(1, 5, 0, Blocks.Stone);
        world.SetBlock(2, 5, 0, Blocks.Stone);

        var result = ColumnReport.Render(AnvilRegion.FromWorld(world), [(1, 0)]);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[(1, 0)].Count).IsEqualTo(1);
    }
}
