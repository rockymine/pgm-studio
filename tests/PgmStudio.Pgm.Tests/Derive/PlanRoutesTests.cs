using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Derive;

/// <summary>A journey across a plan: how far, along how many genuinely different ways, and where the choice
/// opens and closes again. The cases are shapes whose answer is known by construction — a board with one way
/// must report one, and a ring must report the two sides of it.</summary>
public sealed class PlanRoutesTests
{
    private static PlanPiece P(string id, int x, int z, int w, int h) =>
        new() { Id = id, Role = PlanRoles.Piece, Rect = new CellRect(x, z, w, h) };

    private static PlanModel Plan(params PlanPiece[] pieces)
    {
        var plan = new PlanModel { Globals = new PlanGlobals { Cell = 5, Symmetry = "none", Surface = 9 } };
        plan.Pieces = [.. pieces];
        return plan;
    }

    // A ring about a 3x3 hole: two ways from the west arm to the east arm.
    private static PlanModel Ring() => Plan(
        P("north", -3, -3, 6, 1), P("south", -3, 2, 6, 1),
        P("west", -3, -2, 1, 4), P("east", 2, -2, 1, 4));

    // The same ring, plus a second one far away that this journey never goes near.
    private static PlanModel TwoRings() => Plan(
        P("north", -3, -3, 6, 1), P("south", -3, 2, 6, 1),
        P("west", -3, -2, 1, 4), P("east", 2, -2, 1, 4),
        P("far-n", 20, -3, 6, 1), P("far-s", 20, 2, 6, 1),
        P("far-w", 20, -2, 1, 4), P("far-e", 25, -2, 1, 4));

    [Test]
    public async Task A_ring_offers_two_ways_and_a_bar_offers_one()
    {
        var nav = PlanNav.Of(Ring());
        var read = PlanRoutes.Read(nav, (-3, 0), (2, 0));
        await Assert.That(read.Shortest).IsNotNull();
        await Assert.That(read.Options.Count).IsEqualTo(2).Because("over the top and under the bottom");
        await Assert.That(read.Holes.Count(h => h.OnCorridor && h.Ways == 2)).IsEqualTo(1);

        var bar = PlanRoutes.Read(PlanNav.Of(Plan(P("bar", -3, 0, 6, 1))), (-3, 0), (2, 0));
        await Assert.That(bar.Options.Count).IsEqualTo(1);
        await Assert.That(bar.Holes).IsEmpty();
        await Assert.That(bar.Forks).IsEmpty().Because("one way has nowhere to diverge");
    }

    [Test]
    public async Task Options_are_kept_only_when_the_piece_sequence_differs()
    {
        var read = PlanRoutes.Read(PlanNav.Of(Ring()), (-3, 0), (2, 0));
        var signatures = read.Options.Select(o => string.Join("|", o.Pieces)).ToList();
        await Assert.That(signatures.Distinct().Count()).IsEqualTo(signatures.Count);
        await Assert.That(read.Options.Any(o => o.Pieces.Contains("north"))).IsTrue();
        await Assert.That(read.Options.Any(o => o.Pieces.Contains("south"))).IsTrue();
    }

    /// <summary><b>One decision per door, and it names the door.</b> A ring is one hole and so one choice: it
    /// opens where the arms part and closes where they meet, and the fork carries the hole it is about so a
    /// reader is not left matching coordinates to holes by eye.</summary>
    [Test]
    public async Task A_fork_names_the_door_it_is_about_and_where_the_choice_is_live()
    {
        var read = PlanRoutes.Read(PlanNav.Of(Ring()), (-3, 0), (2, 0));

        await Assert.That(read.Forks.Count).IsEqualTo(1).Because("one hole is one choice");
        var fork = read.Forks[0];
        await Assert.That(fork.Split.X).IsEqualTo(-3);
        await Assert.That(fork.Merge.X).IsEqualTo(2);
        await Assert.That(fork.Live).IsGreaterThan(0);
        await Assert.That(fork.Live).IsLessThanOrEqualTo(read.Shortest!.Value);
        await Assert.That(fork.Area).IsEqualTo(read.Holes.Single(h => h.Index == fork.Hole).Area);
        await Assert.That(fork.Ratio).IsGreaterThanOrEqualTo(1.0);
    }

