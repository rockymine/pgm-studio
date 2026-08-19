using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Derive;

/// <summary>The readback a model is handed with its board: what each side has to walk, where the ways in part
/// and meet, and what that leaves nobody going to. Asserted on boards whose answer is known by construction —
/// a defence that must use the attackers' road against one that can come round behind, and a spur nothing
/// leads to.</summary>
public sealed class PlanFlowTests
{
    private static PlanPiece P(string id, int x, int z, int w, int h, string role = PlanRoles.Piece) =>
        new() { Id = id, Role = role, Rect = new CellRect(x, z, w, h) };

    // A lane from spawn to the enemy wool, with the two ends mirrored by rot_180.
    private static PlanModel Board(params PlanPiece[] extra)
    {
        var plan = new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "rot_180", Surface = 9 } };
        plan.Pieces =
        [
            P("spawn", -2, -14, 4, 3, PlanRoles.Spawn),
            P("lane", -2, -11, 4, 11),
            P("room", -6, -18, 4, 4, PlanRoles.WoolRoom),
            P("neck", -4, -14, 2, 3),
            .. extra,
        ];
        plan.Placements.Spawns = [new SpawnPlacement { Piece = "spawn", At = [2, 1] }];
        plan.Placements.Wools = [new WoolPlacement { Piece = "room", At = [2, 2] }];
        return plan;
    }

    [Test]
    public async Task A_capture_board_is_read_for_routes_and_a_destroy_board_is_not()
    {
        await Assert.That(PlanFlow.Read(Board()).Gamemode).IsEqualTo("ctw");

        var destroy = Board();
        destroy.Placements.Wools = [];
        destroy.Placements.Destroyables = [new DestroyablePlacement { Piece = "room", At = [2, 2] }];
        var read = PlanFlow.Read(destroy);
        await Assert.That(read.Gamemode).IsEqualTo("destroy");
        await Assert.That(read.Legs).IsEmpty().Because("a destroy board has no carry, so no route is invented");
        await Assert.That(PlanFlow.Describe(read)).Contains("not read for routes");
    }

    [Test]
    public async Task A_plan_with_no_objective_says_so_rather_than_reading_nothing()
    {
        var bare = Board();
        bare.Placements.Wools = [];
        var read = PlanFlow.Read(bare);
        await Assert.That(read.Gamemode).IsEqualTo("none");
        await Assert.That(PlanFlow.Describe(read)).Contains("no objective");
    }

    [Test]
    public async Task The_leg_carries_both_walks_and_the_ratio_between_them()
    {
        var leg = PlanFlow.Read(Board()).Legs.Single();
        await Assert.That(leg.Attack).IsGreaterThan(0);
        await Assert.That(leg.Defend).IsGreaterThan(0);
        await Assert.That(leg.Defend).IsLessThan(leg.Attack).Because("the defender starts nearer its own wool");
        await Assert.That(leg.DefenderRatio).IsEqualTo(leg.Defend / (double)leg.Attack);
    }

    [Test]
    public async Task Ground_no_journey_reaches_is_named_with_its_pieces_and_where_it_is()
    {
        // a spur hanging off the lane, leading nowhere any objective is
        var withSpur = Board(P("spur", -14, -8, 8, 5));
        var read = PlanFlow.Read(withSpur);

        await Assert.That(read.DeadBlocks).IsGreaterThan(0);
        var place = read.DeadPlaces[0];
        await Assert.That(place.Pieces.Any(p => p.StartsWith("spur"))).IsTrue();
        await Assert.That(place.Area).IsGreaterThanOrEqualTo(PlanFlow.PlaceFloor);
        await Assert.That(PlanFlow.Describe(read)).Contains("sit off every route");
    }

    [Test]
    public async Task A_board_every_journey_covers_says_so_plainly()
    {
        var read = PlanFlow.Read(Board());
        await Assert.That(read.DeadBlocks).IsEqualTo(0);
        await Assert.That(PlanFlow.Describe(read)).Contains("on somebody's way somewhere");
    }

    [Test]
    public async Task One_way_in_is_reported_as_one_road_to_hold()
    {
        var leg = PlanFlow.Read(Board()).Legs.Single();
        await Assert.That(leg.Ways).IsEqualTo(1);
        await Assert.That(leg.Split).IsNull();
        await Assert.That(leg.SharedRoad).IsFalse().Because("there is no merge for the defender to share");
        await Assert.That(PlanFlow.Describe(PlanFlow.Read(Board()))).Contains("One way in, end to end");
    }

    [Test]
    public async Task Everything_it_states_is_in_blocks_not_cells()
    {
        var plan = Board();
        var leg = PlanFlow.Read(plan).Legs.Single();
        // the lane alone is 11 cells of 5 blocks, so any honest walk to the far room is well past 11
        await Assert.That(leg.Attack).IsGreaterThan(11 * plan.Globals.Cell);
        await Assert.That(PlanFlow.Read(plan).GroundBlocks % (plan.Globals.Cell * plan.Globals.Cell)).IsEqualTo(0);
    }
}
