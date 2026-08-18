using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Pgm.Tests.Render;

/// <summary>The plan as a grid of characters — the form in which a relation between two rectangles is one
/// glance rather than an inference. Asserted on what the grid must show, never on an exact layout, so the
/// tests survive a legend or a ruler changing.</summary>
public sealed class PlanBoardAsciiTests
{
    private static PlanModel Plan(params PlanPiece[] pieces)
    {
        var plan = new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "none", Surface = 9 } };
        plan.Pieces = [.. pieces];
        return plan;
    }

    private static PlanPiece P(string id, int x, int z, int w, int h, string role = PlanRoles.Piece) =>
        new() { Id = id, Role = role, Rect = new CellRect(x, z, w, h) };

    [Test]
    public async Task An_empty_plan_says_so_rather_than_drawing_nothing()
    {
        var text = PlanBoardAscii.Render(new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "none" } });
        await Assert.That(text).Contains("draws nothing");
    }

    [Test]
    public async Task Every_piece_gets_a_letter_and_the_key_names_it()
    {
        var text = PlanBoardAscii.Render(Plan(P("alpha", 0, 0, 3, 2), P("beta", 5, 0, 3, 2)));
        await Assert.That(text).Contains("alpha");
        await Assert.That(text).Contains("beta");
        await Assert.That(text).Contains("cell 5");
        // the two pieces span x 0..8, so the footer states the board and its block size
        await Assert.That(text).Contains("8 × 2 cells = 40 × 10 blocks");
    }

    [Test]
    public async Task A_build_zone_a_lane_and_an_enclosed_void_each_read_differently()
    {
        var plan = Plan(
            P("north", -2, -2, 4, 1), P("south", -2, 1, 4, 1),
            P("west", -2, -1, 1, 2), P("east", 1, -1, 1, 2));
        plan.Zones =
        [
            new PlanZone { Id = "cross", Rect = new CellRect(-2, 2, 4, 1) },
            new PlanZone { Id = "lane", Rect = new CellRect(-2, 3, 4, 1), Kind = PlanZoneKinds.WaterLane },
        ];
        var text = PlanBoardAscii.Render(plan);
        await Assert.That(text).Contains(PlanBoardAscii.BuildZone);
        await Assert.That(text).Contains(PlanBoardAscii.WaterLane);
        await Assert.That(text).Contains(PlanBoardAscii.EnclosedVoid).Because("the ring traps a 2x2 pocket");
    }

    [Test]
    public async Task An_overlay_paints_over_the_board_and_joins_the_key()
    {
        var plan = Plan(P("slab", 0, 0, 4, 3));
        var plain = PlanBoardAscii.Render(plan);
        var marked = PlanBoardAscii.Render(plan, [new PlanBoardAscii.Overlay("the route", '#', [(1, 1), (2, 1)])]);

        await Assert.That(plain).DoesNotContain('#');
        await Assert.That(marked).Contains('#');
        await Assert.That(marked).Contains("the route");
    }

    [Test]
    public async Task Later_overlays_win_so_a_route_reads_over_its_corridor()
    {
        var plan = Plan(P("slab", 0, 0, 4, 3));
        var text = PlanBoardAscii.Render(plan,
        [
            new PlanBoardAscii.Overlay("corridor", ':', [(1, 1), (2, 1)]),
            new PlanBoardAscii.Overlay("route", '#', [(1, 1)]),
        ]);
        await Assert.That(text).Contains('#');
        await Assert.That(text).Contains(':').Because("the cell the route did not claim keeps the corridor");
    }

    [Test]
    public async Task Downsampling_shrinks_the_grid_and_says_it_did()
    {
        var plan = Plan(P("slab", 0, 0, 40, 40));
        var full = PlanBoardAscii.Render(plan);
        var half = PlanBoardAscii.Render(plan, every: 2);
        await Assert.That(half.Length).IsLessThan(full.Length);
        await Assert.That(half).Contains("1:2");
        await Assert.That(full).DoesNotContain("1:2");
        await Assert.That(half).Contains("40 × 40 cells").Because("the true size is still stated");
    }

    [Test]
    public async Task Compare_lays_two_cell_sets_on_the_same_rows()
    {
        var bar = Enumerable.Range(-8, 16).Select(x => (x, 0)).ToList();
        var band = Enumerable.Range(-2, 4).Select(x => (x, 1)).ToList();
        var text = PlanBoardAscii.Compare("the bar against the band",
            ("bar", 'M', bar), ("build zone", 'N', band));

        await Assert.That(text).Contains("the bar against the band");
        await Assert.That(text).Contains("M bar — 16 cells");
        await Assert.That(text).Contains("N build zone — 4 cells");
        await Assert.That(text).Contains(new string('M', 16)).Because("the relation is one row against another");
    }
}