    /// <summary><b>Two doors are two decisions, not one span across both.</b> Two rings in a row each offer a
    /// choice, and an envelope from the first parting to the last merge would describe neither.</summary>
    [Test]
    public async Task Two_doors_answer_twice()
    {
        // one north arm and one south arm spanning two holes, with a post between them: west to east is a
        // choice at each hole
        var twin = Plan(
            P("north", -3, -3, 12, 1), P("south", -3, 2, 12, 1),
            P("west", -3, -2, 1, 4), P("post", 2, -2, 1, 4), P("east", 8, -2, 1, 4));
        var read = PlanRoutes.Read(PlanNav.Of(twin), (-3, 0), (8, 0));

        await Assert.That(read.Shortest).IsNotNull();
        await Assert.That(read.Forks.Count).IsEqualTo(2);
        await Assert.That(read.Forks.Select(fork => fork.Hole).Distinct().Count()).IsEqualTo(2)
            .Because("each names its own door");
    }

    [Test]
    public async Task A_hole_the_journey_never_approaches_is_reported_but_not_enumerated()
    {
        var nav = PlanNav.Of(TwoRings());
        await Assert.That(nav.Holes.Count).IsEqualTo(2);

        var read = PlanRoutes.Read(nav, (-3, 0), (2, 0));
        await Assert.That(read.Holes.Count).IsEqualTo(2).Because("both are named");
        await Assert.That(read.Holes.Count(h => h.OnCorridor)).IsEqualTo(1);

        var off = read.Holes.Single(h => !h.OnCorridor);
        await Assert.That(off.Ways).IsEqualTo(0)
            .Because("a ray cast from a hole this journey never reaches says nothing about it");
        await Assert.That(read.Options.Count).IsEqualTo(2).Because("only the near hole contributes ways");
    }

    [Test]
    public async Task An_unreachable_target_answers_no_distance_rather_than_throwing()
    {
        var split = Plan(P("here", -4, 0, 2, 1), P("there", 2, 0, 2, 1));
        var read = PlanRoutes.Read(PlanNav.Of(split), (-4, 0), (2, 0));
        await Assert.That(read.Shortest).IsNull();
        await Assert.That(read.Options).IsEmpty();
        await Assert.That(read.Corridor).IsEmpty();
        await Assert.That(read.Forks).IsEmpty();
    }

    [Test]
    public async Task The_corridor_is_a_share_of_the_board_and_carries_both_arms_of_a_ring()
    {
        var nav = PlanNav.Of(Ring());
        var read = PlanRoutes.Read(nav, (-3, 0), (2, 0));
        await Assert.That(read.CorridorShare).IsGreaterThan(0);
        await Assert.That(read.CorridorShare).IsLessThanOrEqualTo(1);
        var north = nav.Ground.Where(c => nav.PieceAt[c] == "north").ToHashSet();
        var south = nav.Ground.Where(c => nav.PieceAt[c] == "south").ToHashSet();
        await Assert.That(read.Corridor.Overlaps(north) && read.Corridor.Overlaps(south)).IsTrue()
            .Because("the ribbon carries both sides where one geodesic must pick one");
    }

    [Test]
    public async Task A_second_way_reports_how_much_longer_it_is()
    {
        var read = PlanRoutes.Read(PlanNav.Of(Ring()), (-3, 0), (2, 0));
        foreach (var option in read.Options)
        {
            await Assert.That(option.Ratio(read.Shortest!.Value)).IsGreaterThanOrEqualTo(1.0);
            await Assert.That(option.Length).IsGreaterThanOrEqualTo(read.Shortest!.Value);
        }
    }
}
