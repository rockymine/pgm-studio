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
/// budget. <see cref="Rect"/> is in plan cell coordinates.</summary>
public sealed record Box(string Id, BoxKind Kind, int[] Rect, int LandTargetCells)
{
    public BoxRef Ref => new(Id, Kind);
}
