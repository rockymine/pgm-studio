using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The typed box kinds of the partition scaffold (docs/generator/model.md §4).</summary>
public enum BoxKind { Spawn, Hub, Wool, Frontline, Mid }

/// <summary>The fill directive for a wool box the allocator chose: the approach <see cref="Family"/>, its room
/// <see cref="Placement"/> (back vs side-tuck), the <see cref="Flip"/> handedness, <see cref="WoolAtEnd"/>
/// (the clamp's adjacent/corner L+I variant vs its centered I+I; the U/H wool-at-an-end knob), and
/// <see cref="AttachmentWidth"/> (the donut's sampled hub-entry width in cells; 0 = one corridor), and
/// <see cref="RingWalls"/> (the donut's four wall widths when one is widened; <c>null</c> = every wall at the
/// corridor width) — carried so the filler re-emits the exact shape the allocator seated (an overhang dock
/// aligns to a specific family's entry at its exact width).</summary>
public sealed record WoolFill(
    ShapeFamily Family, RoomPlacement Placement, bool Flip, bool WoolAtEnd = false, int AttachmentWidth = 0,
    RingWalls? RingWalls = null);

/// <summary>A piece's box ownership — which box's fill it belongs to. Together with the piece's slot this is
/// the full label (<c>wool-a/entry</c>) every compose-side rule binds to; the piece-id prefix is its
/// serialization, never the source of truth.</summary>
public sealed record BoxRef(string Id, BoxKind Kind)
{
    public override string ToString() => Id;
}

/// <summary>A typed box of the partition: a bounding envelope (its contents must touch its edges and stay
/// connected but need not fill it solid — never a fill target) plus the per-box half of the two-currency
/// budget. <see cref="Rect"/> is in plan cell coordinates. <see cref="Form"/> is the compound the allocator
/// chose for a body box (a hub) to fill it — the fill directive the filler re-emits, its real free-edge
/// intervals (§1.13) the offerable surface neighbours seat against; <c>null</c> for a box whose kind carries
/// no form (leaves the hub filler its default solid rectangle). <see cref="FlipV"/> is the hub form's
/// orientation — reflect it vertically so its open feet face the front — the second half of that directive,
/// carried so the filler re-emits the body the allocator seated against. <see cref="HubWalls"/> is the third
/// half of that directive for a ring-bodied form — the four wall widths when one is widened, in the
/// <b>pre-flip</b> frame; <c>null</c> = every wall at the corridor width. <see cref="HubArms"/> is the branch
/// forms' counterpart — each leg's <c>(Start, Width)</c> on the spine, <c>null</c> = every leg one corridor wide
/// at the spine's ends. Both are carried for the same reason as the form itself: the allocator emits the hub to
/// read the runs it offers and the filler re-emits it, so a body sampled twice would diverge. <see cref="Wool"/> is the wool box's
/// fill directive (family + room placement + handedness), likewise carried so the filler re-emits what the
/// allocator seated; <c>null</c> for a non-wool box.</summary>
public sealed record Box(
    string Id, BoxKind Kind, CellRect Rect, int LandTargetCells,
    CompoundRead? Form = null, bool FlipV = false, WoolFill? Wool = null, RingWalls? HubWalls = null,
    IReadOnlyList<(int Start, int Width)>? HubArms = null)
{
    public BoxRef Ref => new(Id, Kind);
}
