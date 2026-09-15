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

    /// <summary><b>A defence is read at the door of the room it defends, not at the wool inside it.</b> A team
    /// cannot enter its own wool room — the defining rule of the mode — so a walk that ends on the wool is a
    /// walk that team cannot make. The doorstep is nearer, so the number falls, and `DefenderRatio` falls with
    /// it: that ratio is what match length is read off before anything else geometric.</summary>
    [Test]
    public async Task A_defence_is_read_at_the_door_of_the_room_it_defends()
    {
        var plan = Board();
        var nav = PlanNav.Of(plan);
        var leg = PlanFlow.Read(plan).Legs.Single();

        // the wool, and the walk that ignores whose ground it is
        var defenderSpawn = nav.Snap(nav.Waypoints().First(w => w.Kind == "spawn" && w.K == 1).Cell)!.Value;
        var wool = nav.Snap(nav.Waypoints().First(w => w.Kind == "wool" && w.K == 1).Cell)!.Value;
        var shared = nav.Walkable();
        var through = Walk.Between(shared.Stand(defenderSpawn)!.Value, shared.Stand(wool)!.Value, shared)!;

        await Assert.That(leg.Defend).IsLessThan(through.Cost.Distance)
            .Because("the walk through the room is one the defence is not allowed to make");
        await Assert.That(leg.Defend).IsGreaterThan(0);
        await Assert.That(nav.For(1).Stand(wool)).IsNull();
    }

    /// <summary><b>A defence has a second origin, and which one is nearer is a fact about the board.</b> The
    /// board above puts the room behind the spawn, so a player at the crossing is further from it than a
    /// respawn — and moving the room to the front flips that. A read that knew only the spawn could not tell
    /// the two boards apart.</summary>
    [Test]
    public async Task A_chase_starts_at_the_crossing_and_may_beat_a_respawn_or_not()
    {
        var behind = PlanFlow.Read(Board()).Legs.Single();

        // the same board with the room in front of the spawn instead of behind it
        var forward = Board();
        forward.Pieces = [.. forward.Pieces.Select(piece => piece.Id == "room"
            ? P("room", -6, -6, 4, 4, PlanRoles.WoolRoom) : piece)];
        var ahead = PlanFlow.Read(forward).Legs.Single();

        await Assert.That(behind.Chase).IsGreaterThan(0);
        await Assert.That(ahead.Chase).IsGreaterThan(0);
        await Assert.That(behind.Chase).IsGreaterThan(behind.Defend)
            .Because("a room at the back is reached sooner from the spawn than from the middle");
        await Assert.That(ahead.Chase).IsLessThan(ahead.Defend)
            .Because("a room at the front is reached sooner from the middle than from the spawn");
    }

    /// <summary>And the account says which it is, since the number alone does not tell a reader whether the
    /// defence that matters is the one already out.</summary>
    [Test]
    public async Task The_account_names_where_the_defence_comes_from()
    {
        await Assert.That(PlanFlow.Describe(PlanFlow.Read(Board())))
            .Contains("already at the crossing");
    }
}
