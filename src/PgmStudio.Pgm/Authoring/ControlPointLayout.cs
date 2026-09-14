namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Where a board's capture points go, given how many of them it is played for and where a team enters it.
///
/// <para><b>A capture point belongs to nobody, so it has to be the same walk for everyone</b> (the author's
/// rule, <c>docs/gameplay/approaches.md</c>). That is the whole difference from a destroy goal: a destroyable
/// belongs to the team behind it and sits forward of its own spawn, while the only positions that are the
/// same walk for every team lie on the board's own axes of symmetry. So an anchor is never stated against a
/// piece — it is stated against the centre and the spawn frame, and a count is the whole of what an author
/// has to say.</para>
///
/// <para>Two positions come out of that. The <b>centre</b> of symmetry is its own orbit image and stays one
/// point. A <b>side</b> point sits on the bisector between two neighbouring spawns — 90° off the spawn
/// direction on two teams, 45° on four, which is <c>180°/order</c> either way — so it is the same walk for the
/// two teams it lies between rather than the doorstep of one, and the orbit turns that single primary into
/// the matched pair or the ring of four. Its distance is stated as a share of the centre-to-spawn distance
/// rather than in blocks, because a board twice the size wants its points twice as far out.</para>
///
/// <para>The counts this answers are the ones the author stated and the corpus builds: one, a ring, or a ring
/// and a centre — <b>1, 2 or 3</b> points on two teams and <b>1, 4 or 5</b> on four. A board asking for any
/// other number is asking for an arrangement nobody has ruled on, and <see cref="Fans"/> says so rather than
/// inventing one.</para>
/// </summary>
public static class ControlPointLayout
{
    /// <summary>How far out a side point sits, as a share of the centre-to-spawn distance, on a two-team
    /// board — the corpus median (quartiles 0.52 and 0.88), and the author's rule.</summary>
    public const double SideShare = 0.66;

    /// <summary>The same share on a board of four teams or more, where the ring sits further out (quartiles
    /// 0.52 and 0.99).</summary>
    public const double WideSideShare = 0.90;

    /// <summary>Whether an orbit of <paramref name="order"/> builds exactly <paramref name="count"/> points
    /// from the primaries below: a centre alone, a ring, or a ring and a centre.</summary>
    public static bool Fans(int count, int order) =>
        order >= 2 && count >= 1 && (count == 1 || count == order || count == order + 1);

    /// <summary>The anchors to author — the primaries an orbit of <paramref name="order"/> fans into the whole
    /// set, so the caller turns them the same way it turns every other marker. Empty where the count is one
    /// <see cref="Fans"/> does not answer for.
    ///
    /// <para><paramref name="spawnX"/>/<paramref name="spawnZ"/> are any one team's entry, in the same frame
    /// the anchors come back in; the orbit's own centre is the origin, which is what a plan is authored
    /// about.</para></summary>
    public static IReadOnlyList<(double X, double Z)> Primaries(int count, int order, double spawnX, double spawnZ)
    {
        if (!Fans(count, order)) return [];

        var anchors = new List<(double X, double Z)>();
        if (count % order == 1 || count == 1) anchors.Add((0, 0));
        if (count == 1) return anchors;

        var reach = Math.Sqrt(spawnX * spawnX + spawnZ * spawnZ);
        if (reach <= 0) return anchors;

        var bearing = Math.Atan2(spawnZ, spawnX) + Math.PI / order;
        var reachOut = reach * (order >= 4 ? WideSideShare : SideShare);
        anchors.Add((Math.Round(reachOut * Math.Cos(bearing)), Math.Round(reachOut * Math.Sin(bearing))));
        return anchors;
    }
}
