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
    public async Task A_projection_shows_the_wall_the_plane_misses_and_the_slice_does_not()
    {
        // A room whose floor lies on the cut and whose walls stand three blocks behind it: the exact case a
        // one-plane cut reads as floor, air, roof.
        var world = new VoxelWorld();
        for (var x = 0; x <= 6; x++) world.SetBlock(x, 5, 20, Blocks.Stone);          // the floor, on the cut
        for (var y = 6; y <= 9; y++) world.SetBlock(0, y, 23, Blocks.Stone);          // walls, three behind
        for (var y = 6; y <= 9; y++) world.SetBlock(6, y, 23, Blocks.Stone);

        var slice = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 6, fixedCoord: 20,
            yMin: 4, yMax: 10)!;
        var projected = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 6,
            fixedCoord: 20, yMin: 4, yMax: 10, depth: 4)!;

        // Row 0 is the highest course. The wall tops at y9 are one row down from y10.
        static bool Painted(SectionRender.Result result, int column, int y)
        {
            var row = result.HighestY - y;
            var at = (row * result.Width + column) * 3;
            return (result.Pixels[at] << 16 | result.Pixels[at + 1] << 8 | result.Pixels[at + 2]) is not 0xE7ECF3
                and not 0x0E0E12;
        }

        await Assert.That(Painted(slice, 0, 9)).IsFalse().Because("one plane cannot see a wall behind it");
        await Assert.That(Painted(projected, 0, 9)).IsTrue();
        await Assert.That(Painted(projected, 6, 9)).IsTrue();
        await Assert.That(projected.Depth).IsEqualTo(4);
    }

    [Test]
    public async Task What_stands_on_the_cut_keeps_its_own_colour_and_what_is_behind_is_dimmed()
    {
        // The same block at the plane and four behind it: hue is the material either way and only the value
        // moves, which is what lets a reader name the material of something in the background.
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 20, Blocks.Stone);
        world.SetBlock(1, 5, 24, Blocks.Stone);

        var projected = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 1,
            fixedCoord: 20, yMin: 5, yMax: 5, depth: 4)!;
        var slice = SectionRender.Render(AnvilRegion.FromWorld(world), SectionAxis.AlongX, 0, 1,
            fixedCoord: 20, yMin: 5, yMax: 5, depth: 0)!;

        static (int R, int G, int B) At(SectionRender.Result result, int column)
        {
            var offset = column * 3;
            return (result.Pixels[offset], result.Pixels[offset + 1], result.Pixels[offset + 2]);
        }

        await Assert.That(At(projected, 0)).IsEqualTo(At(slice, 0))
            .Because("a block on the cut reads exactly as the slice draws it");
        var (nearR, nearG, nearB) = At(projected, 0);
        var (farR, farG, farB) = At(projected, 1);
        await Assert.That(farR).IsLessThan(nearR);
        await Assert.That(farG).IsLessThan(nearG);
        await Assert.That(farB).IsLessThan(nearB);
        // Dimmed, not washed out: the ratios hold, so the material is still nameable from the picture.
        await Assert.That(Math.Abs((double)farR / nearR - (double)farG / nearG)).IsLessThan(0.05);
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
