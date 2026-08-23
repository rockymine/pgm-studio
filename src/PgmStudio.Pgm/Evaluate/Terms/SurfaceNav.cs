using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>Shared surface-traversal helpers for the distance terms: the walkable cell surface (terrain + build),
/// snapping a marker onto it, and drawing a route as evidence. Distances between markers are
/// <see cref="Walk"/>'s — how far a player actually travels, in blocks, not a straight line and not a count of
/// cells. A plan states no heights until its relief is solved, so this ground charges no climb: what it
/// measures is the shape of the board, which is what a layout term is scoring.</summary>
internal static class SurfaceNav
{
    /// <summary>The cells a player can stand on or build across: the team's own (k=0) terrain ∪ build zones,
    /// rasterized straight from the plan. Distances here are intra-team (spawn ↔ its wools), so the un-fanned
    /// surface is both the correct one and cheaper — no board derivation, keeping the gate free of it.</summary>
    /// <summary>The walkable surface as ground a walk runs over, answering in blocks at the plan's own cell
    /// size.</summary>
    public static WalkGround Ground(EvalContext ctx)
        => WalkGround.Over(Walkable(ctx), ctx.Plan.Globals.Cell);

    public static HashSet<(int, int)> Walkable(EvalContext ctx)
    {
        var walkable = new HashSet<(int, int)>();
        foreach (var p in ctx.Plan.Pieces)
            if (!PlanRoles.IsAnnotation(p.Role))   // buffers are reserved empty space, never walkable
                AddRect(walkable, p.Rect);
        // A water lane is closed at the tick the gate measures, so it is not walkable ground yet.
        foreach (var z in ctx.Plan.BuildZones)
            AddRect(walkable, z.Rect);
        return walkable;
    }

    private static void AddRect(HashSet<(int, int)> set, CellRect rect)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
            for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                set.Add((x, z));
    }

    /// <summary>The walkable cell a marker sits on: its piece origin + offset, floored, snapped to the nearest
    /// walkable cell (a marker sits on its own filled piece; this fixes the odd off-by-one). A marker naming
    /// no piece reads its offset as an absolute cell position from the symmetry centre — the one shape a
    /// destroyable or core may take. Null if a named piece is unknown or no walkable cell is within reach.</summary>
    public static (int, int)? MarkerCell(EvalContext ctx, string pieceId, double[] at, IReadOnlySet<(int, int)> walkable)
        => MarkerCell(ctx.Plan, pieceId, at, walkable);

    /// <summary>As above, straight off the plan — for a reader that has no <see cref="EvalContext"/>.</summary>
    public static (int, int)? MarkerCell(PlanModel plan, string pieceId, double[] at, IReadOnlySet<(int, int)> walkable)
    {
        if (string.IsNullOrEmpty(pieceId))
            return Snap(((int)Math.Floor(at[0]), (int)Math.Floor(at[1])), walkable);
        var piece = plan.Pieces.FirstOrDefault(p => p.Id == pieceId);
        if (piece is null) return null;
        return Snap(((int)Math.Floor(piece.Rect.X + at[0]), (int)Math.Floor(piece.Rect.Z + at[1])), walkable);
    }

    // Snaps onto the canonical square (Chebyshev) ring rather than the diamond (Manhattan) ring this used to
    // walk itself — the square ring is a superset at every radius, so a marker that only a diagonal-corner
    // cell would reach (one the Manhattan ring refused) can now snap where it previously read unreachable.
    private static (int, int)? Snap((int, int) cell, IReadOnlySet<(int, int)> walkable) =>
        Cells.SnapToWalkable(cell, walkable, radius: 2);

    /// <summary>Evidence for a distance violation between two markers: the two endpoints, a labelled measure, and
    /// the walked route itself (collinear runs merged into segments — the path the number came from).</summary>
    public static IReadOnlyList<Evidence> RouteEvidence(
        WalkGround ground, (int, int) a, (int, int) b, string label)
    {
        if (Walk.Between(a, b, ground) is not { } walked) return [];
        var path = walked.Cells.Select(cell => (cell.X, cell.Z)).ToList();
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
