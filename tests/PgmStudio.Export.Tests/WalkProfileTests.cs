using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Export.Tests;

/// <summary>
/// A walked route's own profile (WS21): the steps that are not a plain walk, classed the way
/// <see cref="Walk.StepWord"/> classes them, and what the provenance record names beside the route.
/// </summary>
public sealed class WalkProfileTests
{
    private static WalkPath Path(params (int X, int Z, int Y)[] places) =>
        new([.. places.Select(place => new WalkPlace(place.X, place.Z, place.Y))], default);

    [Test]
    public async Task A_route_that_never_leaves_a_walk_has_no_events()
    {
        var path = Path((0, 0, 10), (1, 0, 10), (2, 0, 11), (3, 0, 9));   // rises of 0, 1, -2 — all within a walk
        await Assert.That(WalkProfile.Events(path)).IsEmpty();
    }

    [Test]
    public async Task A_climb_a_bigger_climb_and_a_fall_each_class_as_their_own_word()
    {
        var path = Path((0, 0, 10), (1, 0, 12), (2, 0, 20), (3, 0, 10));
        var events = WalkProfile.Events(path);

        await Assert.That(events.Count).IsEqualTo(3);
        await Assert.That(events[0]).IsEqualTo(new WalkProfile.Event(1, 0, 2, "scramble"));
        await Assert.That(events[1]).IsEqualTo(new WalkProfile.Event(2, 0, 8, "barrier"));
        await Assert.That(events[2]).IsEqualTo(new WalkProfile.Event(3, 0, -10, "drop"));
    }

    [Test]
    public async Task Beside_finds_a_claim_within_the_radius_and_leaves_out_an_excluded_kind()
    {
        var path = Path((0, 0, 10), (1, 0, 10), (2, 0, 10));
        var provenance = new WorldProvenance();
        provenance.Claim(3, 0, ProvenancePass.Prop, new StampId("tree", "t1", 0));    // 1 cell off the route
        provenance.Claim(1, 1, ProvenancePass.Prop, new StampId("flora", "f1", 0));   // in range, excluded kind

        var found = WalkProfile.Beside(path, provenance, 2);

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Owner.Kind).IsEqualTo("tree");
        await Assert.That(found[0].Distance).IsEqualTo(1);
    }

    [Test]
    public async Task Beside_keeps_the_nearest_cell_when_one_owner_covers_more_than_one()
    {
        var path = Path((0, 0, 10));
        var provenance = new WorldProvenance();
        var owner = new StampId("house", "h1", 0);
        provenance.Claim(2, 0, ProvenancePass.Structure, owner);
        provenance.Claim(1, 0, ProvenancePass.Structure, owner);   // nearer — this is "the first cell met"

        var found = WalkProfile.Beside(path, provenance, 3);

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Distance).IsEqualTo(1);
        await Assert.That((found[0].X, found[0].Z)).IsEqualTo((1, 0));
    }

    [Test]
    public async Task A_radius_of_zero_finds_nothing()
    {
        var path = Path((0, 0, 10));
        var provenance = new WorldProvenance();
        provenance.Claim(0, 0, ProvenancePass.Prop, new StampId("tree", "t1", 0));

        await Assert.That(WalkProfile.Beside(path, provenance, 0)).IsEmpty();
    }
}
