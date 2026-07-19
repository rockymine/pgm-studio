using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The dock role a template slot plays when a neighbour box meets the edge it sits on — the frozen
/// docking law as data (docs/contracts/layout-rules.md). <see cref="DockingEdge"/>: an <c>entry</c> is where a
/// host connects (its mouth). <see cref="NeverDock"/>: the wool <c>room</c> — a dock there seals the goal, so
/// it never connects at this stage (it may, legally, at the elevation stage G81, which is why this is a rule,
/// not a fact of the edge). <see cref="Internal"/>: a <c>run</c>/<c>bar</c>/<c>leg</c> (and the entry/room
/// qualified runs and bars) is shape-internal corridor — it neither offers a dock nor forbids one.</summary>
public enum SlotDockRole { DockingEdge, NeverDock, Internal }

/// <summary>Why a proposed dock is illegal — the directed signal a filler reports instead of silently dropping
/// the connection. <see cref="SealsWool"/>: the edge touches the never-dock room. <see cref="NotAnEntryEdge"/>:
/// no entry slot reaches the edge (nothing to dock to).</summary>
public enum DockRejection { SealsWool, NotAnEntryEdge }

/// <summary>
/// The docking gate (G80): the one declarative place that decides whether a box edge may receive a dock. It is
/// a <b>compose-side gate</b> — not an <c>ILayoutTerm</c>. The evaluator reads the derived board (never a shape
/// or family name) and the interfaces drop at <c>Assemble</c>, so a docking term is doubly impossible; the
/// filler/partitioner consults this gate as it docks a box and <b>emits only legal docks</b>, the existing hard
/// terms catching any symptom on derived topology as the mirror.
///
/// <para>Every family docks through a <b>single mouth</b> (the clamp too — its two legs meet the host on one
/// edge, the wool clamped inside as a cut cell), so a dock is legal iff the edge <b>lands on a docking-edge
/// slot</b> (an entry) and <b>touches no never-dock slot</b> (the wool room). The gate resolves each edge to
/// its slots via the <see cref="BoxEdgeInterface"/> facts (read off the shape), so validity is
/// <b>shape-relative for free</b> — an entry shift moves the entry's edge and the verdict follows, with no
/// per-family imperative code.</para>
/// </summary>
public static class DockingGate
{
    /// <summary>The dock role a slot or designation mark plays for its <paramref name="designation"/> — the
    /// docking law as data, scoped per designation (<see cref="Designation"/>; map-generation.md §5.3). The
    /// <see cref="Designation.Approach"/> table is the approach law verbatim: only the bare <c>room</c> (the
    /// wool/spawn terminal) never-docks and only the bare <c>entry</c> is a docking edge; every corridor slot —
    /// runs, bars, legs, and the entry/room-qualified runs and bars — is internal. The
    /// <see cref="Designation.Hub"/> and <see cref="Designation.Frontline"/> carry <b>no terminal</b>, so
    /// nothing never-docks: the hub's per-edge <see cref="DesignationMarks.Interface"/> and the frontline's
    /// <see cref="DesignationMarks.Face"/> are the docking edges their neighbours land on, every structural slot
    /// internal. The marks are stamped by the hub/frontline designations (G88/G89); this table is the binding
    /// they consume.</summary>
    public static SlotDockRole Role(Designation designation, string slotOrMark) => designation switch
    {
        Designation.Approach => slotOrMark switch
        {
            ApproachSlots.Room => SlotDockRole.NeverDock,
            ApproachSlots.Entry => SlotDockRole.DockingEdge,
            _ => SlotDockRole.Internal,
        },
        Designation.Hub => slotOrMark == DesignationMarks.Interface ? SlotDockRole.DockingEdge : SlotDockRole.Internal,
        Designation.Frontline => slotOrMark == DesignationMarks.Face ? SlotDockRole.DockingEdge : SlotDockRole.Internal,
        _ => SlotDockRole.Internal,
    };

    /// <summary>Why docking on <paramref name="edge"/> is illegal, or <c>null</c> when it is a legal dock. The
    /// room veto is strongest (a sealed wool is unrecoverable here), then the edge must actually land on an
    /// entry. Every family docks through a single mouth, so the verdict reads only the edge's slots — it needs
    /// no family name.</summary>
    public static DockRejection? Check(BoxEdgeInterface edge)
    {
        if (edge.Slots.Any(s => Role(Designation.Approach, s) == SlotDockRole.NeverDock)) return DockRejection.SealsWool;
        if (!edge.Slots.Any(s => Role(Designation.Approach, s) == SlotDockRole.DockingEdge)) return DockRejection.NotAnEntryEdge;
        return null;
    }

    /// <summary>The verdict for docking through a single <paramref name="mouth"/> edge, or <c>null</c> when it is
    /// a legal dock — the mouth's own legality (<see cref="Check"/>). The <paramref name="edges"/> are the box's
    /// <see cref="BoxEdgeInterface"/> facts read off the placed shape, so the verdict is shape-relative.</summary>
    public static DockRejection? CheckMouth(IReadOnlyList<BoxEdgeInterface> edges, BoxEdge mouth)
    {
        var edge = edges.FirstOrDefault(e => e.Edge == mouth);
        return edge is null ? DockRejection.NotAnEntryEdge : Check(edge);
    }

    /// <summary>The edges a filled box exposes as legal docks — every edge <see cref="Check"/> clears.</summary>
    public static IReadOnlyList<BoxEdgeInterface> DockingEdges(IReadOnlyList<BoxEdgeInterface> edges) =>
        edges.Where(e => Check(e) is null).ToList();

    /// <summary>True when <paramref name="edge"/> may receive a dock on the filled box.</summary>
    public static bool CanDock(IReadOnlyList<BoxEdgeInterface> edges, BoxEdge edge) =>
        edges.Where(e => e.Edge == edge).Any(e => Check(e) is null);
}
