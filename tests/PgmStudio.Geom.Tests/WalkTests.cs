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

    /// <summary>Ground stated the way a board is drawn — cells and a height apiece — seated into the places
    /// a walk runs over. Every cell holds one, which is what a board with nothing stacked on it is.</summary>
    private static WalkGround Board(IReadOnlySet<(int X, int Z)> ground, IReadOnlySet<(int X, int Z)> bridge,
        IReadOnlyDictionary<(int X, int Z), int> surface, CellRect bounds, int blocksPerCell = 1,
        IReadOnlySet<(int X, int Z)>? water = null)
        => new(Seat(ground, surface), Seat(bridge, surface), bounds, blocksPerCell, water);

    private static HashSet<WalkPlace> Seat(IEnumerable<(int X, int Z)> cells,
        IReadOnlyDictionary<(int X, int Z), int> surface)
        => [.. cells.Select(cell => new WalkPlace(cell.X, cell.Z, surface.GetValueOrDefault(cell)))];

    /// <summary>The one place a cell holds on a board drawn this way.</summary>
    private static WalkPlace At(WalkGround ground, (int X, int Z) cell) => ground.Stand(cell)!.Value;

    /// <summary>Flat ground of one height, nothing to bridge, no water.</summary>
    private static WalkGround Flat(int width, int height, int surface = 10)
    {
        var ground = Rect(0, 0, width, height);
        return Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => surface), new CellRect(0, 0, width, height));
    }

    [Test]
    public async Task A_straight_run_is_its_own_length_in_blocks()
    {
        var walkable = Flat(20, 5);
        var path = Walk.Between(At(walkable, (0, 0)), At(walkable, (10, 0)), walkable);
        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Cost.Distance).IsEqualTo(10);
        await Assert.That(path.Cost.Blocks).IsEqualTo(0);
    }

    [Test]
    public async Task A_diagonal_run_costs_the_root_two_a_player_walks_not_the_staircase()
    {
        // Ten cells across and ten down: an axis-aligned walk reports 20, a player walks 14.1.
        var walkable = Flat(20, 20);
        var path = Walk.Between(At(walkable, (0, 0)), At(walkable, (10, 10)), walkable);
        await Assert.That(path!.Cost.Distance).IsEqualTo(14);
    }

    [Test]
    public async Task A_diagonal_may_not_cut_a_corner_across_void()
    {
        // Two cells touching only at their corner: passable, and not a step.
        var ground = new HashSet<(int X, int Z)> { (0, 0), (1, 1) };
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 4, 4));
        await Assert.That(Walk.Between(At(walkable, (0, 0)), At(walkable, (1, 1)), walkable)).IsNull();
    }

    [Test]
    public async Task A_rise_of_one_is_walked_and_costs_nothing()
    {
        var ground = Rect(0, 0, 6, 1);
        var surface = ground.ToDictionary(cell => cell, cell => 10 + cell.X);   // one block a step
        var walkable = Board(ground, new HashSet<(int X, int Z)>(), surface, new CellRect(0, 0, 6, 1));
        await Assert.That(Walk.Between(At(walkable, (0, 0)), At(walkable, (5, 0)), walkable)!.Cost.Blocks).IsEqualTo(0);
    }

    [Test]
    public async Task A_rise_costs_one_block_less_than_its_height()
    {
        // A single step up of four: three blocks placed. Two would cost one, three would cost two.
        var ground = Rect(0, 0, 2, 1);
        var surface = new Dictionary<(int X, int Z), int> { [(0, 0)] = 10, [(1, 0)] = 14 };
        var walkable = Board(ground, new HashSet<(int X, int Z)>(), surface, new CellRect(0, 0, 2, 1));
        await Assert.That(Walk.Between(At(walkable, (0, 0)), At(walkable, (1, 0)), walkable)!.Cost.Blocks).IsEqualTo(3);
    }

    [Test]
    public async Task A_fall_costs_no_block_and_a_deep_one_is_counted()
    {
        var ground = Rect(0, 0, 3, 1);
        // 20 → 17 is free; 17 → 10 is a seven-block fall.
        var surface = new Dictionary<(int X, int Z), int> { [(0, 0)] = 20, [(1, 0)] = 17, [(2, 0)] = 10 };
        var walkable = Board(ground, new HashSet<(int X, int Z)>(), surface, new CellRect(0, 0, 3, 1));

        var cost = Walk.Between(At(walkable, (0, 0)), At(walkable, (2, 0)), walkable)!.Cost;
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
        var walkable = Board(ground, bridge, surface, new CellRect(0, 0, 5, 1));

        var cost = Walk.Between(At(walkable, (0, 0)), At(walkable, (4, 0)), walkable)!.Cost;
        await Assert.That(cost.Blocks).IsEqualTo(3);
        await Assert.That(cost.Distance).IsEqualTo(4);
    }

    [Test]
    public async Task Water_costs_no_block_and_doubles_the_distance_it_covers()
    {
        var ground = Rect(0, 0, 5, 1);
        var surface = ground.ToDictionary(cell => cell, _ => 10);
        var water = new HashSet<(int X, int Z)> { (1, 0), (2, 0), (3, 0) };
        var walkable = Board(ground, new HashSet<(int X, int Z)>(), surface,
            new CellRect(0, 0, 5, 1), 1, water);

        var cost = Walk.Between(At(walkable, (0, 0)), At(walkable, (4, 0)), walkable)!.Cost;
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
        var walkable = Board(ground, bridge, surface, new CellRect(0, 0, 6, 7));

        var travel = Walk.Between(At(walkable, (0, 0)), At(walkable, (5, 0)), walkable, WalkAim.Travel)!.Cost;
        var reach = Walk.Between(At(walkable, (0, 0)), At(walkable, (5, 0)), walkable, WalkAim.Reach)!.Cost;

        await Assert.That(travel.Blocks).IsEqualTo(4);       // straight over the void
        await Assert.That(reach.Blocks).IsEqualTo(0);        // all the way round
        await Assert.That(reach.Distance).IsGreaterThan(travel.Distance);
    }

    [Test]
    public async Task A_plan_answers_in_blocks_and_not_in_cells()
    {
        // Ten cells of five blocks each is fifty blocks, whatever the grid is drawn in.
        var ground = Rect(0, 0, 11, 1);
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 11, 1), 5);
        await Assert.That(Walk.Between(At(walkable, (0, 0)), At(walkable, (10, 0)), walkable)!.Cost.Distance).IsEqualTo(50);
    }

    [Test]
    public async Task An_unreachable_target_answers_nothing_rather_than_a_number()
    {
        var ground = new HashSet<(int X, int Z)> { (0, 0), (9, 9) };
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 10, 10));
        await Assert.That(Walk.Between(At(walkable, (0, 0)), At(walkable, (9, 9)), walkable)).IsNull();
    }

    [Test]
    public async Task Where_two_routes_tie_the_one_further_from_the_void_wins()
    {
        // A twenty-wide slab: every crossing from west to east is the same length, and the middle rows are
        // the ones with room either side.
        var walkable = Flat(21, 21);
        var path = Walk.Between(At(walkable, (0, 10)), At(walkable, (20, 10)), walkable)!;
        var strayed = path.Cells.Min(cell => Math.Min(cell.Z, 20 - cell.Z));
        await Assert.That(strayed).IsGreaterThanOrEqualTo(10);
    }

    [Test]
    public async Task A_field_prices_every_cell_the_way_one_journey_would()
    {
        var ground = new HashSet<(int X, int Z)> { (0, 0), (4, 0) };
        var bridge = new HashSet<(int X, int Z)> { (1, 0), (2, 0), (3, 0) };
        var surface = Rect(0, 0, 5, 1).ToDictionary(cell => cell, _ => 10);
        var walkable = Board(ground, bridge, surface, new CellRect(0, 0, 5, 1));

        var field = Walk.Field(At(walkable, (0, 0)), walkable, WalkAim.Reach);
        await Assert.That(field[At(walkable, (4, 0))].Blocks).IsEqualTo(3);
        await Assert.That(field[At(walkable, (4, 0))]).IsEqualTo(Walk.Between(At(walkable, (0, 0)), At(walkable, (4, 0)), walkable, WalkAim.Reach)!.Cost);
    }

    [Test]
    public async Task Comfort_pays_for_the_wider_crossing_that_travel_will_not()
    {
        // Two rooms joined two ways: a three-wide passage straight between them, and a nine-wide one a
        // little further down. Travel takes the near one; comfort pays for the room either side.
        var ground = new HashSet<(int X, int Z)>();
        foreach (var cell in Rect(0, 0, 8, 31)) ground.Add(cell);
        foreach (var cell in Rect(20, 0, 8, 31)) ground.Add(cell);
        foreach (var cell in Rect(8, 14, 12, 3)) ground.Add(cell);
        foreach (var cell in Rect(8, 18, 12, 9)) ground.Add(cell);

        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), Cells.BoundingBox(ground));
        var clearance = Cells.Clearance(ground, Cells.BoundingBox(ground));

        var travel = Walk.Between(At(walkable, (4, 15)), At(walkable, (23, 15)), walkable)!;
        var comfort = Walk.Between(At(walkable, (4, 15)), At(walkable, (23, 15)), walkable, WalkAim.Comfort)!;

        await Assert.That(comfort.Cells.Min(cell => clearance[cell]))
            .IsGreaterThan(travel.Cells.Min(cell => clearance[cell]));
        await Assert.That(comfort.Cost.Distance).IsGreaterThan(travel.Cost.Distance);
        await Assert.That(comfort.Cost.Distance - travel.Cost.Distance).IsLessThanOrEqualTo(Walk.Detour);
    }

    [Test]
    public async Task Comfort_never_strays_further_than_the_allowance()
    {
        // One room with a pillar in it: comfort goes round the far side of the pillar only while that stays
        // inside the allowance, which is what stops a standoff route wandering.
        var ground = new HashSet<(int X, int Z)>(Rect(0, 0, 41, 41));
        foreach (var cell in Rect(18, 15, 5, 11)) ground.Remove(cell);
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 41, 41));

        var travel = Walk.Between(At(walkable, (0, 20)), At(walkable, (40, 20)), walkable)!;
        var comfort = Walk.Between(At(walkable, (0, 20)), At(walkable, (40, 20)), walkable, WalkAim.Comfort)!;
        await Assert.That(comfort.Cost.Distance - travel.Cost.Distance).IsLessThanOrEqualTo(Walk.Detour);
    }

    /// <summary>A ring: two arms of equal length round a hole three cells wide.</summary>
    private static WalkGround Ring()
    {
        var ground = new HashSet<(int X, int Z)>(Rect(0, 0, 9, 7));
        foreach (var cell in Rect(3, 2, 3, 3)) ground.Remove(cell);
        return Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 9, 7));
    }

    [Test]
    public async Task A_field_reaches_only_what_the_origin_connects_to()
    {
        var ground = new HashSet<(int X, int Z)>(Rect(0, 0, 3, 2));
        foreach (var cell in Rect(4, 0, 3, 2)) ground.Add(cell);
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 7, 2));

        var field = Walk.Field(At(walkable, (0, 0)), walkable);
        await Assert.That(field.ContainsKey(At(walkable, (2, 1)))).IsTrue();
        await Assert.That(field.ContainsKey(At(walkable, (4, 0)))).IsFalse().Because("the far block is a separate component");
    }

    [Test]
    public async Task A_walk_routes_round_a_wall_rather_than_through_it()
    {
        // A U of ground with a wall down the middle: the two arm tops are two cells apart in a straight line
        // and a long way apart on foot, which is the difference a straight-line measure cannot see.
        var ground = new HashSet<(int X, int Z)>(Rect(0, 0, 5, 5));
        for (var z = 1; z <= 4; z++) ground.Remove((2, z));
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 5, 5));

        var walked = Walk.Between(At(walkable, (1, 4)), At(walkable, (3, 4)), walkable)!;
        await Assert.That(walked.Cost.Distance).IsGreaterThan(2);
        await Assert.That(walked.Cells.Any(cell => cell.Z == 0)).IsTrue().Because("the base is the only way round");
    }

    [Test]
    public async Task A_corridor_carries_both_arms_where_one_route_commits_to_one()
    {
        var ring = Ring();
        var north = ring.Ground.Where(place => place.Z <= 1).ToHashSet();
        var south = ring.Ground.Where(place => place.Z >= 5).ToHashSet();

        var ribbon = Walk.Corridor(At(ring, (0, 3)), At(ring, (8, 3)), ring, 0.30);
        await Assert.That(ribbon.Overlaps(north)).IsTrue();
        await Assert.That(ribbon.Overlaps(south)).IsTrue();

        // A single route can only ever carry one side, so the other reads unused however many players walk it.
        var route = Walk.Between(At(ring, (0, 3)), At(ring, (8, 3)), ring)!.Places.ToHashSet();
        var arms = (route.Overlaps(north) ? 1 : 0) + (route.Overlaps(south) ? 1 : 0);
        await Assert.That(arms).IsEqualTo(1);
    }

    [Test]
    public async Task A_corridor_stops_at_the_slack_budget()
    {
        // The north way round is the short one; the south bay is a long way further.
        var ground = new HashSet<(int X, int Z)>(Rect(0, 0, 9, 8));
        foreach (var cell in Rect(3, 2, 3, 2)) ground.Remove(cell);
        foreach (var cell in Rect(1, 4, 7, 3)) ground.Remove(cell);
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 9, 8));

        var bay = At(walkable, (4, 7));
        await Assert.That(Walk.Corridor(At(walkable, (0, 3)), At(walkable, (8, 3)), walkable, 0.30).Contains(bay)).IsFalse();
        await Assert.That(Walk.Corridor(At(walkable, (0, 3)), At(walkable, (8, 3)), walkable, 3.0).Contains(bay)).IsTrue();
    }

    [Test]
    public async Task A_corridor_is_empty_when_the_ends_do_not_connect()
    {
        var ground = new HashSet<(int X, int Z)>(Rect(0, 0, 3, 2));
        foreach (var cell in Rect(4, 0, 3, 2)) ground.Add(cell);
        var walkable = Board(ground, new HashSet<(int X, int Z)>(),
            ground.ToDictionary(cell => cell, _ => 10), new CellRect(0, 0, 7, 2));
        await Assert.That(Walk.Corridor(At(walkable, (0, 0)), At(walkable, (6, 0)), walkable, 0.30)).IsEmpty();
    }

    /// <summary>A yard under a deck: one cell, two places, and no way between them. The pair the walk could
    /// not tell apart while its node was a cell.</summary>
    private static WalkGround Stacked()
    {
        var places = new HashSet<WalkPlace>();
        var clear = new Dictionary<WalkPlace, int>();
        for (var x = 0; x < 9; x++)
            for (var z = 0; z < 5; z++)
            {
                var yard = new WalkPlace(x, z, 7);
                places.Add(yard);
                places.Add(new WalkPlace(x, z, 27));
                clear[yard] = 13;                             // roofed at 20, thirteen blocks over the yard
            }
        return new WalkGround(places, new HashSet<WalkPlace>(), new CellRect(0, 0, 9, 5), 1, null, clear);
    }

    [Test]
    public async Task A_stacked_cell_holds_a_place_for_each_storey()
    {
        var board = Stacked();
        await Assert.That(board.Footprint.Count).IsEqualTo(45);
        await Assert.That(board.Passable.Count).IsEqualTo(90);
        await Assert.That(board.Stacks[(4, 2)].Select(place => place.Y)).IsEquivalentTo(new[] { 7, 27 });
    }

    [Test]
    public async Task A_cell_naming_no_storey_means_the_one_a_player_walks_in_at()
    {
        var board = Stacked();
        await Assert.That(board.Stand((4, 2))).IsEqualTo(new WalkPlace(4, 2, 7));
        await Assert.That(board.Nearest((4, 2), 30)).IsEqualTo(new WalkPlace(4, 2, 27));
        await Assert.That(board.Nearest((4, 2), 9)).IsEqualTo(new WalkPlace(4, 2, 7));
    }

    /// <summary>The whole point: a route along the yard stays on the yard, and one along the deck stays on
    /// the deck. A walk keyed on the cell answered one of the two for both.</summary>
    [Test]
    public async Task A_route_keeps_to_the_storey_it_started_on()
    {
        var board = Stacked();
        var below = Walk.Between(new WalkPlace(0, 2, 7), new WalkPlace(8, 2, 7), board)!;
        var above = Walk.Between(new WalkPlace(0, 2, 27), new WalkPlace(8, 2, 27), board)!;

        await Assert.That(below.Places.All(place => place.Y == 7)).IsTrue();
        await Assert.That(above.Places.All(place => place.Y == 27)).IsTrue();
        await Assert.That(below.Cost.Distance).IsEqualTo(above.Cost.Distance);
        await Assert.That(below.Cost.Blocks).IsEqualTo(0);
    }

    /// <summary>A roof thirteen blocks over the yard is not a step onto a deck twenty over it, so the two
    /// storeys are separate boards however much of one cell they share.</summary>
    [Test]
    public async Task A_roofed_storey_is_not_a_step_from_the_one_over_it()
        => await Assert.That(Walk.Between(new WalkPlace(0, 2, 7), new WalkPlace(8, 2, 27), Stacked())).IsNull();

    /// <summary>Cut the roof away over one cell and the two storeys join there — the clearance is the only
    /// thing keeping them apart, which is what makes a stairwell a stairwell.</summary>
    [Test]
    public async Task A_gap_in_the_roof_joins_the_two_storeys()
    {
        var board = Stacked();
        var open = new Dictionary<WalkPlace, int>(board.Clear!);
        open.Remove(new WalkPlace(4, 2, 7));
        var holed = new WalkGround(board.Ground, board.Bridgeable, board.Bounds, 1, null, open);

        var path = Walk.Between(new WalkPlace(0, 2, 7), new WalkPlace(8, 2, 27), holed);
        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Places.Any(place => place == new WalkPlace(4, 2, 7))).IsTrue();
        await Assert.That(path.Cost.Blocks).IsEqualTo(19);     // a rise of twenty, one of it free
    }

    [Test]
    public async Task A_comfort_field_is_refused_rather_than_answered_as_travel()
    {
        var walkable = Flat(5, 5);
        await Assert.That(() => Walk.Field(At(walkable, (0, 0)), walkable, WalkAim.Comfort))
            .Throws<ArgumentOutOfRangeException>();
    }
}
