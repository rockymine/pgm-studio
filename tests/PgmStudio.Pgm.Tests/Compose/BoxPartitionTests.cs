using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The partition constraint graph (G63): <see cref="BoxPartition"/> is typed boxes (footprint + land target)
/// and the joints between them, with hard invariants (<see cref="BoxPartition.Valid"/>), and
/// <see cref="BoxPartition.Of"/> the derive-side mirror reading the partition a grown unit implies. Joints are
/// the abutments between footprints (<see cref="BoxPartition.SharedEdge"/>); overlap is legal, a phantom joint
/// is not.
/// </summary>
public sealed class BoxPartitionTests
{
    private static GrownUnit? TryFill(int players, ulong seed)
    {
        var request = new ComposeRequest(players, seed: seed);
        var rng = new ComposeRng(seed);
        var env = Envelope.Derive(request, rng);
        var crossing = MidCarver.BandOnly(env);
        if (TeamUnitAllocator.Allocate(env, rng, crossing) is not { } alloc) return null;
        return TeamUnitFiller.Fill(alloc.Partition, alloc.SpawnFacing, rng)?.Unit;
    }

    [Test]
    public async Task Shared_edge_is_the_abutment_interval_on_the_first_rects_frame()
    {
        // right/left/top/bottom abutments, box-local start on the first rect
        await Assert.That(BoxPartition.SharedEdge([0, 0, 4, 4], [4, 0, 4, 4]))
            .IsEqualTo(new BoxInterface(BoxEdge.Right, 0, 4));
        await Assert.That(BoxPartition.SharedEdge([4, 0, 4, 4], [0, 0, 4, 4]))
            .IsEqualTo(new BoxInterface(BoxEdge.Left, 0, 4));
        await Assert.That(BoxPartition.SharedEdge([0, 0, 4, 4], [0, 4, 4, 4]))
            .IsEqualTo(new BoxInterface(BoxEdge.Bottom, 0, 4));
        await Assert.That(BoxPartition.SharedEdge([0, 4, 4, 4], [0, 0, 4, 4]))
            .IsEqualTo(new BoxInterface(BoxEdge.Top, 0, 4));
        // a partial overlap: the interval is the shared span, offset into the first rect's edge
        await Assert.That(BoxPartition.SharedEdge([0, 0, 4, 10], [4, 3, 4, 4]))
            .IsEqualTo(new BoxInterface(BoxEdge.Right, 3, 4));
    }

    [Test]
    public async Task Shared_edge_is_null_for_a_gap_a_bare_corner_or_interpenetration()
    {
        await Assert.That(BoxPartition.SharedEdge([0, 0, 4, 4], [5, 0, 4, 4])).IsNull();   // gap
        await Assert.That(BoxPartition.SharedEdge([0, 0, 4, 4], [4, 4, 4, 4])).IsNull();   // bare corner
        await Assert.That(BoxPartition.SharedEdge([0, 0, 4, 4], [2, 0, 4, 4])).IsNull();   // overlap, no edge
    }

    [Test]
    public async Task A_well_formed_partition_is_valid()
    {
        var hub = new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 30);
        var spawn = new Box("spawn", BoxKind.Spawn, [0, 6, 6, 8], 30);
        var joint = new BoxJoint("hub", "spawn", BoxPartition.SharedEdge(hub.Rect, spawn.Rect)!);
        await Assert.That(new BoxPartition([hub, spawn], [joint]).Valid()).IsTrue();
    }

    [Test]
    public async Task Invariants_reject_degenerate_boxes_dup_ids_over_budget_land_and_phantom_joints()
    {
        var hub = new Box("hub", BoxKind.Hub, [0, 0, 6, 6], 30);
        var spawn = new Box("spawn", BoxKind.Spawn, [0, 6, 6, 8], 30);
        var joint = new BoxJoint("hub", "spawn", BoxPartition.SharedEdge(hub.Rect, spawn.Rect)!);

        await Assert.That(new BoxPartition([hub with { Rect = [0, 0, 6, 0] }, spawn], []).Valid()).IsFalse();  // degenerate
        await Assert.That(new BoxPartition([hub, hub with { Kind = BoxKind.Wool }], []).Valid()).IsFalse();    // dup id
        await Assert.That(new BoxPartition([hub with { LandTargetCells = 37 }], []).Valid()).IsFalse();        // land > footprint
        await Assert.That(new BoxPartition([hub, spawn], [joint with { BoxB = "ghost" }]).Valid()).IsFalse();  // dangling
        await Assert.That(new BoxPartition([hub, spawn], [new BoxJoint("hub", "hub", joint.Interface)]).Valid()).IsFalse(); // self
        // a joint asserting a contact the footprints do not share
        await Assert.That(new BoxPartition([hub, spawn],
            [joint with { Interface = new BoxInterface(BoxEdge.Top, 0, 6) }]).Valid()).IsFalse();
    }

    [Test]
    public async Task The_mirror_reads_a_valid_partition_off_a_grown_unit()
    {
        for (ulong seed = 1; seed <= 8; seed++)
        {
            if (TryFill(24, seed) is not { } unit) continue;
            var partition = BoxPartition.Of(unit);
            await Assert.That(partition.Valid()).IsTrue();
            // the spine and the wool boxes are all present as typed boxes
            await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Hub)).IsTrue();
            await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Spawn)).IsTrue();
            await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Wool)).IsTrue();
            // every piece landed in exactly one box (the land currency sums to the unit's land)
            await Assert.That(partition.Boxes.Sum(b => b.LandTargetCells))
                .IsEqualTo(unit.Pieces.Sum(p => p.Rect[2] * p.Rect[3]));
        }
    }

    [Test]
    public async Task The_hub_box_is_jointed_to_its_neighbours()
    {
        // the hub is the central box the spawn/frontline/wool boxes dock; across seeds it carries joints, and
        // a spawn box abuts it (the spine) — the constraint graph the mirror recovers is connected at the hub
        var sawSpawnJoint = false;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            if (TryFill(24, seed) is not { } unit) continue;
            var partition = BoxPartition.Of(unit);
            await Assert.That(partition.JointsOf("hub")).IsNotEmpty();
            var spawn = partition.Boxes.First(b => b.Kind == BoxKind.Spawn);
            if (partition.JointsOf("hub").Any(j => j.BoxA == spawn.Id || j.BoxB == spawn.Id)) sawSpawnJoint = true;
        }
        await Assert.That(sawSpawnJoint).IsTrue();
    }
}
