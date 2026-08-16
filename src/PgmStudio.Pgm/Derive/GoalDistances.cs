using PgmStudio.Geom;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Derive;

/// <summary>
/// The walk from each destroy goal to the two spawns that contest it, in blocks — the rectilinear traversal
/// over the fanned closure (pieces ∪ build zones) that every distance rule is stated in, never the straight
/// line. A destroy goal is defended by the team it belongs to and attacked from the other spawn, so the pair
/// of walks and their ratio are what the board's shape actually charges each side.
///
/// <para>This is a <b>measurement surface</b>: it reports and never judges. The judging lives in one place —
/// <see cref="Evaluate.Terms.GoalSpawnRatio"/> holds the ratio to the author's stated band (GO1) — and it
/// reads its numbers from here, so the score and the inspect response can never disagree about what a
/// board's walks are.</para>
/// </summary>
public static class GoalDistances
{
    /// <summary>One goal's walks, in blocks: from its own team's nearest spawn, from the enemy's, and the
    /// enemy÷own ratio. A null walk means no route exists over the closure — the traversability gate's
    /// territory, reported here as the absence it is rather than invented.</summary>
    public sealed record GoalWalk(
        string Id, string Kind, double? OwnSpawnBlocks, double? EnemySpawnBlocks, double? Ratio);

    /// <summary>Every destroyable and core's walks, or empty where the symmetry is not order 2 (the only
    /// order those goals compile at) or the plan has no destroy goal or no spawn.</summary>
    public static List<GoalWalk> Read(PlanModel plan)
    {
        var mode = plan.Globals.Symmetry;
        if (Symmetry.Order(mode) != 2) return [];

        var goals = new List<(string Id, string Kind, string Piece, double[] At)>();
        foreach (var destroyable in plan.Placements.Destroyables)
            goals.Add((destroyable.Id, "destroyable", destroyable.Piece, destroyable.At));
        foreach (var core in plan.Placements.Cores)
            goals.Add((core.Id, "core", core.Piece, core.At));
        if (goals.Count == 0 || plan.Placements.Spawns.Count == 0) return [];

        // The fanned closure: both orbit images of every generating piece and every build zone. Distances
        // here cross the team boundary (the enemy's spawn to this team's goal), so the un-fanned surface the
        // intra-team terms walk would cut the route at the axis.
        var walkable = new HashSet<(int, int)>();
        foreach (var piece in plan.Pieces)
            if (!PlanRoles.IsAnnotation(piece.Role))
                AddFanned(walkable, piece.Rect, mode);
        foreach (var zone in plan.BuildZones)
            AddFanned(walkable, zone.Rect, mode);

        // Spawn cells per orbit image: image 0 is the authored team, image 1 its opponent.
        var spawnCells = new List<(int, int)>[] { [], [] };
        foreach (var spawn in plan.Placements.Spawns)
        {
            if (SurfaceNav.MarkerCell(plan, spawn.Piece, spawn.At, walkable) is not { } authored) continue;
            spawnCells[0].Add(authored);
            if (Snap(ImageCell(authored, mode), walkable) is { } opposed) spawnCells[1].Add(opposed);
        }
        if (spawnCells[0].Count == 0) return [];

        var cell = (double)plan.Globals.Cell;
        var walks = new List<GoalWalk>(goals.Count);
        foreach (var (id, kind, pieceId, at) in goals)
        {
            // The authored image of the goal belongs to the authored team (goals fan team-outer), so its own
            // spawns are image 0 and the enemy's image 1. Both images give the same pair by symmetry, so one
            // is measured and reported once per authored goal.
            if (SurfaceNav.MarkerCell(plan, pieceId, at, walkable) is not { } goalCell)
            {
                walks.Add(new GoalWalk(id, kind, null, null, null));
                continue;
            }
            var own = Nearest(goalCell, spawnCells[0], walkable) * cell;
            var enemy = spawnCells[1].Count > 0 ? Nearest(goalCell, spawnCells[1], walkable) * cell : null;
            var ratio = own is > 0 && enemy is { } far ? far / own : null;
            walks.Add(new GoalWalk(id, kind, own, enemy, ratio));
        }
        return walks;
    }

    private static double? Nearest((int, int) from, List<(int, int)> targets, IReadOnlySet<(int, int)> within)
    {
        if (targets.Count == 0) return null;
        return Cells.PathLengthToAny(from, targets.ToHashSet(), within) is { } steps ? steps : null;
    }

    // A cell under the second orbit image: carry its centre through the point fan and floor back to a cell,
    // which is exact for every order-2 mode (a rect corner fan would need the same rounding either way).
    private static (int, int) ImageCell((int X, int Z) cell, string? mode)
    {
        var (imageX, imageZ) = Symmetry.Point(cell.X + 0.5, cell.Z + 0.5, mode, 0, 0, 1);
        return ((int)Math.Floor(imageX), (int)Math.Floor(imageZ));
    }

    private static (int, int)? Snap((int, int) cell, IReadOnlySet<(int, int)> within) =>
        Cells.SnapToWalkable(cell, within, radius: 2);

    private static void AddFanned(HashSet<(int, int)> set, CellRect rect, string? mode)
    {
        for (var imageIndex = 0; imageIndex < 2; imageIndex++)
            for (var x = rect.X; x < rect.X + rect.Width; x++)
                for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                {
                    if (imageIndex == 0) { set.Add((x, z)); continue; }
                    set.Add(ImageCell((x, z), mode));
                }
    }
}
