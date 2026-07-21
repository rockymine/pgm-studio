using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A composed unit piece: a rect in cell coordinates (<see cref="Plan.PlanPiece.Rect"/> convention),
/// its map-level role, and — when it came out of a box fill — its shape-internal <see cref="Slot"/>
/// (<see cref="ApproachSlots"/>) and its <see cref="Box"/> ownership. Slot + box are the full label
/// (<c>wool-a/entry</c>) the compose-side rules bind to. Labels are compose-internal: every compose move
/// preserves them, and they drop only at <see cref="Composer"/> assembly (the written plan is label-free by
/// design).</summary>
public sealed record GrownPiece(string Id, int[] Rect, string Role = PlanRoles.Piece, string? Slot = null, BoxRef? Box = null);

/// <summary>The unit's spawn: which piece it sits on, its piece-relative half-cell offset, and its
/// absolute facing (SP3: toward the enemy by default).</summary>
public sealed record GrownSpawn(string Piece, double[] At, string Facing);

/// <summary>A placed wool: which piece it sits on and its piece-relative half-cell offset.</summary>
public sealed record GrownWool(string Piece, double[] At);

/// <summary>The team-0 unit the allocate→fill pipeline produces: its pieces and objective placements, in plan
/// cell coordinates, ready for <see cref="Composer"/> to assemble into a <see cref="Plan.PlanModel"/>.</summary>
public sealed record GrownUnit(IReadOnlyList<GrownPiece> Pieces, GrownSpawn Spawn, IReadOnlyList<GrownWool> Wools);
