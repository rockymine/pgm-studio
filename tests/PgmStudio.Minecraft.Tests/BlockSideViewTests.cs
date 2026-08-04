using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// A world seen from the side. The case that matters is the one a cut gets wrong: a projection answers "what
/// would you see", so a shape with gaps between its parts still reads as the shape rather than as whichever
/// holes one row happened to fall through.
/// </summary>
public sealed class BlockSideViewTests
{
    [Test]
    public async Task The_nearest_block_is_what_is_seen()
    {
        var world = new VoxelWorld();
        world.SetBlock(3, 10, 5, Blocks.Wool, 14);      // behind
        world.SetBlock(3, 10, 1, Blocks.Grass);         // in front of it

        var view = BlockSideView.Project(world, 0, 7, 0, 7, 8, 12);
        await Assert.That(view.At(3, 10)?.Id).IsEqualTo(Blocks.Grass);
        await Assert.That(view.At(4, 10)).IsNull();     // nothing in that column at all
    }

    [Test]
    public async Task Depth_runs_from_the_front_plane_to_the_back()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 9, 0, Blocks.Stone);
        world.SetBlock(1, 9, 7, Blocks.Stone);
        world.SetBlock(2, 9, 3, Blocks.Stone);

        var view = BlockSideView.Project(world, 0, 7, 0, 7, 8, 10);
        await Assert.That(view.At(0, 9)?.Depth).IsEqualTo(0);
        await Assert.That(view.At(1, 9)?.Depth).IsEqualTo(1);
        await Assert.That(view.At(2, 9)!.Value.Depth).IsEqualTo(3 / 7.0).Within(1e-9);
    }

    [Test]
    public async Task A_blocks_shade_says_where_that_block_is_and_nothing_else()
    {
        // Depth is measured against the box, not against the depths the view happened to reach. Were it the
        // latter, adding one block at the back would re-shade every block already in the picture — and two
        // cards of one picker set, drawn from the same box, could not be compared.
        var world = new VoxelWorld();
        world.SetBlock(0, 9, 2, Blocks.Stone);

        var alone = BlockSideView.Project(world, 0, 3, 0, 8, 8, 10);
        world.SetBlock(1, 9, 8, Blocks.Stone);            // something further back joins it
        var joined = BlockSideView.Project(world, 0, 3, 0, 8, 8, 10);

        await Assert.That(joined.At(0, 9)?.Depth).IsEqualTo(alone.At(0, 9)?.Depth);
        await Assert.That(joined.At(0, 9)!.Value.Depth).IsEqualTo(2 / 8.0).Within(1e-9);
    }

    [Test]
    public async Task A_shape_with_gaps_between_its_parts_still_reads_as_the_shape()
    {
        // Two blocks a row apart, the way a crown's clusters sit: a cut through either row sees one of them
        // and calls the other empty. Every projected column that carries something is what the eye would see.
        var world = new VoxelWorld();
        world.SetBlock(2, 12, 1, DressingPalette.Leaves);
        world.SetBlock(3, 12, 4, DressingPalette.Leaves);

        var view = BlockSideView.Project(world, 0, 7, 0, 7, 8, 14);
        await Assert.That(view.At(2, 12)).IsNotNull();
        await Assert.That(view.At(3, 12)).IsNotNull();
    }

    [Test]
    public async Task The_highest_course_is_what_a_caller_crops_to()
    {
        var world = new VoxelWorld();
        world.SetBlock(1, 9, 0, Blocks.Stone);
        world.SetBlock(1, 17, 0, DressingPalette.Leaves);

        await Assert.That(BlockSideView.Project(world, 0, 3, 0, 3, 8, 30).Highest()).IsEqualTo(17);
        await Assert.That(BlockSideView.Project(new VoxelWorld(), 0, 3, 0, 3, 8, 30).Highest()).IsNull();
    }

    [Test]
    public async Task Distance_shades_a_block_without_repainting_it()
    {
        // The whole point of keeping the block: a leaf at the back is a darker green, not grey. Same hue,
        // less of it.
        var front = BlockPalette.Hex(Blocks.Grass, 0, 0);
        var back = BlockPalette.Hex(Blocks.Grass, 0, 1);

        await Assert.That(front).IsEqualTo(BlockPalette.Hex(Blocks.Grass, 0));
        await Assert.That(back).IsNotEqualTo(front);

        var (fr, fg, fb) = Channels(front);
        var (br, bg, bb) = Channels(back);
        await Assert.That(br).IsLessThan(fr);
        await Assert.That(bg).IsLessThan(fg);
        await Assert.That(bb).IsLessThan(fb);
        // Still the same colour: the channel that dominated in front still dominates at the back.
        await Assert.That(bg > br && bg > bb).IsEqualTo(fg > fr && fg > fb);
    }

    private static (int R, int G, int B) Channels(string hex)
        => (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
}
