using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Derive;

/// <summary>What a step costs beyond being a step. The three charges are the edge (a walk that only
/// minimises length hugs every border, and on these boards a border is void), the bridge, and the reach of a
/// held wall — each tested by the route it moves, not by the number it produces.</summary>
public sealed class PlanRouteCostTests
{
    private static PlanPiece P(string id, int x, int z, int w, int h, string role = PlanRoles.Piece) =>
        new() { Id = id, Role = role, Rect = new CellRect(x, z, w, h) };

    private static PlanModel Plan(params PlanPiece[] pieces)
    {
        var plan = new PlanModel { Globals = new PlanGlobals { Cell = 1, Symmetry = "none", Surface = 9 } };
        plan.Pieces = [.. pieces];
        return plan;
    }

    // A wide slab: a flat walk cuts the corner along the border, a charged one keeps off it.
    private static PlanModel Slab() => Plan(P("slab", 0, 0, 21, 21));

    [Test]
    public async Task Flat_costs_nothing_and_leaves_the_walk_alone()
    {
        var nav = PlanNav.Of(Slab());
        await Assert.That(PlanRouteCost.Build(nav, Slab(), RouteCosts.Flat)).IsEmpty();
        var cost = PlanRouteCost.Of(nav, Slab(), RouteCosts.Flat);
        await Assert.That(cost((0, 0))).IsEqualTo(1);
        await Assert.That(cost((10, 10))).IsEqualTo(1);
    }

    [Test]
    public async Task The_edge_charge_falls_off_with_clearance()
    {
        var plan = Slab();
        var nav = PlanNav.Of(plan);
        var cost = PlanRouteCost.Of(nav, plan, new RouteCosts(CorridorWidth: 20, EdgeWeight: 3,
            BridgeWeight: 0, ThreatWeight: 0, BowRange: 0));
        // cell 1, so comfort is 10 cells: the border pays the most and the middle pays nothing
        await Assert.That(cost((0, 0))).IsGreaterThan(cost((5, 5)));
        await Assert.That(cost((5, 5))).IsGreaterThan(cost((10, 10)));
        await Assert.That(cost((10, 10))).IsEqualTo(1).Because("dead centre has all the clearance it wants");
    }

    [Test]
    public async Task A_charged_edge_pulls_the_walk_off_the_border()
    {
        var plan = Slab();
        var nav = PlanNav.Of(plan);
        var clearance = Cells.Clearance(nav.Ground, nav.Bounds);
        double MeanClearance(List<(int X, int Z)> path) => path.Average(c => (double)clearance.GetValueOrDefault(c, 0));

        var flat = Cells.CheapestPath((0, 0), (20, 20), nav.Navigable, PlanRouteCost.Of(nav, plan, RouteCosts.Flat))!;
        var kept = Cells.CheapestPath((0, 0), (20, 20), nav.Navigable,
            PlanRouteCost.Of(nav, plan, new RouteCosts(20, 3, 0, 0, 0)))!;

        await Assert.That(MeanClearance(kept)).IsGreaterThan(MeanClearance(flat));
    }

    [Test]
    public async Task A_wall_reaches_as_far_as_the_bow_and_only_over_bridged_ground()
    {
        var plan = Plan(P("hold", 0, 0, 4, 1), P("behind", 0, 1, 4, 1));
        plan.Walls = [new PlanWall { A = "hold", B = "behind" }];
        plan.Zones = [new PlanZone { Id = "cross", Rect = new CellRect(0, 2, 4, 1) }];
        var nav = PlanNav.Of(plan);

        var seam = PlanRouteCost.Seam(nav, "hold", "behind");
        await Assert.That(seam).IsNotEmpty().Because("the two pieces share a border");
        var covered = PlanRouteCost.Threatened(nav, plan, bowCells: 3);
        await Assert.That(covered.Overlaps(nav.Bridge)).IsTrue();

        var charged = PlanRouteCost.Of(nav, plan, new RouteCosts(0, 0, 0, ThreatWeight: 12, BowRange: 3));
        var free = PlanRouteCost.Of(nav, plan, new RouteCosts(0, 0, 0, 0, 0));
        var bridgeCell = nav.Bridge.First();
        await Assert.That(charged(bridgeCell)).IsGreaterThan(free(bridgeCell));
        var groundCell = nav.Ground.First();
        await Assert.That(charged(groundCell)).IsEqualTo(free(groundCell))
            .Because("standing on terrain near a wall is the fight, not the exposure");
    }

    [Test]
    public async Task No_wall_means_nothing_is_threatened()
    {
        var plan = Slab();
        await Assert.That(PlanRouteCost.Threatened(PlanNav.Of(plan), plan, bowCells: 30)).IsEmpty();
    }

