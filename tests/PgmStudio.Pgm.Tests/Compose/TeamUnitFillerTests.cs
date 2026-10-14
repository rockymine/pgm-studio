using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The offer-consumption seam: the hub emits first as the constraint source, and a
/// neighbour box fills at the width <b>its own joint</b> was granted (that offer's <c>WidthClass</c> is the
/// neighbour's lane width). Docking an un-offered edge is a directed error.</summary>
public class TeamUnitFillerTests
{
    private static readonly Box HubBox = new("hub", BoxKind.Hub, new(0, 0, 6, 6), 36);

    private static Compound Hub() =>
        HubBoxEmitter.Fill(HubBox, new CompoundRead(CompoundForm.Rectangle), laneWidth: 2)!;

    /// <summary>A hub joint granting <paramref name="width"/> over the dock <c>[start, start+len)</c> on
    /// <paramref name="edge"/> — the allocator's per-dock grant.</summary>
    private static BoxJoint Joint(BoxEdge edge, int width, int start = 0, int len = 6, string nb = "nb") =>
        new("hub", nb, new EdgeInterval(edge, start, len), width);

    [Test]
    public async Task A_neighbour_consumes_the_width_its_own_joint_was_granted()
    {
        var hub = Hub();
        await Assert.That(TeamUnitFiller.GrantedLaneWidth(HubBox, hub, Joint(BoxEdge.Bottom, 4), BoxEdge.Bottom)).IsEqualTo(4);
        await Assert.That(TeamUnitFiller.GrantedLaneWidth(HubBox, hub, Joint(BoxEdge.Right, 2), BoxEdge.Right)).IsEqualTo(2);
    }

    [Test]
    public async Task Two_neighbours_on_one_edge_each_consume_their_own_width()
    {
        // the third wool doubles onto the spawn's side: same hub edge, same run on a solid hub, two widths.
        // An edge- (or run-) keyed lookup would hand one of them the other's laneWidth.
        var hub = Hub();
        var spawnJoint = Joint(BoxEdge.Bottom, 3, start: 0, len: 3, nb: "spawn");
        var woolJoint = Joint(BoxEdge.Bottom, 2, start: 3, len: 2, nb: "wool-c");

        await Assert.That(TeamUnitFiller.GrantedLaneWidth(HubBox, hub, spawnJoint, BoxEdge.Bottom)).IsEqualTo(3);
        await Assert.That(TeamUnitFiller.GrantedLaneWidth(HubBox, hub, woolJoint, BoxEdge.Bottom)).IsEqualTo(2);
    }

