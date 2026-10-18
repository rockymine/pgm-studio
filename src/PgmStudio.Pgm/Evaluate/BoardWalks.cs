using PgmStudio.Geom;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>One place a walk starts or ends: the marker's id, the owner a finding highlights for it, and the cell
/// it stands on, or null where the walkable ground has no cell within reach of it.</summary>
public sealed record BoardMarker(string Id, string Owner, (int X, int Z)? Cell);

/// <summary>
/// What the distance terms walk: one team's own ground and the build cells beside it, in cells answering in
/// blocks, and the authored team's spawns and wools seated on it. The walks between them are a team's own, so
/// its image of the board is the ground they take. A plan states one (<see cref="Of(PlanModel)"/>); a ground read
/// off another source states its own.
/// </summary>
public sealed record BoardWalks(WalkGround Ground, IReadOnlyList<BoardMarker> Spawns, IReadOnlyList<BoardMarker> Wools)
{
    /// <summary>The plan's authored pieces and build zones, its spawns and wools seated on them.</summary>
    public static BoardWalks Of(PlanModel plan)
    {
        var walkable = SurfaceNav.Walkable(plan);
        var ground = WalkGround.Over(walkable, plan.Globals.Cell);
        return new BoardWalks(ground,
            [.. plan.Placements.Spawns.Select(s => new BoardMarker(s.Id, s.Piece, SurfaceNav.MarkerCell(plan, s.Piece, s.At, ground.Footprint)))],
            [.. plan.Placements.Wools.Select(w => new BoardMarker(w.Id, w.Piece, SurfaceNav.MarkerCell(plan, w.Piece, w.At, ground.Footprint)))]);
    }
}
