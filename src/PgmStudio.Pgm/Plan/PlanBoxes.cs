using PgmStudio.Geom;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// Membership for the authored <see cref="PlanBox"/> annotation: which pieces a box groups. One rule in one
/// place, so the editor's drag, the readback harnesses and any later report all group a plan identically.
///
/// <para>Two modes, in order: a box that <b>names</b> its members (<see cref="PlanBox.Members"/> — what a
/// composed partition writes) owns exactly those, and a box that names none takes every <b>generating</b>
/// piece wholly inside its rect. Annotations (buffer) are never members: a reserved gap drawn
/// inside a box documents the box's spacing, it is not part of what the box realizes.</para>
/// </summary>
public static class PlanBoxes
{
    /// <summary>True when the <paramref name="inner"/> cell rect lies wholly within <paramref name="outer"/>
    /// (touching edges count as inside — a box drawn tight around its pieces still contains them).</summary>
    public static bool Contains(CellRect outer, CellRect inner) =>
        inner.X >= outer.X && inner.Z >= outer.Z
        && inner.X + inner.Width <= outer.X + outer.Width
        && inner.Z + inner.Height <= outer.Z + outer.Height;

    /// <summary>The pieces <paramref name="box"/> groups, in the plan's piece order. A named member that no
    /// longer exists is skipped rather than failing the read — a box outliving a deleted piece is an
    /// annotation gone stale, not a broken plan.</summary>
    public static IReadOnlyList<PlanPiece> MembersOf(PlanModel plan, PlanBox box)
    {
        if (box.Members is { Count: > 0 } named)
        {
            var ids = named.ToHashSet();
            return plan.Pieces.Where(p => ids.Contains(p.Id)).ToList();
        }
        return plan.Pieces
            .Where(p => PlanRoles.IsGenerating(p.Role) && Contains(box.Rect, p.Rect))
            .ToList();
    }

    /// <summary>The box with <paramref name="id"/>, or <c>null</c>.</summary>
    public static PlanBox? ById(PlanModel plan, string id) => plan.Boxes.FirstOrDefault(b => b.Id == id);
}
