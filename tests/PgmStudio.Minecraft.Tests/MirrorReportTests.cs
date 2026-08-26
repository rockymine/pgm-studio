using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Reading a board's own symmetry off its blocks. What every case asserts is the count, not the picture: a
/// render that draws something is not evidence, and the number is the thing a reader acts on. The invariant
/// throughout is that a column pairs when the column its orbit lands on holds the same solid spans — so a
/// world built symmetric reports zero, and one block moved reports exactly the columns that block touched.
/// Material is the second count and the same discipline: a column that stands where its image stands and is
/// made of something else is <c>Repainted</c>, and a team's own colour is not that.
/// </summary>
public sealed class MirrorReportTests
{
    private static MirrorReport.Result Read(VoxelWorld world, string mode = "rot_180")
        => MirrorReport.Render(AnvilRegion.FromWorld(world), mode)!;

    /// <summary>A pair of columns that are images of each other about the cell boundary at 0.</summary>
    private static void Pair(VoxelWorld world, int x, int z, int top)
    {
        for (var y = 0; y <= top; y++)
        {
            world.SetBlock(x, y, z, 1);
            world.SetBlock(-1 - x, y, -1 - z, 1);
        }
    }

    [Test]
    public async Task A_board_built_about_the_cell_boundary_reports_nothing_unpaired()
    {
        var world = new VoxelWorld();
        for (var z = 0; z < 6; z++) for (var x = 0; x < 6; x++) Pair(world, x, z, 8 + x);

        var read = Read(world);

        await Assert.That(read.Unpaired).IsEqualTo(0);
        await Assert.That(read.Columns).IsEqualTo(72);
    }

    [Test]
    public async Task A_board_built_about_the_origin_is_reported_unpaired_throughout()
    {
        // The defect, as a board: every column mirrored to -x instead of -1-x. It is exactly the case an
        // eye cannot see and this render exists for, so it must not come back clean.
        var world = new VoxelWorld();
        for (var z = 1; z <= 6; z++)
        for (var x = 1; x <= 6; x++)
            for (var y = 0; y <= 8 + x; y++) { world.SetBlock(x, y, z, 1); world.SetBlock(-x, y, -z, 1); }

        var read = Read(world);

        await Assert.That(read.Unpaired).IsGreaterThan(0);
    }

    [Test]
    public async Task One_block_added_to_one_side_unpairs_exactly_that_column_and_its_image()
    {
        var world = new VoxelWorld();
        for (var z = 0; z < 6; z++) for (var x = 0; x < 6; x++) Pair(world, x, z, 8);
        await Assert.That(Read(world).Unpaired).IsEqualTo(0);

        world.SetBlock(2, 20, 3, 1);                       // a block on one half and not the other

        var read = Read(world);

        // The column itself and the column it should have matched — a fault is always a pair, because the
        // question is symmetric.
        await Assert.That(read.Unpaired).IsEqualTo(2);
    }

    [Test]
    public async Task A_column_of_the_same_height_in_a_different_material_pairs_by_shape_and_not_by_material()
    {
        // The two counts are separate answers to separate questions: the ground is the same ground, and the
        // finish over it is not the same finish.
        var world = new VoxelWorld();
        for (var y = 0; y <= 8; y++) { world.SetBlock(3, y, 4, 1); world.SetBlock(-4, y, -5, 4); }

        var read = Read(world);

        await Assert.That(read.Unpaired).IsEqualTo(0);
        await Assert.That(read.Repainted).IsEqualTo(2);
    }

    [Test]
    public async Task A_team_colour_is_not_a_difference_in_material()
    {
        // A red wall facing a blue one is what a team tint is for, so the data of a dyed block is not read.
        var world = new VoxelWorld();
        for (var y = 0; y <= 8; y++) { world.SetBlock(3, y, 4, 35, 14); world.SetBlock(-4, y, -5, 35, 11); }

        var read = Read(world);

        await Assert.That(read.Unpaired).IsEqualTo(0);
        await Assert.That(read.Repainted).IsEqualTo(0);
    }

    [Test]
    public async Task A_column_that_does_not_pair_by_shape_is_not_counted_a_second_time_for_its_material()
    {
        // One fault, one count: a column standing where its image does not has no material to compare.
        var world = new VoxelWorld();
        for (var y = 0; y <= 8; y++) world.SetBlock(3, y, 4, 1);
        for (var y = 0; y <= 5; y++) world.SetBlock(-4, y, -5, 4);

        var read = Read(world);

        await Assert.That(read.Unpaired).IsEqualTo(2);
        await Assert.That(read.Repainted).IsEqualTo(0);
    }

    [Test]
    public async Task A_mode_with_no_orbit_pairs_every_column_with_itself()
    {
        // An unmirrored map is not a broken map, so "none" must report clean rather than report everything.
        var world = new VoxelWorld();
        for (var y = 0; y <= 8; y++) world.SetBlock(17, y, 3, 1);

        await Assert.That(Read(world, "none").Unpaired).IsEqualTo(0);
    }

    [Test]
    public async Task A_quarter_turn_asks_all_three_images()
    {
        // Under rot_90 a column pairs only when the whole orbit closes on it, so a board that is merely
        // half-turn symmetric must not pass as a quarter-turn one.
        var world = new VoxelWorld();
        for (var z = 0; z < 5; z++) for (var x = 0; x < 5; x++) Pair(world, x, z, 8);

        await Assert.That(Read(world, "rot_180").Unpaired).IsEqualTo(0);
        await Assert.That(Read(world, "rot_90").Unpaired).IsGreaterThan(0);
    }

    [Test]
    public async Task An_empty_world_reads_as_nothing_rather_than_as_a_fault()
    {
        await Assert.That(MirrorReport.Render(AnvilRegion.FromWorld(new VoxelWorld()), "rot_180")).IsNull();
    }
}