    [Test]
    public async Task An_ungranted_joint_falls_back_to_what_its_run_can_support()
    {
        // a derived (not allocated) partition carries no offer — the dock reads the capacity of the run it
        // lands on: the U's Bottom edge is two 2-cell leg tips, its Top one full 6-cell run
        var hub = HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, new(0, 0, 6, 6), 36),
            new CompoundRead(CompoundForm.SpineArms, 2), laneWidth: 2)!;
        var onTip = new BoxJoint("hub", "nb", new EdgeInterval(BoxEdge.Bottom, 4, 2), null);
        var onSpine = new BoxJoint("hub", "nb", new EdgeInterval(BoxEdge.Top, 0, 6), null);

        await Assert.That(TeamUnitFiller.GrantedLaneWidth(HubBox, hub, onTip, BoxEdge.Bottom)).IsEqualTo(CompoundEdges.LaneWidthOn(2));
        await Assert.That(TeamUnitFiller.GrantedLaneWidth(HubBox, hub, onSpine, BoxEdge.Top)).IsEqualTo(CompoundEdges.LaneWidthOn(6));
    }

    [Test]
    public async Task Consuming_an_edge_the_hub_did_not_offer_is_a_directed_error()
    {
        // a hub that offers only its Top edge — an ungranted dock on the Bottom has nothing to consume
        var hub = new Compound([new Piece(new(0, 0, 6, 1), CompoundParts.Bar)], []);
        var ungranted = new BoxJoint("hub", "nb", new EdgeInterval(BoxEdge.Bottom, 0, 6), null);
        await Assert.That(() => TeamUnitFiller.GrantedLaneWidth(HubBox, hub, ungranted, BoxEdge.Bottom)).Throws<ComposeException>();
    }

    [Test]
    public async Task A_spawn_neighbour_fills_at_the_width_the_hub_offered()
    {
        var hub = Hub();
        // the spawn box sits below the hub, docking the hub's Bottom edge with its Top mouth, granted w4
        var spawnBox = new Box("spawn-a", BoxKind.Spawn, new(0, 6, 4, 10), 40);
        var laneWidth = TeamUnitFiller.GrantedLaneWidth(HubBox, hub, Joint(BoxEdge.Bottom, 4), BoxEdge.Bottom);
        var spawn = SpawnBoxEmitter.Fill(spawnBox, BoxEdge.Top, ShapeFamily.I, laneWidth, flip: false);

        await Assert.That(spawn).IsNotNull();
        // filled at laneWidth 4: the spawn corridor is four cells wide across the mouth
        await Assert.That(spawn!.Pieces.Any(p => p.Rect.Width == 4)).IsTrue();
    }

    [Test]
    public async Task A_wool_neighbour_fills_at_the_hubs_w2_edge_through_the_gated_filler()
    {
        var hub = Hub();
        // wool production is w2-only, so the wool's joint grants w2
        var family = FillMenu.FamiliesFor(2, 2)[0];
        // docks the hub's Right (vertical) edge with its Left mouth: the lane runs rightward into the box width
        var woolBox = new Box("wool-a", BoxKind.Wool, new(8, 0, 12, 6), 40);
        var laneWidth = TeamUnitFiller.GrantedLaneWidth(HubBox, hub, Joint(BoxEdge.Right, 2), BoxEdge.Right);
        await Assert.That(BoxFiller.Fill(woolBox, BoxEdge.Left, laneWidth, family, out _)).IsNotNull();
    }

    [Test]
    public async Task Fills_an_allocated_hub_spawn_wool_partition_into_a_unit()
    {
        // the allocated partition: hub in the centre, spawn below it (docking the hub's w4 Bottom), a wool to
        // the right (docking the hub's w2 Right) — the hub's width plan riding on the joint offers
        var partition = new BoxLayout(
            [
                new Box("hub", BoxKind.Hub, new(0, 0, 6, 6), 36),
                new Box("spawn", BoxKind.Spawn, new(0, 6, 6, 12), 72),
                new Box("wool-a", BoxKind.Wool, new(8, 0, 14, 6), 84),
            ],
            [
                new BoxJoint("hub", "spawn", new EdgeInterval(BoxEdge.Bottom, 0, 6), 4),
                new BoxJoint("hub", "wool-a", new EdgeInterval(BoxEdge.Right, 0, 6), 2),
            ]);

        var filled = TeamUnitFiller.Fill(partition, new ComposeRng(1))!;

        await Assert.That(filled.SpawnFacing).IsEqualTo("front");                       // behind the hub, it faces through it
        await Assert.That(filled.Wools.Count).IsEqualTo(1);
        // every team-unit box kind emitted its pieces, hub-first
        await Assert.That(filled.Pieces.Any(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(filled.Pieces.Any(p => p.Box!.Kind == BoxKind.Spawn)).IsTrue();
        await Assert.That(filled.Pieces.Any(p => p.Box!.Kind == BoxKind.Wool)).IsTrue();
        // the spawn/wool rooms are the placements' pieces
        await Assert.That(filled.Pieces.Any(p => p.Id == filled.Spawn.Piece)).IsTrue();
        await Assert.That(filled.Pieces.Any(p => p.Id == filled.Wools[0].Piece)).IsTrue();
    }

    [Test]
    public async Task A_partition_with_no_spawn_is_a_directed_null()
    {
        var partition = new BoxLayout(
            [new Box("hub", BoxKind.Hub, new(0, 0, 6, 6), 36)], []);
        await Assert.That(TeamUnitFiller.Fill(partition, new ComposeRng(1))).IsNull();
    }

    [Test]
    public async Task Fills_a_frontline_join_with_its_face_toward_the_axis()
    {
        // hub with a spawn behind (Bottom) and a frontline in front (Top), docking the hub's w4 front edge
        var partition = new BoxLayout(
            [
                new Box("hub", BoxKind.Hub, new(0, 10, 6, 6), 36),
                new Box("spawn", BoxKind.Spawn, new(0, 16, 6, 12), 72),
                new Box("frontline", BoxKind.Frontline, new(0, 0, 12, 10), 120),
            ],
            [
                new BoxJoint("hub", "spawn", new EdgeInterval(BoxEdge.Bottom, 0, 6), 4),
                new BoxJoint("hub", "frontline", new EdgeInterval(BoxEdge.Top, 0, 6), 4),
            ]);

        var filled = TeamUnitFiller.Fill(partition, new ComposeRng(3))!;

        // the frontline emitted its terrain but no room (a join, not a placement)
        var front = filled.Pieces.Where(p => p.Box!.Kind == BoxKind.Frontline).ToList();
        await Assert.That(front).IsNotEmpty();
        await Assert.That(filled.Wools.Count).IsEqualTo(0);
        // it docks the hub's Top edge, so its spine lies along its Bottom edge against the hub and its tips reach
        // the opposite Top edge — toward the axis, away from the hub
        await Assert.That(front.Any(p => p.Rect.Z + p.Rect.Height == 10)).IsTrue();
        await Assert.That(front.Any(p => p.Rect.Z == 0)).IsTrue();
    }
}
