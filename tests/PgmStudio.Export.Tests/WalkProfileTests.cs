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
        await Assert.That(WalkProfile.Events(path, new WorldProvenance())).IsEmpty();
    }

    [Test]
    public async Task A_climb_a_bigger_climb_and_a_fall_each_class_as_their_own_word()
    {
        var path = Path((0, 0, 10), (1, 0, 12), (2, 0, 20), (3, 0, 10));
        var events = WalkProfile.Events(path, new WorldProvenance());

        await Assert.That(events.Count).IsEqualTo(3);
        await Assert.That(events[0]).IsEqualTo(new WalkProfile.Event(1, 0, 2, "scramble"));
        await Assert.That(events[1]).IsEqualTo(new WalkProfile.Event(2, 0, 8, "barrier"));
        await Assert.That(events[2]).IsEqualTo(new WalkProfile.Event(3, 0, -10, "drop"));
    }

    /// <summary>A plan's wall is a step the map states, so the climb onto it and the drop off it are the
    /// wall's, and the worst step is the worst the ground makes.</summary>
    [Test]
    public async Task A_step_onto_or_off_a_stated_wall_is_the_wall_and_not_the_worst_step()
    {
        var path = Path((0, 0, 14), (1, 0, 16), (2, 0, 20), (3, 0, 14), (4, 0, 14));
        var provenance = new WorldProvenance();
        provenance.Claim(2, 0, ProvenancePass.Structure, new StampId("wall", "0", 0));

        var profile = WalkProfile.Of(path, provenance);

        await Assert.That(profile.Events.Select(step => step.Word).ToList())
            .IsEquivalentTo(new[] { "scramble", WalkProfile.WallWord, WalkProfile.WallWord });
        await Assert.That(profile.WorstStep).IsEqualTo(2).Because("the scramble is the ground's worst step");
        await Assert.That(profile.Rises).IsEqualTo(2);
        await Assert.That(profile.Falls).IsEqualTo(1);
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

    /// <summary>A route that crosses a wall, a wool room's redstone line and a control point passes all three,
    /// and each is named beside it.</summary>
    [Test]
    [Arguments("wall")]
    [Arguments("redstoneline")]
    [Arguments("controlpoint")]
    public async Task Beside_names_a_structure_the_route_passes_through(string kind)
    {
        var path = Path((0, 0, 10), (1, 0, 14), (2, 0, 10));
        var provenance = new WorldProvenance();
        provenance.Claim(1, 0, ProvenancePass.Structure, new StampId(kind, "0", 0));

        var found = WalkProfile.Beside(path, provenance, 1);

        await Assert.That(found.Select(near => near.Owner.Kind).ToList()).IsEquivalentTo(new[] { kind });
        await Assert.That(found[0].Distance).IsEqualTo(0);
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
