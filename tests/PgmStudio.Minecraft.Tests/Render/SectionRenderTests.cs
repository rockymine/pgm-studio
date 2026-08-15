using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The vertical-slice stage image, over an in-memory <see cref="VoxelWorld"/> via
/// <see cref="AnvilRegion.FromWorld"/>.</summary>
public sealed class SectionRenderTests
{
    [Test]
    public async Task A_riser_reads_as_a_step_between_two_neighbouring_columns()
    {
        // A flat floor at y5 for z 0..2, then a riser: y5..y8 at z3 — the exact shape no plan-view
        // renderer in this suite can show, because it differs only in Y.
        var world = new VoxelWorld();
        for (var z = 0; z <= 2; z++) world.SetBlock(10, 5, z, Blocks.Stone);
        for (var y = 5; y <= 8; y++) world.SetBlock(10, y, 3, Blocks.Stone);

        var result = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongZ, 0, 3, fixedCoord: 10,
            yMin: 0, yMax: 10);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Width).IsEqualTo(4);     // z 0..3
        await Assert.That(result.Height).IsEqualTo(11);    // y 0..10
        await Assert.That(result.LowestY).IsEqualTo(0);
        await Assert.That(result.HighestY).IsEqualTo(10);
        await Assert.That(result.Columns).IsEqualTo(4);    // every one of the four columns has a block
    }

    [Test]
    public async Task A_column_with_nothing_recorded_counts_as_void()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(0, 5, 4, Blocks.Stone);
        // z 1..3 at x=0 carry no block at all — a gap between two built columns.

        var result = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongZ, 0, 4, fixedCoord: 0,
            yMin: 0, yMax: 10);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.VoidColumns).IsEqualTo(3);
        await Assert.That(result.Columns).IsEqualTo(2);
    }

    [Test]
    public async Task The_two_axes_read_the_same_world_from_their_own_side()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 4; x++) world.SetBlock(x, 5, 7, Blocks.Stone);   // a run along x at fixed z
        for (var z = 0; z <= 4; z++) world.SetBlock(9, 5, z, Blocks.Stone);  // a run along z at fixed x

        var alongX = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 4, fixedCoord: 7,
            yMin: 4, yMax: 6);
        var alongZ = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongZ, 0, 4, fixedCoord: 9,
            yMin: 4, yMax: 6);

        await Assert.That(alongX).IsNotNull();
        await Assert.That(alongX!.Columns).IsEqualTo(5);
        await Assert.That(alongZ).IsNotNull();
        await Assert.That(alongZ!.Columns).IsEqualTo(5);
    }

    [Test]
    public async Task Render_returns_null_when_nothing_lies_on_the_requested_line()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 50, Blocks.Stone);   // real terrain, but not on the cut asked for

        var result = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 4, fixedCoord: 0,
            yMin: 0, yMax: 10);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Pixels_fill_exactly_width_times_height_times_three_bytes()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 2; x++) world.SetBlock(x, 5, 0, Blocks.Stone);

        var result = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 2, fixedCoord: 0,
            yMin: 3, yMax: 7)!;

        await Assert.That(result.Pixels.Length).IsEqualTo(result.Width * result.Height * 3);
    }
}
