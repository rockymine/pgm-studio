using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The hub box kind: a terminal-free solid Rectangle body publishing one per-edge <see cref="EdgeOffer"/>
/// as the constraint source — no room, hub-labeled pieces, the offered widths the composer's per-edge constraint.</summary>
public class HubBoxEmitterTests
{
    [Test]
    public async Task Rectangle_hub_fills_the_box_terminal_free_and_hub_labeled()
    {
        var box = new Box("hub", BoxKind.Hub, [10, 20, 6, 5], 30);
        var hub = HubBoxEmitter.Fill(box, new CompoundRead(Compound.Rectangle));

        // one solid terrain piece covering the whole box, no terminal/room, labeled to the hub box, Bar slot
        await Assert.That(hub.Pieces.Count).IsEqualTo(1);
        await Assert.That(hub.Pieces[0].Rect).IsEquivalentTo(new[] { 10, 20, 6, 5 });
        await Assert.That(hub.Pieces[0].Box!.Kind).IsEqualTo(BoxKind.Hub);
        await Assert.That(hub.Pieces[0].Slot).IsEqualTo(ApproachSlots.Bar);
        await Assert.That(hub.Pieces.Any(p => p.Slot == ApproachSlots.Room)).IsFalse();
        await Assert.That(hub.Form.Form).IsEqualTo(Compound.Rectangle);
    }

    [Test]
    public async Task Rectangle_hub_publishes_one_offer_per_edge_severally()
    {
        var box = new Box("hub", BoxKind.Hub, [0, 0, 6, 5], 30);
        var hub = HubBoxEmitter.Fill(box, new CompoundRead(Compound.Rectangle));

        await Assert.That(hub.Offers.Count).IsEqualTo(4);
        await Assert.That(hub.Offers.Select(o => o.Edge))
            .IsEquivalentTo(new[] { BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right });
        // every hub edge is offered severally — each neighbour docks its own edge, no joint span
        await Assert.That(hub.Offers.All(o => o.Grouping == OfferGrouping.Several)).IsTrue();
        // the interval is the whole free edge: top/bottom span the width (6), left/right the height (5)
        var top = hub.Offers.Single(o => o.Edge == BoxEdge.Top);
        var left = hub.Offers.Single(o => o.Edge == BoxEdge.Left);
        await Assert.That(top.Interval.LengthCells).IsEqualTo(6);
        await Assert.That(left.Interval.LengthCells).IsEqualTo(5);
        // each offer is its own group
        await Assert.That(hub.Offers.Select(o => o.GroupId).Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Per_edge_width_is_the_composer_constraint_defaulting_to_the_edge_width_class()
    {
        var box = new Box("hub", BoxKind.Hub, [0, 0, 8, 5], 40);
        var hub = HubBoxEmitter.Fill(box, new CompoundRead(Compound.Rectangle),
            new Dictionary<BoxEdge, int> { [BoxEdge.Top] = 4 });

        // the sourced constraint: Top is the composer's explicit w4
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Top).WidthClass).IsEqualTo(4);
        // an unset edge offers its own free width class: Bottom spans 8 -> w6 (nearest rung), Left/Right span 5 -> w4 (tie, smaller)
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Bottom).WidthClass).IsEqualTo(BodyEdges.WidthClass(8));
        await Assert.That(hub.Offers.Single(o => o.Edge == BoxEdge.Left).WidthClass).IsEqualTo(BodyEdges.WidthClass(5));
    }

    [Test]
    public async Task Unbuilt_forms_are_a_directed_signal_not_a_silent_wrong_body()
    {
        var box = new Box("hub", BoxKind.Hub, [0, 0, 8, 8], 64);
        await Assert.That(() => HubBoxEmitter.Fill(box, new CompoundRead(Compound.Ring)))
            .Throws<ComposeException>();
    }
}
