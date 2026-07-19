using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The hub box kind: a terminal-free <see cref="Compound"/> body (Rectangle · L · U · Ring · Double-hole)
/// finished by the hub designation — no room, hub-labeled pieces, publishing one per-edge <see cref="EdgeOffer"/>
/// as the constraint source (the offered widths the composer's per-edge constraint).</summary>
public class HubBoxEmitterTests
{
    private static readonly Box Box6x5 = new("hub", BoxKind.Hub, [10, 20, 6, 5], 30);

    [Test]
    public async Task Rectangle_hub_fills_the_box_terminal_free_and_hub_labeled()
    {
        var hub = HubBoxEmitter.Fill(Box6x5, new CompoundRead(Compound.Rectangle), cw: 2)!;

        // one solid terrain piece covering the whole box, no terminal/room, labeled to the hub box, Bar slot
        await Assert.That(hub.Pieces.Count).IsEqualTo(1);
        await Assert.That(hub.Pieces[0].Rect).IsEquivalentTo(new[] { 10, 20, 6, 5 });
        await Assert.That(hub.Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(hub.Pieces.Any(p => p.Slot == ApproachSlots.Room)).IsFalse();
        await Assert.That(hub.Form.Form).IsEqualTo(Compound.Rectangle);
    }

    [Test]
    public async Task Rectangle_hub_publishes_one_offer_per_edge_severally()
    {
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 6, 5], 30), new CompoundRead(Compound.Rectangle), cw: 2)!;

        await Assert.That(hub.Offers.Count).IsEqualTo(4);
        await Assert.That(hub.Offers.Select(o => o.Edge))
            .IsEquivalentTo(new[] { BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right });
        await Assert.That(hub.Offers.All(o => o.Grouping == OfferGrouping.Several)).IsTrue();
        // the interval is the whole free edge: top/bottom span the width (6), left/right the height (5)
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Top).Interval.LengthCells).IsEqualTo(6);
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Left).Interval.LengthCells).IsEqualTo(5);
        await Assert.That(hub.Offers.Select(o => o.GroupId).Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Every_run_offers_the_width_its_own_length_can_support()
    {
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 8, 5], 40), new CompoundRead(Compound.Rectangle),
            cw: 2)!;

        // the hub publishes capacity, not a per-neighbour agreement: Top/Bottom span 8, Left/Right span 5. What a
        // given neighbour was granted rides on its joint (TeamUnitFillerTests), because one run can carry two.
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Top).WidthClass).IsEqualTo(BodyEdges.WidthClass(8));
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Bottom).WidthClass).IsEqualTo(BodyEdges.WidthClass(8));
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Left).WidthClass).IsEqualTo(BodyEdges.WidthClass(5));
    }

    [Test]
    public async Task L_hub_is_a_spine_with_one_end_arm_its_side_wall_one_merged_run()
    {
        // spine [0,0,8,2] + one arm [0,2,2,4]; the left edge (spine corner + arm) merges to one full-height run
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 8, 6], 48), new CompoundRead(Compound.SpineArms, 1), cw: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(2);
        await Assert.That(hub.Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(hub.Offers.Count).IsEqualTo(4);                                     // top, bottom, left, right — one run each
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Left).Interval.LengthCells).IsEqualTo(6);
    }

    [Test]
    public async Task U_hub_opens_a_bay_two_offers_on_the_open_edge()
    {
        // spine [0,0,8,2] + arms [0,2,2,4] and [6,2,2,4]; the bottom edge is the bay — two arm-tip runs with a gap
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 8, 6], 48), new CompoundRead(Compound.SpineArms, 2), cw: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(3);
        await Assert.That(hub.Offers.Count(o => o.Edge == BoxEdge.Bottom)).IsEqualTo(2);
        await Assert.That(hub.Offers.Count(o => o.Edge == BoxEdge.Top)).IsEqualTo(1);
        await Assert.That(hub.Offers.Where(o => o.Edge == BoxEdge.Bottom).All(o => o.Interval.LengthCells == 2)).IsTrue();
    }

    [Test]
    public async Task Ring_hub_walls_a_hole_and_offers_four_full_edges()
    {
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 9, 9], 81), new CompoundRead(Compound.Ring), cw: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(4);                                     // four ring bars
        await Assert.That(hub.Form.Form).IsEqualTo(Compound.Ring);
        // every outer edge is a solid wall → one full-length run each (corner + leg + corner merged)
        await Assert.That(hub.Offers.Count).IsEqualTo(4);
        await Assert.That(hub.Offers.All(o => o.Interval.LengthCells == 9)).IsTrue();
    }

    [Test]
    public async Task DoubleHole_hub_builds_two_holes_hub_labeled()
    {
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 12, 8], 96), new CompoundRead(Compound.DoubleHole), cw: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(7);                                     // ring (4) + U (top/bottom arms + outer wall)
        await Assert.That(hub.Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(hub.Form.Form).IsEqualTo(Compound.DoubleHole);
        await Assert.That(hub.Offers.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Too_small_a_box_is_a_directed_null_not_a_throw()
    {
        // a 3x3 box cannot hold a ring at cw 2 (needs 2cw+1 = 5 each way)
        await Assert.That(HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 3, 3], 9), new CompoundRead(Compound.Ring), cw: 2))
            .IsNull();
    }

    [Test]
    public async Task A_form_off_the_hub_menu_throws()
    {
        // P and TwoUOnI are body compounds but not hub forms (the hub stays rectangle-ish)
        await Assert.That(() => HubBoxEmitter.Fill(Box6x5, new CompoundRead(Compound.P), cw: 2))
            .Throws<ComposeException>();
    }
}
