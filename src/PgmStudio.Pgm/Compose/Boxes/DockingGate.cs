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
/// no entry slot reaches the edge (nothing to dock to). <see cref="WrongSpan"/>: the family docks a specific
/// span (the clamp its short edge) and this edge is the other one. <see cref="UnmetDemand"/>: the family needs
/// more distinct docking edges than the proposed dock offers — the clamp's two hosts, which the partition graph
/// places (the dual-host corner-wrap), never a single-mouth fill.</summary>
public enum DockRejection { SealsWool, NotAnEntryEdge, WrongSpan, UnmetDemand }

/// <summary>What a family demands of the boxes that dock it: <see cref="EntryDemand"/> distinct box edges must
/// connect (the clamp needs both its bars joined, most shapes one mouth), and <see cref="Span"/>, when set,
/// constrains which edges count (the clamp docks along its short edge; the long edges are its bay and its
/// wool wall).</summary>
public sealed record FamilyDock(int EntryDemand, EdgeSpan? Span)
{
    /// <summary>The docking demand of <paramref name="family"/>. The clamp is the one family whose entries sit
    /// on two different box edges (its parallel bars) and must both connect, along the short edge; every other
    /// family exposes its mouth on a single edge with no span preference.</summary>
    public static FamilyDock Of(ShapeFamily family) => family switch
    {
        ShapeFamily.Clamp => new(EntryDemand: 2, Span: EdgeSpan.Short),
        _ => new(EntryDemand: 1, Span: null),
    };
}

/// <summary>
/// The docking gate (G80): the one declarative place that decides whether a box edge may receive a dock. It is
/// a <b>compose-side gate</b> — not an <c>ILayoutTerm</c>. The evaluator reads the derived board (never a shape
/// or family name) and the interfaces drop at <c>Assemble</c>, so a docking term is doubly impossible; the
/// filler/partitioner consults this gate as it docks a box and <b>emits only legal docks</b>, the existing hard
/// terms catching any symptom on derived topology as the mirror.
///
/// <para>Every docking rule reduces to one table over slots: a dock is legal iff the edge <b>lands on a
/// docking-edge slot</b> (an entry) and <b>touches no never-dock slot</b> (the wool room), and the family's
/// span demand is met. The gate resolves each edge to its slots via the <see cref="BoxEdgeInterface"/> facts
/// (which are read off the shape), so validity is <b>shape-relative for free</b> — an entry shift moves the
/// entry's edge and the verdict follows, with no per-family imperative code.</para>
/// </summary>
public static class DockingGate
{
    /// <summary>The dock role of a template slot (<see cref="ApproachSlots"/>). Only the bare <c>room</c> (the
    /// wool) never-docks and only the bare <c>entry</c> is a docking edge; every corridor slot — runs, bars,
    /// legs, and the entry/room-qualified runs and bars — is internal.</summary>
    public static SlotDockRole Role(string slot) => slot switch
    {
        ApproachSlots.Room => SlotDockRole.NeverDock,
        ApproachSlots.Entry => SlotDockRole.DockingEdge,
        _ => SlotDockRole.Internal,
    };

    /// <summary>Why docking on <paramref name="edge"/> is illegal for <paramref name="family"/>, or
    /// <c>null</c> when it is a legal dock. The room veto is strongest (a sealed wool is unrecoverable here),
    /// then the edge must actually land on an entry, then satisfy the family's span demand.</summary>
    public static DockRejection? Check(BoxEdgeInterface edge, ShapeFamily family)
    {
        if (edge.Slots.Any(s => Role(s) == SlotDockRole.NeverDock)) return DockRejection.SealsWool;
        if (!edge.Slots.Any(s => Role(s) == SlotDockRole.DockingEdge)) return DockRejection.NotAnEntryEdge;
        var span = FamilyDock.Of(family).Span;
        if (span is not null && edge.Span != span) return DockRejection.WrongSpan;
        return null;
    }

    /// <summary>The verdict for docking <paramref name="family"/> through a single <paramref name="mouth"/> edge,
    /// or <c>null</c> when it is a legal dock. The mouth's own legality (<see cref="Check"/>) plus the family's
    /// demand: a family that needs more than one distinct docking edge (the clamp's dual-host) cannot be
    /// satisfied by a single mouth, so it rejects <see cref="DockRejection.UnmetDemand"/> here — the partition
    /// graph places that corner-wrap, not the single-mouth filler. The <paramref name="edges"/> are the box's
    /// <see cref="BoxEdgeInterface"/> facts read off the placed shape, so the verdict is shape-relative.</summary>
    public static DockRejection? CheckMouth(
        IReadOnlyList<BoxEdgeInterface> edges, BoxEdge mouth, ShapeFamily family)
    {
        var edge = edges.FirstOrDefault(e => e.Edge == mouth);
        if (edge is null) return DockRejection.NotAnEntryEdge;
        if (Check(edge, family) is { } rejection) return rejection;
        return FamilyDock.Of(family).EntryDemand > 1 ? DockRejection.UnmetDemand : null;
    }

    /// <summary>The edges a box filled with <paramref name="family"/> exposes as legal docks — every edge
    /// <see cref="Check"/> clears.</summary>
    public static IReadOnlyList<BoxEdgeInterface> DockingEdges(
        IReadOnlyList<BoxEdgeInterface> edges, ShapeFamily family) =>
        edges.Where(e => Check(e, family) is null).ToList();

    /// <summary>True when <paramref name="edge"/> may receive a dock on a box filled with
    /// <paramref name="family"/>.</summary>
    public static bool CanDock(IReadOnlyList<BoxEdgeInterface> edges, ShapeFamily family, BoxEdge edge) =>
        edges.Where(e => e.Edge == edge).Any(e => Check(e, family) is null);

    /// <summary>True when the box exposes at least as many legal docking edges as the family demands — the
    /// clamp needs its two short bars reachable, every other family one mouth. A fill that cannot meet its own
    /// demand is a box the partitioner must change (resize, reface) rather than a valid placement.</summary>
    public static bool MeetsDemand(IReadOnlyList<BoxEdgeInterface> edges, ShapeFamily family) =>
        DockingEdges(edges, family).Count >= FamilyDock.Of(family).EntryDemand;
}
