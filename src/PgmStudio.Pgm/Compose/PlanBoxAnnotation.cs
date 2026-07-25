using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Writes a composed unit's partition into a plan as the authored <see cref="PlanBox"/> annotation — the one
/// place the compose-internal grouping crosses into the plan document.
///
/// <para>It is applied when a generated plan is <b>kept</b>, never while it is being browsed: the feed stays
/// label-free, and a board the author picked opens in the editor with the boxes that produced it already
/// drawn. Members are written <b>explicitly</b> (<see cref="PlanBox.Members"/>) rather than left to
/// containment, because the composer knows the grouping exactly — a wool box whose envelope happens to
/// enclose a neighbour's run must still own only its own pieces.</para>
/// </summary>
public static class PlanBoxAnnotation
{
    /// <summary>The authoring kind string for a partition <paramref name="kind"/>.</summary>
    public static string KindOf(BoxKind kind) => kind switch
    {
        BoxKind.Spawn => PlanBoxKinds.Spawn,
        BoxKind.Hub => PlanBoxKinds.Hub,
        BoxKind.Wool => PlanBoxKinds.Wool,
        BoxKind.Frontline => PlanBoxKinds.Frontline,
        _ => PlanBoxKinds.Mid,
    };

    /// <summary>The boxes a grown <paramref name="unit"/> implies: one per partition group
    /// (<see cref="BoxPartition.Of"/> for the envelopes, <see cref="BoxPartition.KeyOf"/> for the members), in
    /// the order the unit's pieces first reach them.</summary>
    public static List<PlanBox> For(GrownUnit unit)
    {
        var members = new Dictionary<string, List<string>>();
        foreach (var piece in unit.Pieces)
        {
            var (id, _) = BoxPartition.KeyOf(piece);
            if (!members.TryGetValue(id, out var list)) members[id] = list = [];
            list.Add(piece.Id);
        }

        return BoxPartition.Of(unit).Boxes
            .Select(b => new PlanBox
            {
                Id = b.Id,
                Kind = KindOf(b.Kind),
                Rect = [.. b.Rect],
                Members = members.TryGetValue(b.Id, out var ids) ? ids : null,
            })
            .ToList();
    }

    /// <summary>Replace <paramref name="plan"/>'s boxes with the ones <paramref name="unit"/> implies.</summary>
    public static void Apply(PlanModel plan, GrownUnit unit) => plan.Boxes = For(unit);
}
