using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Reading a built world back as per-column runs, and the two membership sets projected off them. The
/// invariant under every run case: a run covers a span that is solid throughout and one material throughout,
/// and the runs of a column partition exactly the solid blocks in it — so what these assert is where the
/// boundaries fall, not how many there happen to be. The membership cases assert the projection rather than a
/// second walk: what is in a set follows from the runs the same world reports.
/// </summary>
public sealed class WorldColumnsTests
{
    private static IReadOnlyList<ColumnRun> At(VoxelWorld world, int x, int z, BlockBox? within = null)
        => WorldColumns.Of(world, within).Where(column => column.X == x && column.Z == z)
                       .Select(column => column.Runs).FirstOrDefault() ?? [];

    [Test]
    public async Task One_material_to_the_surface_is_one_run()
    {
        var world = new VoxelWorld();
        for (var y = 0; y <= 63; y++) world.SetBlock(4, y, 6, 1);

        var runs = At(world, 4, 6);

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(63, 0, 1, 0));
    }

    [Test]
    public async Task A_material_change_breaks_the_run_where_it_changes()
    {
        var world = new VoxelWorld();
        for (var y = 0; y <= 60; y++) world.SetBlock(0, y, 0, 1);    // stone
        for (var y = 61; y <= 63; y++) world.SetBlock(0, y, 0, 3);   // dirt over it

        var runs = At(world, 0, 0);

        // Top first, and the two spans touch: the dirt's bottom is one above the stone's top.
        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(63, 61, 3, 0));
        await Assert.That(runs[1]).IsEqualTo(new ColumnRun(60, 0, 1, 0));
    }

    [Test]
    public async Task The_data_nibble_breaks_a_run_the_same_way_the_id_does()
    {
        var world = new VoxelWorld();
        world.SetBlock(2, 10, 2, 35, 14);   // red wool
        world.SetBlock(2, 11, 2, 35, 5);    // green wool, same id

        var runs = At(world, 2, 2);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(11, 11, 35, 5));
        await Assert.That(runs[1]).IsEqualTo(new ColumnRun(10, 10, 35, 14));
    }

    [Test]
    public async Task A_span_floating_over_ground_keeps_both_of_its_parts()
    {
        var world = new VoxelWorld();
        for (var y = 0; y <= 40; y++) world.SetBlock(7, y, 7, 1);     // the ground
        for (var y = 60; y <= 62; y++) world.SetBlock(7, y, 7, 1);    // a bridge over it, same material

        var runs = At(world, 7, 7);

        // Same material either side of the air, so only the gap can separate them — the case a top-only
        // read loses entirely.
        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(62, 60, 1, 0));
        await Assert.That(runs[1]).IsEqualTo(new ColumnRun(40, 0, 1, 0));
    }

    [Test]
    public async Task A_run_survives_the_section_boundary_it_crosses()
    {
        // Sections are 16 blocks tall and allocated one at a time; a column filled across three of them is
        // still one span and must not be reported as three.
        var world = new VoxelWorld();
        for (var y = 20; y <= 60; y++) world.SetBlock(1, y, 1, 1);

        var runs = At(world, 1, 1);

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(60, 20, 1, 0));
    }

    [Test]
    public async Task A_column_at_the_height_extremes_is_read_whole()
    {
        var world = new VoxelWorld();
        world.SetBlock(3, 0, 3, 7);                            // bedrock on the floor
        world.SetBlock(3, VoxelWorld.MaxHeight - 1, 3, 1);      // and a block at the ceiling

        var runs = At(world, 3, 3);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(255, 255, 1, 0));
        await Assert.That(runs[1]).IsEqualTo(new ColumnRun(0, 0, 7, 0));
    }

    [Test]
    public async Task An_empty_world_reads_as_no_columns()
    {
        await Assert.That(WorldColumns.Of(new VoxelWorld()).Any()).IsFalse();
    }

    [Test]
    public async Task A_column_holding_only_air_is_not_reported()
    {
        // Touching a chunk allocates it; nothing solid in the column means nothing to say about it.
        var world = new VoxelWorld();
        world.SetBlock(0, 10, 0, 1);

        var columns = WorldColumns.Of(world).ToList();

        await Assert.That(columns.Count).IsEqualTo(1);
        await Assert.That(columns[0].X).IsEqualTo(0);
        await Assert.That(columns[0].Z).IsEqualTo(0);
    }

    [Test]
    public async Task Negative_coordinates_read_back_at_the_positions_they_were_written()
    {
        var world = new VoxelWorld();
        world.SetBlock(-1, 5, -1, 1);
        world.SetBlock(-20, 5, -33, 1);

        var positions = WorldColumns.Of(world).Select(column => (column.X, column.Z)).ToList();

        await Assert.That(positions).Contains((-1, -1));
        await Assert.That(positions).Contains((-20, -33));
    }

    [Test]
    public async Task A_box_restricts_the_footprint_it_reads()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 10, 0, 1);
        world.SetBlock(30, 10, 30, 1);

        var inside = WorldColumns.Of(world, new BlockBox(0, 0, 0, 15, 255, 15))
                                 .Select(column => (column.X, column.Z)).ToList();

        await Assert.That(inside.Count).IsEqualTo(1);
        await Assert.That(inside[0]).IsEqualTo((0, 0));
    }

    [Test]
    public async Task A_box_clips_a_run_to_its_own_height_rather_than_dropping_it()
    {
        var world = new VoxelWorld();
        for (var y = 0; y <= 63; y++) world.SetBlock(5, y, 5, 1);

        var runs = At(world, 5, 5, new BlockBox(0, 20, 0, 15, 40, 15));

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0]).IsEqualTo(new ColumnRun(40, 20, 1, 0));
    }

    [Test]
    public async Task The_same_world_reads_back_in_the_same_order_twice()
    {
        var world = new VoxelWorld();
        foreach (var (x, z) in new[] { (40, 40), (-16, 8), (0, 0), (17, -3) }) world.SetBlock(x, 10, z, 1);

        var first = WorldColumns.Of(world).Select(column => (column.X, column.Z)).ToList();
        var second = WorldColumns.Of(world).Select(column => (column.X, column.Z)).ToList();

        await Assert.That(first).IsEquivalentTo(second);
    }

    [Test]
    public async Task Every_solid_block_lands_in_exactly_one_run()
    {
        // The partition invariant, checked against the world itself rather than against a hand-counted
        // expectation: a stack with gaps, repeats and a material change in it.
        var world = new VoxelWorld();
        var solid = new HashSet<int>();
        foreach (var (from, to, id) in new[] { (0, 12, 1), (13, 20, 3), (34, 36, 1), (50, 50, 20) })
            for (var y = from; y <= to; y++) { world.SetBlock(9, y, 9, id); solid.Add(y); }

        var covered = new List<int>();
        foreach (var run in At(world, 9, 9))
            for (var y = run.YBottom; y <= run.YTop; y++)
            {
                covered.Add(y);
                await Assert.That(world.GetBlock(9, y, 9)).IsEqualTo((run.BlockId, run.BlockData));
            }

        await Assert.That(covered.Count).IsEqualTo(solid.Count);          // nothing counted twice
        await Assert.That(covered.ToHashSet()).IsEquivalentTo(solid);     // and nothing missed
    }

    [Test]
    public async Task Surface_holds_exactly_the_columns_the_runs_report()
    {
        // The projection invariant: the membership set cannot know about a column the run read does not
        // yield, and cannot miss one it does.
        var world = new VoxelWorld();
        foreach (var (x, z, y) in new[] { (0, 0, 10), (-5, 12, 0), (33, -7, 200), (16, 16, 64) })
            world.SetBlock(x, y, z, 1);

        var (surface, _) = WorldColumns.Membership(world);

        await Assert.That(surface).IsEquivalentTo(
            WorldColumns.Of(world).Select(column => (column.X, column.Z)).ToHashSet());
    }

    [Test]
    public async Task A_column_that_does_not_reach_the_floor_is_surface_but_not_Y0()
    {
        // The case that separates the two sets: a mass floating over void carries a surface a player stands
        // on and no block on the layer the void filter reads.
        var world = new VoxelWorld();
        for (var y = 40; y <= 44; y++) world.SetBlock(2, y, 3, 1);

        var (surface, y0) = WorldColumns.Membership(world);

        await Assert.That(surface).Contains((2, 3));
        await Assert.That(y0).DoesNotContain((2, 3));
    }

    [Test]
    public async Task A_column_grounded_under_a_gap_is_in_both_sets()
    {
        // Reading the topmost run would answer the bridge and miss the floor; the lowest run is what says
        // whether the column reaches Y=0 at all.
        var world = new VoxelWorld();
        for (var y = 0; y <= 30; y++) world.SetBlock(8, y, 9, 1);      // ground from the floor up
        for (var y = 55; y <= 57; y++) world.SetBlock(8, y, 9, 1);     // and a bridge over it

        var (surface, y0) = WorldColumns.Membership(world);

        await Assert.That(surface).Contains((8, 9));
        await Assert.That(y0).Contains((8, 9));
    }

    [Test]
    public async Task An_empty_world_has_no_columns_in_either_set()
    {
        var (surface, y0) = WorldColumns.Membership(new VoxelWorld());

        await Assert.That(surface.Count).IsEqualTo(0);
        await Assert.That(y0.Count).IsEqualTo(0);
    }
}
