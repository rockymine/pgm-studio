using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>Shared surface-traversal helpers for the distance terms: the walkable cell surface (terrain + build),
/// snapping a marker onto it, and drawing a rectilinear route as evidence. Distances between markers are the
/// 4-connected shortest path over this surface (<see cref="Cells.PathLength"/>) — how far a player actually
/// travels, not a straight line.</summary>
internal static class SurfaceNav
{
    /// <summary>The cells a player can stand on or build across: the team's own (k=0) terrain ∪ build zones,
    /// rasterized straight from the plan. Distances here are intra-team (spawn ↔ its wools), so the un-fanned
    /// surface is both the correct one and cheaper — no board derivation, keeping the gate free of it.</summary>
    public static HashSet<(int, int)> Walkable(EvalContext ctx)
    {
        var walkable = new HashSet<(int, int)>();
        foreach (var p in ctx.Plan.Pieces)
            if (!PlanRoles.IsAnnotation(p.Role))   // buffers are reserved empty space, never walkable
                AddRect(walkable, p.Rect);
        foreach (var z in ctx.Plan.Zones)
            AddRect(walkable, z.Rect);
        return walkable;
    }

    private static void AddRect(HashSet<(int, int)> set, int[] rect)
    {
        for (var x = rect[0]; x < rect[0] + rect[2]; x++)
            for (var z = rect[1]; z < rect[1] + rect[3]; z++)
                set.Add((x, z));
    }

    /// <summary>The walkable cell a marker sits on: its piece origin + offset, floored, snapped to the nearest
    /// walkable cell (a marker sits on its own filled piece; this fixes the odd off-by-one). Null if the piece
    /// is unknown or no walkable cell is within reach.</summary>
    public static (int, int)? MarkerCell(EvalContext ctx, string pieceId, double[] at, IReadOnlySet<(int, int)> walkable)
    {
        var piece = ctx.Plan.Pieces.FirstOrDefault(p => p.Id == pieceId);
        if (piece is null) return null;
        return Snap(((int)Math.Floor(piece.Rect[0] + at[0]), (int)Math.Floor(piece.Rect[1] + at[1])), walkable);
    }

    private static (int, int)? Snap((int, int) cell, IReadOnlySet<(int, int)> walkable)
    {
        if (walkable.Contains(cell)) return cell;
        for (var r = 1; r <= 2; r++)
            for (var dx = -r; dx <= r; dx++)
                for (var dz = -r; dz <= r; dz++)
                    if (Math.Abs(dx) + Math.Abs(dz) == r && walkable.Contains((cell.Item1 + dx, cell.Item2 + dz)))
                        return (cell.Item1 + dx, cell.Item2 + dz);
        return null;
    }

    /// <summary>Evidence for a distance violation between two markers: the two endpoints, a labelled measure, and
    /// the rectilinear route itself (collinear runs merged into segments — the path the number came from).</summary>
    public static IReadOnlyList<Evidence> RouteEvidence(
        IReadOnlySet<(int, int)> walkable, (int, int) a, (int, int) b, string label)
    {
        if (Cells.ShortestPath(a, b, walkable) is not { } path) return [];
        var evidence = new List<Evidence>
        {
            Ev.Marker(EvidenceTags.Offender, a.Item1 + 0.5, a.Item2 + 0.5),
            Ev.Marker(EvidenceTags.Offender, b.Item1 + 0.5, b.Item2 + 0.5),
            Ev.Measure(a.Item1 + 0.5, a.Item2 + 0.5, b.Item1 + 0.5, b.Item2 + 0.5, label),
        };
        foreach (var (s, e) in Runs(path))
            evidence.Add(Ev.Segment(EvidenceTags.Measure, s.Item1 + 0.5, s.Item2 + 0.5, e.Item1 + 0.5, e.Item2 + 0.5));
        return evidence;
    }

    // Collapse a cell path into its straight runs: one (start, end) segment per change of direction.
    private static IEnumerable<((int, int) Start, (int, int) End)> Runs(List<(int, int)> path)
    {
        var start = 0;
        for (var i = 1; i < path.Count; i++)
        {
            var prevDir = (path[i].Item1 - path[i - 1].Item1, path[i].Item2 - path[i - 1].Item2);
            var runDir = (path[start + 1].Item1 - path[start].Item1, path[start + 1].Item2 - path[start].Item2);
            if (prevDir != runDir) { yield return (path[start], path[i - 1]); start = i - 1; }
        }
        yield return (path[start], path[^1]);
    }
}
