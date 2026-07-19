using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The docking gate (G80): a dock is legal iff the box edge lands on an <c>entry</c> (docking) slot and touches
/// no <c>room</c> (never-dock) slot — <see cref="DockingGate"/> resolving each edge to its slots via the
/// <see cref="BoxInterfaces"/> facts and applying the one table. Every family docks through a single mouth (the
/// clamp too — its two legs meet the host on one edge, the wool clamped inside as a cut cell), so the verdict
/// reads only the edge's slots and is shape-relative — the slots are read off the shape, not a fixed mouth edge.
/// </summary>
public sealed class DockingGateTests
{
    private const int Cw = 2;

    private static IReadOnlyList<BoxEdgeInterface> Edges(ShapeFamily family, int w, int h, bool flip = false, bool woolAtEnd = false)
        => BoxInterfaces.Of(ShapeEmitter.Emit(family, w, h, Cw, flip, woolAtEnd: woolAtEnd), w, h);

    private static DockRejection? Check(IReadOnlyList<BoxEdgeInterface> es, BoxEdge e)
        => DockingGate.Check(es.Single(x => x.Edge == e));

    [Test]
    public async Task Room_is_never_dock_entry_is_the_docking_edge()
    {
        // an I lane docks its entry mouth (top) and never its wool room (bottom)
        var es = Edges(ShapeFamily.I, 6, 12);
        await Assert.That(DockingGate.CanDock(es, BoxEdge.Top)).IsTrue();
        await Assert.That(Check(es, BoxEdge.Bottom)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(DockingGate.DockingEdges(es).Select(e => e.Edge)).IsEquivalentTo(new[] { BoxEdge.Top });
    }

    [Test]
    public async Task Clamp_docks_its_two_leg_mouth_and_never_the_clamped_wool()
    {
        // the redefined clamp docks like a U: its two legs meet the host on the bottom mouth, and the wool is
        // clamped inside as a cut cell — the top, where the wool bridges the legs, seals the wool and never docks
        var es = Edges(ShapeFamily.Clamp, 6, 6);
        await Assert.That(DockingGate.CanDock(es, BoxEdge.Bottom)).IsTrue();          // the two legs meet the host
        await Assert.That(Check(es, BoxEdge.Top)).IsEqualTo(DockRejection.SealsWool); // the clamped wool bridge
    }

    [Test]
    public async Task Corner_clamp_docks_the_two_leg_mouth_and_seals_the_corner_wool()
    {
        // the corner (L+I) variant: the two legs still meet the host on the bottom; the wool sits in the top
        // corner, so the top touches the clamped wool and never docks
        var es = Edges(ShapeFamily.Clamp, 6, 6, woolAtEnd: true);
        await Assert.That(DockingGate.CanDock(es, BoxEdge.Bottom)).IsTrue();
        await Assert.That(Check(es, BoxEdge.Top)).IsEqualTo(DockRejection.SealsWool);   // the corner wool
    }

    [Test]
    public async Task Scythe_docks_the_clean_entry_edge_not_the_room_contaminated_mouth()
    {
        // the emitter's canonical mouth for a scythe is the top edge, but the room also reaches the top-right,
        // so the top is never-dock; the gate finds the clean docking edge off the shape — the entry's left
        // edge, parallel to the entry↔entry-run seam. This is the gate reading slots, not a fixed mouth edge.
        var es = Edges(ShapeFamily.Scythe, 8, 6);
        await Assert.That(ShapeEmitter.MouthEdge(ShapeFamily.Scythe)).IsEqualTo(BoxEdge.Top);
        await Assert.That(DockingGate.DockingEdges(es).Select(e => e.Edge)).IsEquivalentTo(new[] { BoxEdge.Left });
        await Assert.That(Check(es, BoxEdge.Top)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(Check(es, BoxEdge.Bottom)).IsEqualTo(DockRejection.NotAnEntryEdge);
    }

    [Test]
    public async Task U_docks_the_leg_edge_and_never_the_wool_side()
    {
        // U enters at the two legs (the bottom edge); the wool sits flush on the crossbar at the top
        var es = Edges(ShapeFamily.U, 6, 6);
        await Assert.That(DockingGate.CanDock(es, BoxEdge.Bottom)).IsTrue();
        await Assert.That(Check(es, BoxEdge.Top)).IsEqualTo(DockRejection.SealsWool);
    }

    [Test]
    public async Task Check_mouth_admits_a_single_mouth_family_through_its_entry_edge()
    {
        // the single-mouth verdict a filler consults: a terminal-capped family docks cleanly through the edge
        // its entry reaches (I/L its top, U/H/clamp the leg edge at the bottom)
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.I, 6, 12), BoxEdge.Top)).IsNull();
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.L, 8, 8), BoxEdge.Top)).IsNull();
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.U, 6, 6), BoxEdge.Bottom)).IsNull();
        await Assert.That(DockingGate.CheckMouth(Edges(ShapeFamily.Clamp, 6, 6), BoxEdge.Bottom)).IsNull();
    }

    [Test]
    public async Task Check_mouth_rejects_the_scythes_room_contaminated_canonical_mouth()
    {
        // docking the scythe through its canonical top mouth seals the bay against the host (WL8) — the mouth
        // verdict catches it as SealsWool even though the scythe has a clean docking edge elsewhere (the left)
        var es = Edges(ShapeFamily.Scythe, 8, 6);
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Top)).IsEqualTo(DockRejection.SealsWool);
        await Assert.That(DockingGate.CheckMouth(es, BoxEdge.Bottom)).IsEqualTo(DockRejection.NotAnEntryEdge);
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
