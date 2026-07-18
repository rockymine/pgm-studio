using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The publish policy (docs/map-generation-constraint-taxonomy.md §4.1): terminal-capped shapes veto their
/// bays and holes, allow their notches (the clearance margin handles room proximity); terminal-free bodies
/// allow everything. The publishable region is the front, unguarded parts. Publishing is an offer for a later
/// pipeline step, never a fill.
/// </summary>
public sealed class PublishPolicyTests
{
    private const int Cw = 3;

    private static NegativeSpace SpaceOf(EdgeClassification c, NegativeSpaceKind kind) =>
        c.Spaces.Single(s => s.Kind == kind);

    [Test]
    public async Task Terminal_capped_bays_and_holes_veto_notches_allow()
    {
        // the scythe's bay (walled by the terminal's path) and its notch
        var scythe = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Scythe, 18, 15, Cw));
        await Assert.That(PublishPolicy.Space(SpaceOf(scythe, NegativeSpaceKind.Bay), terminalCapped: true))
            .IsEqualTo(PublishVerdict.Veto);
        await Assert.That(PublishPolicy.Space(SpaceOf(scythe, NegativeSpaceKind.Notch), terminalCapped: true))
            .IsEqualTo(PublishVerdict.Allow);

        // the U's entry-walled bay is vetoed alike — no room slot needed
        var u = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.U, 15, 18, Cw));
        await Assert.That(PublishPolicy.Space(SpaceOf(u, NegativeSpaceKind.Bay), terminalCapped: true))
            .IsEqualTo(PublishVerdict.Veto);

        // the donut's hole is the shape's own device
        var donut = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Donut, 18, 15, Cw));
        await Assert.That(PublishPolicy.Space(SpaceOf(donut, NegativeSpaceKind.Hole), terminalCapped: true))
            .IsEqualTo(PublishVerdict.Veto);

        // the Z's second notch is walled by the room-run and still publishes — proximity is the guard's job
        var z = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Z, 15, 21, Cw));
        var roomRunNotch = z.Spaces.Single(s => s.WallSlots.Contains(ApproachSlots.RoomRun));
        await Assert.That(PublishPolicy.Space(roomRunNotch, terminalCapped: true)).IsEqualTo(PublishVerdict.Allow);
    }

    [Test]
    public async Task Terminal_free_bodies_publish_their_bays_and_holes()
    {
        var u = BodyEdges.Classify(ShapeEmitter.Body(ShapeFamily.U, 15, 18, Cw));
        var bay = SpaceOf(u, NegativeSpaceKind.Bay);
        await Assert.That(PublishPolicy.Space(bay, terminalCapped: false)).IsEqualTo(PublishVerdict.Allow);
        await Assert.That(PublishPolicy.PublishableParts(bay, terminalCapped: false).Count).IsEqualTo(1);

        var ring = BodyEdges.Classify(BodyEmitter.Ring(Cw, 5 * Cw, 5 * Cw));
        var hole = SpaceOf(ring, NegativeSpaceKind.Hole);
        await Assert.That(PublishPolicy.PublishableParts(hole, terminalCapped: false).Count).IsEqualTo(1);
    }

    // the whole-classification read infers the context from the terminal-owned runs and applies the part
    // filter: front and unguarded
    [Test]
    public async Task Publishable_infers_context_and_filters_parts()
    {
        // the L with its clearance: the notch offers its two unguarded parts; the room-corner stays back
        var l = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.L, 15, 18, Cw), BodyEdges.DefaultClearanceCells);
        var offered = PublishPolicy.Publishable(l);
        await Assert.That(offered.Count).IsEqualTo(1);
        await Assert.That(offered[0].Space.Kind).IsEqualTo(NegativeSpaceKind.Notch);
        await Assert.That(offered[0].Parts.Count).IsEqualTo(2);
        await Assert.That(offered[0].Parts.All(p => !p.Guarded && p.Front)).IsTrue();

        // the scythe offers only its notch — the bay is vetoed wholesale
        var scythe = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Scythe, 18, 15, Cw));
        var scytheOffers = PublishPolicy.Publishable(scythe);
        await Assert.That(scytheOffers.Count).IsEqualTo(1);
        await Assert.That(scytheOffers[0].Space.Kind).IsEqualTo(NegativeSpaceKind.Notch);

        // the degenerate E's covered slots stay back: only the front bay part is offered
        var e = BodyEdges.Classify(BodyEmitter.SpineArms(spineLen: 15, barThickness: 3, arms: [(0, 3, 12), (6, 3, 6), (12, 3, 12)]));
        var eOffers = PublishPolicy.Publishable(e);
        await Assert.That(eOffers.Count).IsEqualTo(1);
        await Assert.That(eOffers[0].Parts.Count).IsEqualTo(1);
        await Assert.That(eOffers[0].Parts[0].Rect.SequenceEqual(new[] { 3, 9, 9, 6 })).IsTrue();
    }
}
