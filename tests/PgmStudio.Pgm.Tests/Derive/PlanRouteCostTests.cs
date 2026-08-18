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

    [Test]
    public async Task BaseId_strips_the_orbit_image()
    {
        await Assert.That(PlanRouteCost.BaseId("piece-7")).IsEqualTo("piece-7");
        await Assert.That(PlanRouteCost.BaseId("piece-7#1")).IsEqualTo("piece-7");
    }
}
