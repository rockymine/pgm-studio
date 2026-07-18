using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>G63-C.1 — the offer-consumption seam: the hub emits first as the constraint source, and a
/// neighbour box fills at the width the hub's edge offers (the offered <c>WidthClass</c> is the neighbour's
/// corridor width). Docking an un-offered edge is a directed error.</summary>
public class TeamUnitFillerTests
{
    private static EmittedHub Hub() =>
        HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 36), new CompoundRead(Compound.Rectangle),
            cw: 2, edgeWidths: new Dictionary<BoxEdge, int> { [BoxEdge.Bottom] = 4, [BoxEdge.Right] = 2 })!;

    [Test]
    public async Task Hub_sources_per_edge_widths_the_neighbour_consumes()
    {
        var hub = Hub();
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, BoxEdge.Bottom)).IsEqualTo(4);   // the explicit constraint
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, BoxEdge.Right)).IsEqualTo(2);
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, BoxEdge.Top)).IsEqualTo(BodyEdges.WidthClass(6));  // geometric default
    }

    [Test]
    public async Task Consuming_an_edge_the_hub_did_not_offer_is_a_directed_error()
    {
        // a hub that offers only its Top edge — docking the Bottom has nothing to consume
        var hub = new EmittedHub([],
            [new EdgeOffer(BoxEdge.Top, new EdgeInterval(0, 6, ApproachSlots.Bar), 4, OfferGrouping.Several, "g")],
            new CompoundRead(Compound.Rectangle));
        await Assert.That(() => TeamUnitFiller.ConsumedCw(hub, BoxEdge.Bottom)).Throws<ComposeException>();
    }

    [Test]
    public async Task A_spawn_neighbour_fills_at_the_width_the_hub_offered()
    {
        var hub = Hub();
        // the spawn box sits below the hub, docking the hub's w4 Bottom edge with its Top mouth
        var spawnBox = new Box("spawn-a", BoxKind.Spawn, [0, 6, 4, 10], 40);
        var spawn = TeamUnitFiller.FillSpawn(hub, BoxEdge.Bottom, spawnBox, BoxEdge.Top, ShapeFamily.I, flip: false, "spawn-a-room");

        await Assert.That(spawn).IsNotNull();
        await Assert.That(spawn!.Pieces.All(p => p.Box!.Kind == BoxKind.Spawn)).IsTrue();
        // filled at cw 4: the spawn corridor is four cells wide across the mouth
        await Assert.That(spawn.Pieces.Any(p => p.Rect[2] == 4)).IsTrue();
    }

    [Test]
    public async Task A_wool_neighbour_fills_at_the_hubs_w2_edge_through_the_gated_filler()
    {
        var hub = Hub();
        // wool production is w2-only, so the wool consumes the hub's Right (w2) edge, not the w4 Bottom
        var family = FillMenu.FamiliesFor(2)[0];
        // docks the hub's Right (vertical) edge with its Left mouth: the lane runs rightward into the box width
        var woolBox = new Box("wool-a", BoxKind.Wool, [8, 0, 12, 6], 40);
        var wool = TeamUnitFiller.FillWool(hub, BoxEdge.Right, woolBox, BoxEdge.Left, family, flip: false, "wool-a-room");

        await Assert.That(wool is FillResult.Ok).IsTrue();
    }

    [Test]
    public async Task Fills_an_allocated_hub_spawn_wool_partition_into_a_unit()
    {
        // the allocated partition: hub in the centre, spawn below it (docking the hub's w4 Bottom), a wool to
        // the right (docking the hub's w2 Right) — the hub's width plan riding on the joint offers
        var partition = new BoxPartition(
            [
                new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 36),
                new Box("spawn", BoxKind.Spawn, [0, 6, 6, 12], 72),
                new Box("wool-a", BoxKind.Wool, [8, 0, 14, 6], 84),
            ],
            [
                new BoxJoint("hub", "spawn", new BoxInterface(BoxEdge.Bottom, 0, 6),
                    new EdgeOffer(BoxEdge.Bottom, new EdgeInterval(0, 6, ApproachSlots.Bar), 4, OfferGrouping.Several, "hub-Bottom")),
                new BoxJoint("hub", "wool-a", new BoxInterface(BoxEdge.Right, 0, 6),
                    new EdgeOffer(BoxEdge.Right, new EdgeInterval(0, 6, ApproachSlots.Bar), 2, OfferGrouping.Several, "hub-Right")),
            ]);

        var filled = TeamUnitFiller.Fill(partition, "south", new ComposeRng(1))!;

        await Assert.That(filled.Unit.Spawn.Facing).IsEqualTo("south");                       // the one frame value, threaded through
        await Assert.That(filled.Unit.Wools.Count).IsEqualTo(1);
        // every team-unit box kind emitted its pieces, hub-first
        await Assert.That(filled.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(filled.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Spawn)).IsTrue();
        await Assert.That(filled.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Wool)).IsTrue();
        // the spawn/wool rooms are the placements' pieces
        await Assert.That(filled.Unit.Pieces.Any(p => p.Id == filled.Unit.Spawn.Piece)).IsTrue();
        await Assert.That(filled.Unit.Pieces.Any(p => p.Id == filled.Unit.Wools[0].Piece)).IsTrue();
    }

    [Test]
    public async Task A_partition_with_no_spawn_is_a_directed_null()
    {
        var partition = new BoxPartition(
            [new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 36)], []);
        await Assert.That(TeamUnitFiller.Fill(partition, "south", new ComposeRng(1))).IsNull();
    }
}
