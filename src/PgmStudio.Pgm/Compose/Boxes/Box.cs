using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The typed box kinds of the partition scaffold (docs/contracts/map-generation.md §4).</summary>
public enum BoxKind { Spawn, Hub, Wool, Frontline, Mid }

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
/// no form (leaves the hub filler its default solid rectangle).</summary>
public sealed record Box(string Id, BoxKind Kind, int[] Rect, int LandTargetCells, CompoundRead? Form = null)
{
    public BoxRef Ref => new(Id, Kind);
}