    [Test]
    public async Task Seam_is_the_shared_border_and_not_the_two_whole_pieces()
    {
        var plan = Plan(P("a", 0, 0, 5, 3), P("b", 0, 3, 5, 3));
        var nav = PlanNav.Of(plan);
        var seam = PlanRouteCost.Seam(nav, "a", "b");
        await Assert.That(seam.Count).IsEqualTo(10).Because("one row either side of the border, 5 wide");
        await Assert.That(seam.Count).IsLessThan(nav.Ground.Count);
    }

    // A flat corridor beside a staircase: the short way jumps a scarp, the long way walks up in ones.
    private static PlanModel Scarp()
    {
        var plan = Plan(
            P("low", 0, 0, 3, 1), P("jump", 3, 0, 1, 1), P("high", 4, 0, 3, 1),
            P("s1", 3, 1, 1, 1), P("s2", 4, 1, 1, 1), P("s3", 5, 1, 1, 1), P("foot", 0, 1, 3, 1));
        plan.Globals.Surface = 4;
        foreach (var (id, h) in new[] { ("low", 4), ("jump", 10), ("high", 10),
                                        ("foot", 4), ("s1", 5), ("s2", 7), ("s3", 9) })
            plan.Pieces.First(p => p.Id == id).Surface = h;
        return plan;
    }

    [Test]
    public async Task A_climb_is_charged_on_the_step_up_and_never_on_the_way_down()
    {
        var plan = Scarp();
        var nav = PlanNav.Of(plan);
        var step = PlanRouteCost.StepOf(nav, plan, new RouteCosts(0, 0, 0, 0, 0, ClimbWeight: 3));

        // 4 → 10 is a six-block rise: five of them are charged, the first is free
        await Assert.That(step((2, 0), (3, 0))).IsEqualTo(1 + 3 * 5);
        await Assert.That(step((3, 0), (2, 0))).IsEqualTo(1).Because("coming down is free at any height");
        await Assert.That(step((0, 0), (1, 0))).IsEqualTo(1).Because("level ground costs the bare step");
        await Assert.That(step((0, 1), (1, 1))).IsEqualTo(1);
    }

    [Test]
    public async Task A_free_rise_of_one_block_is_walked_rather_than_built()
    {
        var plan = Scarp();
        var nav = PlanNav.Of(plan);
        var step = PlanRouteCost.StepOf(nav, plan, new RouteCosts(0, 0, 0, 0, 0, ClimbWeight: 3));
        await Assert.That(step((3, 1), (4, 1))).IsEqualTo(1 + 3 * 1).Because("5 → 7 is two blocks, one free");
        await Assert.That(step((2, 1), (3, 1))).IsEqualTo(1).Because("4 → 5 is one block, free");
    }

    [Test]
    public async Task Charging_the_climb_sends_the_walk_round_by_the_staircase()
    {
        var plan = Scarp();
        var nav = PlanNav.Of(plan);
        int Steepest(List<(int X, int Z)> path)
        {
            var worst = 0;
            for (var i = 1; i < path.Count; i++)
                worst = Math.Max(worst, nav.SurfaceAt[path[i]] - nav.SurfaceAt[path[i - 1]]);
            return worst;
        }

        var flat = Cells.CheapestPath((0, 0), (6, 0), nav.Navigable,
            PlanRouteCost.Of(nav, plan, RouteCosts.Flat))!;
        var charged = Cells.CheapestPath((0, 0), (6, 0), nav.Navigable,
            PlanRouteCost.StepOf(nav, plan, new RouteCosts(0, 0, 0, 0, 0, ClimbWeight: 8)))!;

        await Assert.That(Steepest(flat)).IsEqualTo(6).Because("the short way jumps the scarp");
        await Assert.That(Steepest(charged)).IsLessThan(Steepest(flat));
        await Assert.That(charged.Count).IsGreaterThan(flat.Count).Because("the staircase is longer");
    }

    [Test]
    public async Task With_no_climb_weight_the_step_cost_is_the_cell_cost()
    {
        var plan = Scarp();
        var nav = PlanNav.Of(plan);
        var costs = new RouteCosts(20, 3, 2, 12, 30, ClimbWeight: 0);
        var cell = PlanRouteCost.Of(nav, plan, costs);
        var step = PlanRouteCost.StepOf(nav, plan, costs);
        foreach (var c in nav.Navigable)
            foreach (var n in Cells.N4(c).Where(nav.Navigable.Contains))
                await Assert.That(step(c, n)).IsEqualTo(cell(n));
    }

    [Test]
    public async Task BaseId_strips_the_orbit_image()
    {
        await Assert.That(PlanRouteCost.BaseId("piece-7")).IsEqualTo("piece-7");
        await Assert.That(PlanRouteCost.BaseId("piece-7#1")).IsEqualTo("piece-7");
    }
}
