using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The docking gate (G80): a dock is legal iff the box edge lands on an <c>entry</c> (docking) slot, touches no
/// <c>room</c> (never-dock) slot, and satisfies the family's span demand — <see cref="DockingGate"/> resolving
/// each edge to its slots via the <see cref="BoxInterfaces"/> facts and applying the one table. The hard cases
/// (the clamp's two short bars, the scythe's clean entry edge) are just rows of it, and validity is
/// shape-relative because the slots are read off the shape, not a fixed mouth edge.
/// </summary>
public sealed class DockingGateTests
{
    private const int Cw = 2;

    private static IReadOnlyList<BoxEdgeInterface> Edges(ShapeFamily family, int w, int h, bool flip = false)
        => BoxInterfaces.Of(ShapeEmitter.Emit(family, w, h, Cw, flip), w, h);

    private static DockRejection? Check(IReadOnlyList<BoxEdgeInterface> es, BoxEdge e, ShapeFamily f)
        => DockingGate.Check(es.Single(x => x.Edge == e), f);

    [Test]
    public async Task Room_is_never_dock_entry_is_the_docking_edge()
    {
        // an I lane docks its entry mouth (top) and never its wool room (bottom)
        var es = Edges(ShapeFamily.I, 6, 12);
        await Assert.That(DockingGate.CanDock(es, ShapeFamily.I, BoxEdge.Top)).IsTrue();
        await Assert.That(Check(es, BoxEdge.Bottom, ShapeFamily.I)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(DockingGate.DockingEdges(es, ShapeFamily.I).Select(e => e.Edge))
            .IsEquivalentTo(new[] { BoxEdge.Top });
        await Assert.That(DockingGate.MeetsDemand(es, ShapeFamily.I)).IsTrue();
    }

    [Test]
    public async Task Clamp_docks_both_bars_on_the_short_edge_and_demands_two()
    {
        // the two parallel bars are the entries on the short top/bottom edges; the long left is the bay, the
        // long right is the wool wall — a full short-edge host closes the bay into a declared hole
        var es = Edges(ShapeFamily.Clamp, 4, 5);
        await Assert.That(DockingGate.DockingEdges(es, ShapeFamily.Clamp).Select(e => e.Edge))
            .IsEquivalentTo(new[] { BoxEdge.Top, BoxEdge.Bottom });
        await Assert.That(Check(es, BoxEdge.Left, ShapeFamily.Clamp)).IsEqualTo(DockRejection.WrongSpan);   // the bay (long)
        await Assert.That(Check(es, BoxEdge.Right, ShapeFamily.Clamp)).IsEqualTo(DockRejection.SealsWool);  // the wool wall
        await Assert.That(DockingGate.MeetsDemand(es, ShapeFamily.Clamp)).IsTrue();                         // both bars reachable
    }

    [Test]
    public async Task Scythe_docks_the_clean_entry_edge_not_the_room_contaminated_mouth()
    {
        // the emitter's canonical mouth for a scythe is the top edge, but the room also reaches the top-right,
        // so the top is never-dock; the gate finds the clean docking edge off the shape — the entry's left
        // edge, parallel to the entry↔entry-run seam. This is the gate reading slots, not a fixed mouth edge.
        var es = Edges(ShapeFamily.Scythe, 8, 6);
        await Assert.That(ShapeEmitter.MouthEdge(ShapeFamily.Scythe)).IsEqualTo(BoxEdge.Top);
        await Assert.That(DockingGate.DockingEdges(es, ShapeFamily.Scythe).Select(e => e.Edge))
            .IsEquivalentTo(new[] { BoxEdge.Left });
        await Assert.That(Check(es, BoxEdge.Top, ShapeFamily.Scythe)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(Check(es, BoxEdge.Bottom, ShapeFamily.Scythe)).IsEqualTo(DockRejection.NotAnEntryEdge);
    }

    [Test]
    public async Task U_docks_the_leg_edge_and_never_the_wool_side()
    {
        // U enters at the two legs (the bottom edge); the wool sits flush on the crossbar at the top
        var es = Edges(ShapeFamily.U, 6, 6);
        await Assert.That(DockingGate.CanDock(es, ShapeFamily.U, BoxEdge.Bottom)).IsTrue();
        await Assert.That(Check(es, BoxEdge.Top, ShapeFamily.U)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(DockingGate.MeetsDemand(es, ShapeFamily.U)).IsTrue();
    }

    [Test]
    public async Task The_verdict_tracks_the_room_when_the_shape_flips()
    {
        // flipping the clamp mirrors x: the wool wall moves from the right edge to the left, the bay from left
        // to right. The dockable short edges are unchanged, but which long edge seals the wool follows the room
        var normal = Edges(ShapeFamily.Clamp, 4, 5);
        var flipped = Edges(ShapeFamily.Clamp, 4, 5, flip: true);
        await Assert.That(Check(normal, BoxEdge.Right, ShapeFamily.Clamp)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(Check(flipped, BoxEdge.Left, ShapeFamily.Clamp)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(Check(flipped, BoxEdge.Right, ShapeFamily.Clamp)).IsEqualTo(DockRejection.WrongSpan);
    }

    [Test]
    public async Task Check_mouth_admits_a_single_mouth_family_through_its_entry_edge()
    {
        // the single-mouth verdict a filler consults: a terminal-capped family docks cleanly through the edge
        // its entry reaches (I/L/Z its top, U/H the leg edge), demand 1 met by the one mouth
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.I, 6, 12), BoxEdge.Top, ShapeFamily.I)).IsNull();
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.L, 8, 8), BoxEdge.Top, ShapeFamily.L)).IsNull();
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.U, 6, 6), BoxEdge.Bottom, ShapeFamily.U)).IsNull();
    }

    [Test]
    public async Task Check_mouth_rejects_a_single_mouth_dock_of_a_dual_host_family()
    {
        // the clamp's short bars each pass the per-edge Check, but it demands TWO distinct hosts — a single
        // mouth can't satisfy the dual-host corner-wrap, so the mouth verdict is UnmetDemand (the partition
        // graph places it, not a single-mouth fill)
        var es = Edges(ShapeFamily.Clamp, 4, 5);
        await Assert.That(DockingGate.Check(es.Single(x => x.Edge == BoxEdge.Top), ShapeFamily.Clamp)).IsNull();
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Top, ShapeFamily.Clamp)).IsEqualTo(DockRejection.UnmetDemand);
        // the mouth's own illegality still wins ahead of the demand: the bay edge is the wrong span, the wall seals
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Left, ShapeFamily.Clamp)).IsEqualTo(DockRejection.WrongSpan);
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Right, ShapeFamily.Clamp)).IsEqualTo(DockRejection.SealsWool);
    }

    [Test]
    public async Task Check_mouth_rejects_the_scythes_room_contaminated_canonical_mouth()
    {
        // docking the scythe through its canonical top mouth seals the bay against the host (WL8) — the mouth
        // verdict catches it as SealsWool even though the scythe has a clean docking edge elsewhere (the left)
        var es = Edges(ShapeFamily.Scythe, 8, 6);
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Top, ShapeFamily.Scythe)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Bottom, ShapeFamily.Scythe)).IsEqualTo(DockRejection.NotAnEntryEdge);
    }

    [Test]
    public async Task Approach_slot_roles_map_room_to_never_dock_entry_to_docking_rest_internal()
    {
        await Assert.That(DockingGate.Role(Designation.Approach, ApproachSlots.Room)).IsEqualTo(SlotDockRole.NeverDock);
        await Assert.That(DockingGate.Role(Designation.Approach, ApproachSlots.Entry)).IsEqualTo(SlotDockRole.DockingEdge);
        foreach (var s in new[] { ApproachSlots.Run, ApproachSlots.Bar, ApproachSlots.Leg,
                     ApproachSlots.EntryRun, ApproachSlots.RoomRun, ApproachSlots.EntryBar, ApproachSlots.RoomBar })
            await Assert.That(DockingGate.Role(Designation.Approach, s)).IsEqualTo(SlotDockRole.Internal);
    }

    [Test]
    public async Task Hub_and_frontline_designations_dock_their_mark_veto_nothing()
    {
        // no terminal on either → nothing never-docks; the mark is the docking edge, structural slots internal
        await Assert.That(DockingGate.Role(Designation.Hub, DesignationMarks.Interface)).IsEqualTo(SlotDockRole.DockingEdge);
        await Assert.That(DockingGate.Role(Designation.Frontline, DesignationMarks.Face)).IsEqualTo(SlotDockRole.DockingEdge);
        foreach (var s in new[] { ApproachSlots.Run, ApproachSlots.Bar, ApproachSlots.Leg })
        {
            await Assert.That(DockingGate.Role(Designation.Hub, s)).IsEqualTo(SlotDockRole.Internal);
            await Assert.That(DockingGate.Role(Designation.Frontline, s)).IsEqualTo(SlotDockRole.Internal);
        }
        // a mark belongs to its designation: the approach terminal is not a hub/frontline concern (no veto)
        await Assert.That(DockingGate.Role(Designation.Hub, ApproachSlots.Room)).IsEqualTo(SlotDockRole.Internal);
        await Assert.That(DockingGate.Role(Designation.Frontline, ApproachSlots.Room)).IsEqualTo(SlotDockRole.Internal);
    }
}
