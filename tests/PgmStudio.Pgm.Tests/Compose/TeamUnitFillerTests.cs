using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>G63-C.1 — the offer-consumption seam: the hub emits first as the constraint source, and a
/// neighbour box fills at the width <b>its own joint</b> was granted (that offer's <c>WidthClass</c> is the
/// neighbour's corridor width). Docking an un-offered edge is a directed error.</summary>
public class TeamUnitFillerTests
{
    private static EmittedHub Hub() =>
        HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 36), new CompoundRead(Compound.Rectangle), cw: 2)!;

    /// <summary>A hub joint granting <paramref name="width"/> over the dock <c>[start, start+len)</c> on
    /// <paramref name="edge"/> — the allocator's per-dock grant.</summary>
    private static BoxJoint Joint(BoxEdge edge, int width, int start = 0, int len = 6, string nb = "nb") =>
        new("hub", nb, new BoxInterface(edge, start, len),
            new EdgeOffer(edge, new EdgeInterval(start, len, ApproachSlots.Bar), width, OfferGrouping.Several, $"hub-{edge}"));

    [Test]
    public async Task A_neighbour_consumes_the_width_its_own_joint_was_granted()
    {
        var hub = Hub();
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, Joint(BoxEdge.Bottom, 4), BoxEdge.Bottom)).IsEqualTo(4);
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, Joint(BoxEdge.Right, 2), BoxEdge.Right)).IsEqualTo(2);
    }

    [Test]
    public async Task Two_neighbours_on_one_edge_each_consume_their_own_width()
    {
        // the third wool doubles onto the spawn's side: same hub edge, same run on a solid hub, two widths.
        // An edge- (or run-) keyed lookup would hand one of them the other's cw.
        var hub = Hub();
        var spawnJoint = Joint(BoxEdge.Bottom, 3, start: 0, len: 3, nb: "spawn");
        var woolJoint = Joint(BoxEdge.Bottom, 2, start: 3, len: 2, nb: "wool-c");

        await Assert.That(TeamUnitFiller.ConsumedCw(hub, spawnJoint, BoxEdge.Bottom)).IsEqualTo(3);
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, woolJoint, BoxEdge.Bottom)).IsEqualTo(2);
    }

    [Test]
    public async Task An_ungranted_joint_falls_back_to_what_its_run_can_support()
    {
        // a derived (not allocated) partition carries no offer — the dock reads the capacity of the run it
        // lands on: the U's Bottom edge is two 2-cell leg tips, its Top one full 6-cell run
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 36),
            new CompoundRead(Compound.SpineArms, 2), cw: 2)!;
        var onTip = new BoxJoint("hub", "nb", new BoxInterface(BoxEdge.Bottom, 4, 2), null);
        var onSpine = new BoxJoint("hub", "nb", new BoxInterface(BoxEdge.Top, 0, 6), null);

        await Assert.That(TeamUnitFiller.ConsumedCw(hub, onTip, BoxEdge.Bottom)).IsEqualTo(BodyEdges.WidthClass(2));
        await Assert.That(TeamUnitFiller.ConsumedCw(hub, onSpine, BoxEdge.Top)).IsEqualTo(BodyEdges.WidthClass(6));
    }

    [Test]
    public async Task Consuming_an_edge_the_hub_did_not_offer_is_a_directed_error()
    {
        // a hub that offers only its Top edge — an ungranted dock on the Bottom has nothing to consume
        var hub = new EmittedHub([],
            [new EdgeOffer(BoxEdge.Top, new EdgeInterval(0, 6, ApproachSlots.Bar), 4, OfferGrouping.Several, "g")],
            new CompoundRead(Compound.Rectangle));
        var ungranted = new BoxJoint("hub", "nb", new BoxInterface(BoxEdge.Bottom, 0, 6), null);
        await Assert.That(() => TeamUnitFiller.ConsumedCw(hub, ungranted, BoxEdge.Bottom)).Throws<ComposeException>();
    }

    [Test]
    public async Task A_spawn_neighbour_fills_at_the_width_the_hub_offered()
    {
        var hub = Hub();
        // the spawn box sits below the hub, docking the hub's Bottom edge with its Top mouth, granted w4
        var spawnBox = new Box("spawn-a", BoxKind.Spawn, [0, 6, 4, 10], 40);
        var spawn = TeamUnitFiller.FillSpawn(hub, Joint(BoxEdge.Bottom, 4), BoxEdge.Bottom, spawnBox, BoxEdge.Top,
            ShapeFamily.I, flip: false, "spawn-a-room");

        await Assert.That(spawn).IsNotNull();
        await Assert.That(spawn!.Pieces.All(p => p.Box!.Kind == BoxKind.Spawn)).IsTrue();
        // filled at cw 4: the spawn corridor is four cells wide across the mouth
        await Assert.That(spawn.Pieces.Any(p => p.Rect[2] == 4)).IsTrue();
    }

    [Test]
    public async Task A_wool_neighbour_fills_at_the_hubs_w2_edge_through_the_gated_filler()
    {
        var hub = Hub();
        // wool production is w2-only, so the wool's joint grants w2
        var family = FillMenu.FamiliesFor(2)[0];
        // docks the hub's Right (vertical) edge with its Left mouth: the lane runs rightward into the box width
        var woolBox = new Box("wool-a", BoxKind.Wool, [8, 0, 12, 6], 40);
        var wool = TeamUnitFiller.FillWool(hub, Joint(BoxEdge.Right, 2), BoxEdge.Right, woolBox, BoxEdge.Left,
            family, flip: false, "wool-a-room");

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

    [Test]
    public async Task Fills_a_frontline_join_carrying_its_face_offers_for_the_mid()
    {
        // hub with a spawn behind (Bottom) and a frontline in front (Top), docking the hub's w4 front edge
        var partition = new BoxPartition(
            [
                new Box("hub", BoxKind.Hub, [0, 10, 6, 6], 36),
                new Box("spawn", BoxKind.Spawn, [0, 16, 6, 12], 72),
                new Box("frontline", BoxKind.Frontline, [0, 0, 12, 10], 120),
            ],
            [
                new BoxJoint("hub", "spawn", new BoxInterface(BoxEdge.Bottom, 0, 6),
                    new EdgeOffer(BoxEdge.Bottom, new EdgeInterval(0, 6, ApproachSlots.Bar), 4, OfferGrouping.Several, "hub-Bottom")),
                new BoxJoint("hub", "frontline", new BoxInterface(BoxEdge.Top, 0, 6),
                    new EdgeOffer(BoxEdge.Top, new EdgeInterval(0, 6, ApproachSlots.Bar), 4, OfferGrouping.Several, "hub-Top")),
            ]);

        var filled = TeamUnitFiller.Fill(partition, "north", new ComposeRng(3))!;

        // the frontline emitted its terrain but no room (a join, not a placement), and offered its face to the mid
        await Assert.That(filled.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Frontline)).IsTrue();
        await Assert.That(filled.Unit.Wools.Count).IsEqualTo(0);
        await Assert.That(filled.FrontlineFace.Count).IsGreaterThan(0);
        // it docks the hub's Top edge, so its spine orients to its Bottom mouth and the face is the opposite Top
        // edge — toward the axis, away from the hub
        await Assert.That(filled.FrontlineFace.All(o => o.Edge == BoxEdge.Top)).IsTrue();
    }
}
