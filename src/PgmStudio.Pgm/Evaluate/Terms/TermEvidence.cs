using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>Shared helpers for turning a term's subject ids and rects into drawable <see cref="Evidence"/> —
/// so attaching evidence stays one line per term while the geometry is in hand.</summary>
internal static class TermEvidence
{
    /// <summary>The cell rect of a piece or zone by id, or null if neither exists.</summary>
    public static int[]? Locate(PlanModel plan, string id) =>
        plan.Pieces.FirstOrDefault(p => p.Id == id)?.Rect
        ?? plan.Zones.FirstOrDefault(z => z.Id == id)?.Rect;

    /// <summary>One <c>offender</c> rect per subject that resolves to a piece/zone (unknown ids are skipped).</summary>
    public static List<Evidence> OffenderRects(PlanModel plan, IEnumerable<string> ids)
    {
        var evidence = new List<Evidence>();
        foreach (var id in ids)
            if (Locate(plan, id) is { } rect)
                evidence.Add(Ev.Rect(EvidenceTags.Offender, rect));
        return evidence;
    }

    /// <summary>The cell-space centre of a <c>[x, z, w, h]</c> rect.</summary>
    public static (double X, double Z) Center(int[] rect) => (rect[0] + rect[2] / 2.0, rect[1] + rect[3] / 2.0);
}
