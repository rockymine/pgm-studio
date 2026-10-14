using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The frontline box kind: a join with no room (Bar · single · twin), rotation fixed — the spine docks the
/// hub on the top edge and the arm tips are the face on the bottom edge, toward the axis.</summary>
public class FrontlineBoxEmitterTests
{
    private static Box Box(string id = "front") => new(id, BoxKind.Frontline, new(0, 0, 8, 6), 48);

    // the runs a compound presents on one edge of the 8×6 test box — the tips on the face, the spine on the top
    private static IReadOnlyList<EdgeInterval> Runs(Compound f, BoxEdge edge, int w = 8, int h = 6) =>
        BoxInterfaces.Runs(BoxInterfaces.Of(f, w, h).Single(e => e.Edge == edge).Intervals);

    [Test]
    public async Task Bar_frontline_is_a_solid_wide_face_with_no_room()
    {
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(CompoundForm.Rectangle), cw: 2)!;

        await Assert.That(f.Pieces.Count).IsEqualTo(1);
        await Assert.That(f.Room).IsNull();
        // the whole far edge is the wide face — one run
        await Assert.That(Runs(f, BoxEdge.Bottom).Count).IsEqualTo(1);
        await Assert.That(Runs(f, BoxEdge.Bottom)[0].LengthCells).IsEqualTo(8);
    }

    [Test]
    public async Task The_spine_lies_along_the_top_and_the_tips_along_the_face()
    {
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(CompoundForm.SpineArms, 2), cw: 2)!;
        await Assert.That(Runs(f, BoxEdge.Top).Single().LengthCells).IsEqualTo(8);
        await Assert.That(Runs(f, BoxEdge.Bottom).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Single_is_the_fat_L_with_the_tip_spanning_all_but_one_corridor()
    {
        // never the centred T: a T's narrow tip is the whole front-face hull, forcing a too-thin mid band. The
        // fat L anchors its one arm at the spine's start and spans all but one corridor width (the void notch)
        var f = FrontlineBoxEmitter.Fill(Box(), new CompoundRead(CompoundForm.SpineArms, 1), cw: 2)!;

        await Assert.That(f.Pieces.Count).IsEqualTo(2);                        // spine + the one fat leg
        var tips = Runs(f, BoxEdge.Bottom);
        await Assert.That(tips.Count).IsEqualTo(1);
        await Assert.That(tips[0].LengthCells).IsEqualTo(6);  // the leg tip: spine 8 − cw 2
        await Assert.That(tips[0].Start).IsEqualTo(0);        // anchored at the spine's start
    }

    [Test]
    public async Task Single_nulls_when_the_leg_cannot_be_strictly_wider_than_the_notch()
    {
        // the thin-leg L (leg ≤ notch) has the same too-thin band as the banned T and directed-nulls
        var box = new Box("frontline", BoxKind.Frontline, new(0, 0, 4, 4), 16);
        await Assert.That(FrontlineBoxEmitter.Fill(
            box, new CompoundRead(CompoundForm.SpineArms, 1), cw: 2)).IsNull();
    }

    [Test]
    public async Task A_sampled_arm_layout_overrides_the_canonical_legs()
    {
        // an F on a 10-wide front: legs 2 and 3 with a 2-bay and a 3-wide end recess (ttvvtttxxx)
        var box = new Box("frontline", BoxKind.Frontline, new(0, 0, 10, 6), 60);
        var f = FrontlineBoxEmitter.Fill(box, new CompoundRead(CompoundForm.SpineArms, 2), cw: 2,
            armLayout: [(0, 2), (4, 3)])!;

        await Assert.That(f.Pieces.Count).IsEqualTo(3);                        // spine + two uneven legs
        var tips = Runs(f, BoxEdge.Bottom, 10, 6);
        await Assert.That(tips.Count).IsEqualTo(2);
        await Assert.That(tips.Select(o => o.LengthCells).OrderBy(x => x).SequenceEqual([2, 3])).IsTrue();
    }

    [Test]
    public async Task Sampled_layouts_respect_the_leg_laws_across_many_draws()
    {
        // the leg laws: every leg ≥ 2 wide; a pair within factor 2; the inter-leg bay 2–4; end recesses
        // together at most a third of the spine; the single leg strictly wider than its notch
        var rng = new ComposeRng(7);
        foreach (var w in new[] { 5, 6, 8, 10, 12 })
            for (var i = 0; i < 100; i++)
            {
                if (FrontlineBoxEmitter.SampleArms(rng, w, 1) is { } single)
                {
                    var leg = single[0].Width;
                    await Assert.That(leg > w - leg).IsTrue();                 // strictly wider than the notch
                }
                if (FrontlineBoxEmitter.SampleArms(rng, w, 2) is { } pair)
                {
                    var (a, b) = (pair[0], pair[1]);
                    await Assert.That(a.Width >= 2 && b.Width >= 2).IsTrue();
                    await Assert.That(a.Width <= 2 * b.Width && b.Width <= 2 * a.Width).IsTrue();
                    var bay = b.Start - (a.Start + a.Width);
                    await Assert.That(bay is >= 2 and <= 4).IsTrue();
                    var ends = a.Start + (w - (b.Start + b.Width));
                    await Assert.That(ends <= w / 3).IsTrue();
                }
            }
    }

    [Test]
    public async Task Too_small_a_box_is_a_directed_null()
    {
        await Assert.That(FrontlineBoxEmitter.Fill(new Box("f", BoxKind.Frontline, new(0, 0, 3, 3), 9),
            new CompoundRead(CompoundForm.SpineArms, 2), cw: 2)).IsNull();
    }

    [Test]
    public async Task A_form_off_the_frontline_menu_throws()
    {
        await Assert.That(() => FrontlineBoxEmitter.Fill(Box(), new CompoundRead(CompoundForm.Ring), cw: 2))
            .Throws<ComposeException>();
    }
}
