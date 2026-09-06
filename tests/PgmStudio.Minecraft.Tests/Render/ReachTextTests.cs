using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>
/// Which standing ground no player can get to. The reading is the traversability picture's own partition —
/// the navigable components, less the one the board is played on, less every component a marker sits on, less
/// every component the map opens to bridging — and it names nothing as wrong: scenery and a side observer
/// island read exactly like a shape stranded by accident, which is why the answer goes to the author rather
/// than to a gate.
/// </summary>
public sealed class ReachTextTests
{
    /// <summary>A board and a second patch of ground with nothing between them.</summary>
    private static VoxelWorld TwoIslands()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 20; x++)
            for (var z = 0; z <= 4; z++) world.SetBlock(x, 5, z, Blocks.Stone);
        for (var x = 40; x <= 44; x++)
            for (var z = 0; z <= 2; z++) world.SetBlock(x, 5, z, Blocks.Stone);
        return world;
    }

    private static TraversabilityRender.Result Read(VoxelWorld world, int? ceiling = null,
                                                    IReadOnlyList<TraversabilityRender.Marker>? markers = null)
        => TraversabilityRender.Render(AnvilRegion.FromWorld(world), markers ?? [], null, ceiling)!;

    [Test]
    public async Task A_second_island_nothing_reaches_is_reported_with_its_box()
    {
        var read = Read(TwoIslands());
        var patch = read.OutOfReach.Single();

        await Assert.That(patch.Reason).IsEqualTo("no-build-zone");
        await Assert.That(patch.Cells).IsEqualTo(5 * 3);
        await Assert.That(patch.Floor).IsEqualTo(6)
            .Because("the floor is the course a player stands ON, which is one above the block");
        await Assert.That(patch.MinX).IsEqualTo(40);
        await Assert.That(patch.MaxX).IsEqualTo(44);
    }

    /// <summary>The main board is where the game is played, so it is never the thing reported.</summary>
    [Test]
    public async Task The_board_itself_is_never_out_of_reach()
    {
        var read = Read(TwoIslands());
        await Assert.That(read.OutOfReach.Any(patch => patch.MinX == 0)).IsFalse();
    }

    /// <summary>A spawn or an objective cut off from the board is the connectivity rule's to report. Saying it
    /// here as well would state one fault in two vocabularies, which teaches a reader to believe neither.</summary>
    [Test]
    public async Task A_patch_carrying_a_marker_is_left_to_the_connectivity_rule()
    {
        var marker = new TraversabilityRender.Marker(
            new BlockBox(40, 5, 0, 44, 6, 2), "spawn-red", 0xff0000);

        var read = Read(TwoIslands(), markers: [marker]);

        await Assert.That(read.OutOfReach).IsEmpty();
        await Assert.That(read.IsolatedCount).IsEqualTo(1).Because("the marker is still cut off, and says so");
    }

    /// <summary>Ground over the ceiling cannot be built up to, whatever else is true of it.</summary>
    [Test]
    public async Task Ground_above_the_build_ceiling_is_out_of_reach_for_that_reason()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 20; x++)
            for (var z = 0; z <= 4; z++) world.SetBlock(x, 5, z, Blocks.Stone);
        for (var x = 40; x <= 44; x++)
            for (var z = 0; z <= 2; z++) world.SetBlock(x, 90, z, Blocks.Stone);

        await Assert.That(Read(world, ceiling: 64).OutOfReach.Single().Reason).IsEqualTo("above-ceiling");
        // With no ceiling stated the same shelf is still out of reach, for the other reason.
        await Assert.That(Read(world).OutOfReach.Single().Reason).IsEqualTo("no-build-zone");
    }

    [Test]
    public async Task A_board_everything_reaches_says_so_rather_than_answering_nothing()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 20; x++)
            for (var z = 0; z <= 4; z++) world.SetBlock(x, 5, z, Blocks.Stone);

        var text = ReachText.Render(Read(world), maxBuildHeight: null);

        await Assert.That(text).Contains("every patch of standing ground");
        await Assert.That(ReachText.Summary(Read(world))).IsEqualTo("reach: every patch reachable");
    }

    [Test]
    public async Task The_text_carries_the_box_the_reader_stands_in_and_names_no_fault()
    {
        var read = Read(TwoIslands());
        var text = ReachText.Render(read, maxBuildHeight: null);

        await Assert.That(text).Contains("x 40..44");
        await Assert.That(text).Contains("no-build-zone");
        await Assert.That(text).Contains("None of this is a fault");
        await Assert.That(ReachText.Summary(read)).Contains("1 patch(es) out of reach");
    }
}
