using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The directed-reason channel: every box kind that refuses a fill says <b>why</b>, in the shared
/// <see cref="FillRejection"/> vocabulary. A bare "no" is what made the emit↔derive mirror unreadable from
/// outside the composer — these assert the reason survives instead of being discarded, and that the
/// reason-carrying overloads change nothing about whether a fill succeeds.
/// </summary>
public sealed class FillRejectionTests
{
    private const int Cw = 2;

    private static Box Hub(int w, int h) => new("hub", BoxKind.Hub, new(0, 0, w, h), w * h);
    private static Box Wool(int w, int h) => new("wool", BoxKind.Wool, new(0, 0, w, h), w * h);
    private static Box Spawn(int w, int h) => new("spawn", BoxKind.Spawn, new(0, 0, w, h), w * h);
    private static Box Front(int w, int h) => new("front", BoxKind.Frontline, new(0, 0, w, h), w * h);

    [Test]
    public async Task A_hub_too_small_for_its_form_reports_the_emitters_own_reason()
    {
        // a 3x3 box cannot hold a ring at cw 2 (needs 2cw+1 = 5 each way)
        var hub = HubBoxEmitter.Fill(Hub(3, 3), new CompoundRead(Compound.Ring), Cw, false, out var why);
        await Assert.That(hub).IsNull();
        await Assert.That(why).IsTypeOf<FillRejection.FormDoesNotFit>();
        // the reason is the body emitter's own guard message, not a re-derivation of it
        await Assert.That(((FillRejection.FormDoesNotFit)why!).Detail).IsNotEmpty();
    }

    [Test]
    public async Task Each_wide_holed_hub_form_below_width_9_says_why()
    {
        foreach (var form in new[] { Compound.P, Compound.DoubleHole, Compound.G })
        {
            var hub = HubBoxEmitter.Fill(Hub(6, 5), new CompoundRead(form), Cw, false, out var why);
            await Assert.That(hub).IsNull().Because($"{form} needs width 9 at cw 2");
            await Assert.That(why).IsNotNull().Because($"{form}'s refusal must carry a reason");
        }
    }

    [Test]
    public async Task A_hub_that_fills_carries_no_rejection()
    {
        var hub = HubBoxEmitter.Fill(Hub(9, 9), new CompoundRead(Compound.Ring), Cw, false, out var why);
        await Assert.That(hub).IsNotNull();
        await Assert.That(why).IsNull();
    }

    [Test]
    public async Task The_reason_carrying_overload_agrees_with_the_plain_one()
    {
        // the plain signature must stay a pure view over the same implementation — same verdict, every form/size
        foreach (var form in HubBoxEmitter.Forms)
            foreach (var (w, h) in new[] { (3, 3), (6, 5), (9, 9), (11, 6), (12, 8) })
                foreach (var flip in new[] { false, true })
                {
                    var plain = HubBoxEmitter.Fill(Hub(w, h), form, Cw, flip);
                    var withReason = HubBoxEmitter.Fill(Hub(w, h), form, Cw, flip, out var why);
                    await Assert.That(plain is null).IsEqualTo(withReason is null)
                        .Because($"{form.Form}/{form.Arms} on {w}x{h} flip {flip}");
                    await Assert.That(why is null).IsEqualTo(withReason is not null);
                }
    }

    [Test]
    public async Task A_spawn_box_too_small_reports_the_minimum_in_the_boxs_own_frame()
    {
        // an L spawn needs more than 2x2; the reported minimum is in the box frame, so a lateral mouth needs no
        // transposing by the caller
        var spawn = SpawnBoxEmitter.Fill(Spawn(2, 2), BoxEdge.Left, ShapeFamily.L, Cw, false, "r", out var why);
        await Assert.That(spawn).IsNull();
        await Assert.That(why).IsTypeOf<FillRejection.TooSmall>();
        var min = (FillRejection.TooSmall)why!;
        await Assert.That(min.MinW).IsGreaterThan(0);
        await Assert.That(min.MinH).IsGreaterThan(0);
    }

    [Test]
    public async Task A_frontline_thin_leg_reports_the_fat_L_law_not_a_bare_null()
    {
        // the single-arm frontline refuses a leg no wider than its notch (the banned T's thin band)
        var f = FrontlineBoxEmitter.Fill(
            Front(4, 5), new CompoundRead(Compound.SpineArms, 1), Cw, OfferGrouping.Several, out var why);
        await Assert.That(f).IsNull();
        await Assert.That(why).IsTypeOf<FillRejection.FormDoesNotFit>();
        await Assert.That(((FillRejection.FormDoesNotFit)why!).Detail).Contains("notch");
    }

    [Test]
    public async Task An_unsupported_knob_combination_is_data_not_a_throw()
    {
        // a side-tuck room is supported for I/Z/scythe only — an L asking for one used to escape as an
        // ArgumentException straight through BoxFiller, whose contract says rejections are a data channel
        var res = BoxFiller.Fill(Wool(20, 20), BoxEdge.Top, Cw, ShapeFamily.L,
            roomPlacement: RoomPlacement.SideTuck);
        await Assert.That(res).IsTypeOf<FillResult.UnsupportedKnobs>();
        await Assert.That(res.Rejection).IsTypeOf<FillRejection.UnsupportedKnobs>();
    }

    [Test]
    public async Task Every_approach_rejection_projects_onto_the_shared_vocabulary()
    {
        // one taxonomy across the box kinds: the approach channel's own cases map onto FillRejection, and an Ok
        // maps to no reason at all
        await Assert.That(BoxFiller.Fill(Wool(20, 20), BoxEdge.Top, Cw, ShapeFamily.Scythe).Rejection)
            .IsTypeOf<FillRejection.NotOnMenu>();
        await Assert.That(BoxFiller.Fill(Wool(6, 6), BoxEdge.Top, Cw, ShapeFamily.Donut).Rejection)
            .IsTypeOf<FillRejection.TooSmall>();
        await Assert.That(BoxFiller.Fill(Wool(16, 22), BoxEdge.Top, Cw, ShapeFamily.L).Rejection).IsNull();
    }
}
