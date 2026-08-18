using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Derive;

/// <summary>The plan read as ground someone walks: which cells a piece covers, which a build zone bridges,
/// which a water lane does not, where the enclosed voids are, and where the markers sit — all fanned to the
/// whole board.</summary>
public sealed class PlanNavTests
{
    // A ring of four pieces about a 2×2 hole, plus a build zone hanging off one side, mirrored by rot_180.
    private static PlanModel Ring()
    {
        var plan = new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "rot_180", Surface = 9 } };
        plan.Pieces =
        [
            new PlanPiece { Id = "north", Role = PlanRoles.Piece, Rect = new CellRect(-2, -4, 4, 1) },
            new PlanPiece { Id = "south", Role = PlanRoles.Piece, Rect = new CellRect(-2, -1, 4, 1) },
            new PlanPiece { Id = "west",  Role = PlanRoles.Piece, Rect = new CellRect(-2, -3, 1, 2) },
            new PlanPiece { Id = "east",  Role = PlanRoles.Piece, Rect = new CellRect(1, -3, 1, 2) },
        ];
        return plan;
    }

    [Test]
    public async Task Ground_is_every_generating_piece_fanned_to_the_whole_board()
    {
        var nav = PlanNav.Of(Ring());
        // four pieces of 4+4+2+2 = 12 cells, doubled by rot_180
        await Assert.That(nav.Ground.Count).IsEqualTo(24);
        await Assert.That(nav.Bridge).IsEmpty();
        await Assert.That(nav.Navigable.Count).IsEqualTo(24);
    }

    [Test]
    public async Task An_enclosed_void_is_a_hole_and_an_open_bay_is_not()
    {
        var closed = PlanNav.Of(Ring());
        await Assert.That(closed.Holes.Count).IsEqualTo(2).Because("the ring and its orbit image");
        await Assert.That(closed.Holes[0].Count).IsEqualTo(4);

        // remove one arm and the pocket opens onto the outside, so it stops being a hole
        var cup = Ring();
        cup.Pieces = [.. cup.Pieces.Where(p => p.Id != "north")];
        await Assert.That(PlanNav.Of(cup).Holes).IsEmpty();
    }

    // Two islands with a three-cell gap and something spanning it. Unfanned, so the counts are the rects.
    private static PlanModel Strait(string? zoneKind)
    {
        var plan = new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "none", Surface = 9 } };
        plan.Pieces =
        [
            new PlanPiece { Id = "left",  Role = PlanRoles.Piece, Rect = new CellRect(-4, 0, 2, 1) },
            new PlanPiece { Id = "right", Role = PlanRoles.Piece, Rect = new CellRect(1, 0, 2, 1) },
        ];
        plan.Zones = [new PlanZone { Id = "cross", Rect = new CellRect(-2, 0, 3, 1), Kind = zoneKind }];
        return plan;
    }

    [Test]
    public async Task A_build_zone_bridges_the_gap()
    {
        var nav = PlanNav.Of(Strait(null));
        await Assert.That(nav.Ground.Count).IsEqualTo(4);
        await Assert.That(nav.Bridge.Count).IsEqualTo(3);
        await Assert.That(Cells.Components(nav.Navigable)).IsEqualTo(1);
    }

    [Test]
    public async Task A_water_lane_does_not_bridge_it_unless_asked()
    {
        var plan = Strait(PlanZoneKinds.WaterLane);
        var nav = PlanNav.Of(plan);
        await Assert.That(nav.Bridge).IsEmpty().Because("a lane opens late, so no early journey walks it");
        await Assert.That(nav.Lane.Count).IsEqualTo(3).Because("it is still drawn");
        await Assert.That(Cells.Components(nav.Navigable)).IsEqualTo(2).Because("the two islands stay apart");

        var opened = PlanNav.Of(plan, lanesNavigable: true);
        await Assert.That(opened.Bridge.Count).IsEqualTo(3);
        await Assert.That(Cells.Components(opened.Navigable)).IsEqualTo(1);
    }

    [Test]
    public async Task A_zones_declared_hole_is_void_however_it_is_covered()
    {
        var plan = Strait(null);
        plan.Zones[0].Holes = [new CellRect(-1, 0, 1, 1)];
        var nav = PlanNav.Of(plan);
        await Assert.That(nav.Navigable.Contains((-1, 0))).IsFalse().Because("the zone declares it no-build");
        await Assert.That(nav.Navigable.Contains((-2, 0))).IsTrue();
        await Assert.That(Cells.Components(nav.Navigable)).IsEqualTo(2).Because("the hole reopens the strait");
    }

    [Test]
    public async Task Every_marker_is_fanned_and_named_by_kind()
    {
        var plan = Ring();
        plan.Placements.Spawns = [new SpawnPlacement { Piece = "north", At = [1, 0] }];
        plan.Placements.Wools = [new WoolPlacement { Piece = "south", At = [1, 0] }];
        var nav = PlanNav.Of(plan);

        var waypoints = nav.Waypoints().ToList();
        await Assert.That(waypoints.Count).IsEqualTo(4).Because("two markers, two orbit images");
        await Assert.That(waypoints.Count(w => w.Kind == "spawn")).IsEqualTo(2);
        await Assert.That(waypoints.Select(w => w.K).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task PieceAt_names_the_image_so_a_walk_reads_back_as_a_sequence()
    {
        var nav = PlanNav.Of(Ring());
        var names = nav.Ground.Select(c => nav.PieceAt[c]).Distinct().ToList();
        await Assert.That(names).Contains("north");
        await Assert.That(names).Contains("north#1").Because("the orbit image carries its own name");
        await Assert.That(nav.RoleOf["north"]).IsEqualTo(PlanRoles.Piece);
    }

    [Test]
    public async Task Snap_finds_the_nearest_walkable_cell_and_gives_up_past_the_radius()
    {
        var nav = PlanNav.Of(Ring());
        await Assert.That(nav.Snap((-2, -4))).IsEqualTo((-2, -4)).Because("already on ground");
        await Assert.That(nav.Snap((-1, -3))).IsNotNull().Because("a hole cell is one step off the ring");
        await Assert.That(nav.Snap((900, 900), radius: 2)).IsNull();
    }

    [Test]
    public async Task An_empty_plan_reads_as_nothing_rather_than_throwing()
    {
        var nav = PlanNav.Of(new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "rot_180" } });
        await Assert.That(nav.Navigable).IsEmpty();
        await Assert.That(nav.Holes).IsEmpty();
        await Assert.That(nav.Waypoints()).IsEmpty();
    }
}
