using PgmStudio.Geom;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Fanned-board geometry: a rect's orbit image under a symmetry mode's axes, and the collinear-chain
/// measurement (LN2's unit of account).
/// </summary>
public static class ComposeGeometry
{
    /// <summary>LN2's hard cap: a lane runs at most this many blocks before a junction or dead end, and the
    /// cap binds the maximal collinear chain of land-joined same-cross-section pieces — a long lane cut into
    /// several collinear pieces is still one lane.</summary>
    public const int LaneChainMaxBlocks = 50;

    /// <summary>The longest maximal collinear chain over a set of cell rects, in blocks: rects with the same
    /// cross-axis interval that abut along the run axis merge into one chain (a lane cut into collinear
    /// pieces is still one lane); a jog, a width change, or a corner touch breaks the chain. LN2 caps the
    /// result at <see cref="LaneChainMaxBlocks"/>.</summary>
    public static int MaxChainBlocks(int cell, IReadOnlyList<CellRect> rects)
    {
        var xRuns = LongestRun(rects, r => (r.Z, r.Height), r => r.X, r => r.Width);
        var zRuns = LongestRun(rects, r => (r.X, r.Width), r => r.Z, r => r.Height);
        return Math.Max(xRuns, zRuns) * cell;
    }

    private static int LongestRun(
        IReadOnlyList<CellRect> rects,
        Func<CellRect, (int, int)> cross, Func<CellRect, int> runMin, Func<CellRect, int> runSpan)
    {
        var best = 0;
        foreach (var group in rects.GroupBy(cross))
        {
            var runs = group.OrderBy(runMin).ToList();
            int start = runMin(runs[0]), end = start + runSpan(runs[0]);
            foreach (var r in runs.Skip(1))
            {
                if (runMin(r) == end) end = runMin(r) + runSpan(r);   // abutting — same chain
                else
                {
                    best = Math.Max(best, end - start);
                    start = runMin(r);
                    end = start + runSpan(r);
                }
            }
            best = Math.Max(best, end - start);
        }
        return best;
    }

    /// <summary>The k-th orbit image of a rect: identity at k=0, the mode's concrete orbit axes for
    /// k=1..order-1 — matching <see cref="Plan.ContactGraph.FanRect"/> (unlike the k-agnostic
    /// <see cref="Symmetry.Rect"/>).</summary>
    internal static (double X1, double Z1, double X2, double Z2) FanImage(
        double x1, double z1, double x2, double z2, string[] axes, int k)
    {
        if (k == 0) return (x1, z1, x2, z2);
        (double x, double z)[] corners = [(x1, z1), (x1, z2), (x2, z1), (x2, z2)];
        var axis = axes[k - 1];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axis, 0, 0)).ToList();
        return (pts.Min(p => p.X), pts.Min(p => p.Z), pts.Max(p => p.X), pts.Max(p => p.Z));
    }
}
