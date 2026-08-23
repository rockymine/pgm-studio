using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The one traversal every distance is measured with: eight-connected octile distance, a climb priced in the
/// blocks a player places, void bridged at one block a cell, a fall counted and not priced, water slowed,
/// and two aims that can disagree about which route is best.
/// </summary>
public sealed class WalkTests
{
    private static HashSet<(int X, int Z)> Rect(int x0, int z0, int width, int height)
    {
        var cells = new HashSet<(int X, int Z)>();
        for (var x = x0; x < x0 + width; x++)
            for (var z = z0; z < z0 + height; z++)
                cells.Add((x, z));
        return cells;
    }

    /// <summary>Flat ground of one height, nothing to bridge, no water.</summary>
    private static WalkGround Flat(int width, int height, int surface = 10)
    {
        var ground = Rect(0, 0, width, height);
        return new WalkGround(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => surface), new CellRect(0, 0, width, height));
    }

    [Test]
    public async Task A_straight_run_is_its_own_length_in_blocks()
    {
        var path = Walk.Between((0, 0), (10, 0), Flat(20, 5));
        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Cost.Distance).IsEqualTo(10);
        await Assert.That(path.Cost.Blocks).IsEqualTo(0);
    }

    [Test]
    public async Task A_diagonal_run_costs_the_root_two_a_player_walks_not_the_staircase()
    {
        // Ten cells across and ten down: an axis-aligned walk reports 20, a player walks 14.1.
        var path = Walk.Between((0, 0), (10, 10), Flat(20, 20));
        await Assert.That(path!.Cost.Distance).IsEqualTo(14);
    }

    [Test]
    public async Task A_diagonal_may_not_cut_a_corner_across_void()
    {
        // Two cells touching only at their corner: passable, and not a step.
        var ground = new HashSet<(int X, int Z)> { (0, 0), (1, 1) };
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 4, 4));
        await Assert.That(Walk.Between((0, 0), (1, 1), walkable)).IsNull();
    }

    [Test]
    public async Task A_rise_of_one_is_walked_and_costs_nothing()
    {
        var ground = Rect(0, 0, 6, 1);
        var surface = ground.ToDictionary(cell => cell, cell => 10 + cell.X);   // one block a step
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(), surface, new CellRect(0, 0, 6, 1));
        await Assert.That(Walk.Between((0, 0), (5, 0), walkable)!.Cost.Blocks).IsEqualTo(0);
    }

    [Test]
    public async Task A_rise_costs_one_block_less_than_its_height()
    {
        // A single step up of four: three blocks placed. Two would cost one, three would cost two.
        var ground = Rect(0, 0, 2, 1);
        var surface = new Dictionary<(int X, int Z), int> { [(0, 0)] = 10, [(1, 0)] = 14 };
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(), surface, new CellRect(0, 0, 2, 1));
        await Assert.That(Walk.Between((0, 0), (1, 0), walkable)!.Cost.Blocks).IsEqualTo(3);
    }

    [Test]
    public async Task A_fall_costs_no_block_and_a_deep_one_is_counted()
    {
        var ground = Rect(0, 0, 3, 1);
        // 20 → 17 is free; 17 → 10 is a seven-block fall.
        var surface = new Dictionary<(int X, int Z), int> { [(0, 0)] = 20, [(1, 0)] = 17, [(2, 0)] = 10 };
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(), surface, new CellRect(0, 0, 3, 1));

        var cost = Walk.Between((0, 0), (2, 0), walkable)!.Cost;
        await Assert.That(cost.Blocks).IsEqualTo(0);
        await Assert.That(cost.Drops).IsEqualTo(1);
        await Assert.That(cost.WorstDrop).IsEqualTo(7);
    }

    [Test]
    public async Task Bridged_void_costs_one_block_a_cell()
    {
        var ground = new HashSet<(int X, int Z)> { (0, 0), (4, 0) };
        var bridge = new HashSet<(int X, int Z)> { (1, 0), (2, 0), (3, 0) };
        var surface = Rect(0, 0, 5, 1).ToDictionary(cell => cell, _ => 10);
        var walkable = new WalkGround(ground, bridge, surface, new CellRect(0, 0, 5, 1));

        var cost = Walk.Between((0, 0), (4, 0), walkable)!.Cost;
        await Assert.That(cost.Blocks).IsEqualTo(3);
        await Assert.That(cost.Distance).IsEqualTo(4);
    }

    [Test]
    public async Task Water_costs_no_block_and_doubles_the_distance_it_covers()
    {
        var ground = Rect(0, 0, 5, 1);
        var surface = ground.ToDictionary(cell => cell, _ => 10);
        var water = new HashSet<(int X, int Z)> { (1, 0), (2, 0), (3, 0) };
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(), surface,
            new CellRect(0, 0, 5, 1), 1, water);

        var cost = Walk.Between((0, 0), (4, 0), walkable)!.Cost;
        await Assert.That(cost.Blocks).IsEqualTo(0);
        await Assert.That(cost.Distance).IsEqualTo(7);       // one dry step, three swum, one dry
    }

    [Test]
    public async Task Travel_takes_the_short_way_and_reach_takes_the_cheap_one()
    {
        // A short bridge over four cells of void, or a long way round on solid ground.
        var ground = new HashSet<(int X, int Z)> { (0, 0), (5, 0) };
        for (var x = 0; x <= 5; x++) { ground.Add((x, 6)); }
        for (var z = 0; z <= 6; z++) { ground.Add((0, z)); ground.Add((5, z)); }
        var bridge = new HashSet<(int X, int Z)> { (1, 0), (2, 0), (3, 0), (4, 0) };
        var surface = Rect(0, 0, 6, 7).ToDictionary(cell => cell, _ => 10);
        var walkable = new WalkGround(ground, bridge, surface, new CellRect(0, 0, 6, 7));

        var travel = Walk.Between((0, 0), (5, 0), walkable, WalkAim.Travel)!.Cost;
        var reach = Walk.Between((0, 0), (5, 0), walkable, WalkAim.Reach)!.Cost;

        await Assert.That(travel.Blocks).IsEqualTo(4);       // straight over the void
        await Assert.That(reach.Blocks).IsEqualTo(0);        // all the way round
        await Assert.That(reach.Distance).IsGreaterThan(travel.Distance);
    }

    [Test]
    public async Task A_plan_answers_in_blocks_and_not_in_cells()
    {
        // Ten cells of five blocks each is fifty blocks, whatever the grid is drawn in.
        var ground = Rect(0, 0, 11, 1);
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 11, 1), 5);
        await Assert.That(Walk.Between((0, 0), (10, 0), walkable)!.Cost.Distance).IsEqualTo(50);
    }

    [Test]
    public async Task An_unreachable_target_answers_nothing_rather_than_a_number()
    {
        var ground = new HashSet<(int X, int Z)> { (0, 0), (9, 9) };
        var walkable = new WalkGround(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 10, 10));
        await Assert.That(Walk.Between((0, 0), (9, 9), walkable)).IsNull();
    }

    [Test]
    public async Task Where_two_routes_tie_the_one_further_from_the_void_wins()
    {
        // A twenty-wide slab: every crossing from west to east is the same length, and the middle rows are
        // the ones with room either side.
        var walkable = Flat(21, 21);
        var path = Walk.Between((0, 10), (20, 10), walkable)!;
        var strayed = path.Cells.Min(cell => Math.Min(cell.Z, 20 - cell.Z));
        await Assert.That(strayed).IsGreaterThanOrEqualTo(10);
    }

    [Test]
    public async Task A_field_prices_every_cell_the_way_one_journey_would()
    {
        var ground = new HashSet<(int X, int Z)> { (0, 0), (4, 0) };
        var bridge = new HashSet<(int X, int Z)> { (1, 0), (2, 0), (3, 0) };
        var surface = Rect(0, 0, 5, 1).ToDictionary(cell => cell, _ => 10);
        var walkable = new WalkGround(ground, bridge, surface, new CellRect(0, 0, 5, 1));

        var field = Walk.Field((0, 0), walkable, WalkAim.Reach);
        await Assert.That(field[(4, 0)].Blocks).IsEqualTo(3);
        await Assert.That(field[(4, 0)]).IsEqualTo(Walk.Between((0, 0), (4, 0), walkable, WalkAim.Reach)!.Cost);
    }
}
