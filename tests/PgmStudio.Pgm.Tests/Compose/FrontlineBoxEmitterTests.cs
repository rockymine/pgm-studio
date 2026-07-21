using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The frontline box kind: a terminal-free join (Bar · single · twin) — no room, front-labeled pieces,
/// rotation fixed (spine docks the hub, face toward the axis), offering only its face with the mid's grouping
/// contract (joint — one wide consumer; several — one per tip).</summary>
public class FrontlineBoxEmitterTests
{
    private static Box Box(string id = "front") => new(id, BoxKind.Frontline, [0, 0, 8, 6], 48);

    [Test]
    public async Task Bar_frontline_is_a_solid_wide_face_terminal_free()
    {
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(Compound.Rectangle), cw: 2, OfferGrouping.Joint)!;

        await Assert.That(f.Pieces.Count).IsEqualTo(1);
        await Assert.That(f.Pieces.All(p => p.Box!.Kind == BoxKind.Frontline)).IsTrue();
        await Assert.That(f.Pieces.Any(p => p.Slot == ApproachSlots.Room)).IsFalse();
        await Assert.That(f.SpineEdge).IsEqualTo(BoxEdge.Top);
        await Assert.That(f.FaceEdge).IsEqualTo(BoxEdge.Bottom);
        // the whole far edge is the wide face — one run
        await Assert.That(f.FaceOffers.Count).IsEqualTo(1);
        await Assert.That(f.FaceOffers[0].Interval.LengthCells).IsEqualTo(8);
    }

    [Test]
    public async Task Only_the_face_edge_is_offered_never_the_spine_or_sides()
    {
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(Compound.SpineArms, 2), cw: 2, OfferGrouping.Several)!;
        await Assert.That(f.FaceOffers.All(o => o.Edge == BoxEdge.Bottom)).IsTrue();
    }

    [Test]
    public async Task Single_is_the_fat_L_with_the_tip_spanning_all_but_one_corridor()
    {
        // never the centred T: a T's narrow tip is the whole front-face hull, forcing a too-thin mid band. The
        // fat L anchors its one arm at the spine's start and spans all but one corridor width (the void notch)
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(Compound.SpineArms, 1), cw: 2, OfferGrouping.Several)!;

        await Assert.That(f.Pieces.Count).IsEqualTo(2);                        // spine + the one fat leg
        await Assert.That(f.FaceOffers.Count).IsEqualTo(1);
        await Assert.That(f.FaceOffers[0].Interval.LengthCells).IsEqualTo(6);  // the leg tip: spine 8 − cw 2
        await Assert.That(f.FaceOffers[0].Interval.Start).IsEqualTo(0);        // anchored at the spine's start
    }

    [Test]
    public async Task Single_nulls_when_the_leg_cannot_be_strictly_wider_than_the_notch()
    {
        // the thin-leg L (leg ≤ notch) has the same too-thin band as the banned T and directed-nulls
        var box = new Box("frontline", BoxKind.Frontline, [0, 0, 4, 4], 16);
        await Assert.That(FrontlineBoxEmitter.Fill(
            box, new CompoundRead(Compound.SpineArms, 1), cw: 2, OfferGrouping.Several)).IsNull();
    }

    [Test]
    public async Task Twin_several_is_two_tips_each_its_own_consumer()
    {
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(Compound.SpineArms, 2), cw: 2, OfferGrouping.Several)!;

        await Assert.That(f.Pieces.Count).IsEqualTo(3);                        // spine + two arms
        await Assert.That(f.FaceOffers.Count).IsEqualTo(2);
        await Assert.That(f.FaceOffers.All(o => o.Grouping == OfferGrouping.Several)).IsTrue();
        // severally: each tip its own group (two derived runs — the double frontline)
        await Assert.That(f.FaceOffers.Select(o => o.GroupId).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Twin_joint_is_one_group_the_mid_spans_the_recess_unoffered()
    {
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(Compound.SpineArms, 2), cw: 2, OfferGrouping.Joint)!;

        await Assert.That(f.FaceOffers.Count).IsEqualTo(2);                    // the two tips…
        await Assert.That(f.FaceOffers.All(o => o.Grouping == OfferGrouping.Joint)).IsTrue();
        // jointly: one shared group — a single wide consumer spans both, the inter-tip recess simply not offered
        await Assert.That(f.FaceOffers.Select(o => o.GroupId).Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Too_small_a_box_is_a_directed_null()
    {
        await Assert.That(FrontlineBoxEmitter.Fill(new Box("f", BoxKind.Frontline, [0, 0, 3, 3], 9),
            new CompoundRead(Compound.SpineArms, 2), cw: 2, OfferGrouping.Several)).IsNull();
    }

    [Test]
    public async Task A_form_off_the_frontline_menu_throws()
    {
        await Assert.That(() => FrontlineBoxEmitter.Fill(Box(), new CompoundRead(Compound.Ring), cw: 2, OfferGrouping.Several))
            .Throws<ComposeException>();
    }
}
