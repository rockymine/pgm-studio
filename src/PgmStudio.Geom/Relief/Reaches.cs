namespace PgmStudio.Geom.Relief;

/// <summary>
/// The levels water holds along a course flowing from its first station to its last: never climbing, held level
/// in reaches and stepping down in falls where the ground drops <see cref="ModifierDefaults.RiverFall"/> blocks or
/// more after a reach of <see cref="ModifierDefaults.RiverReach"/>, and sooner wherever holding would stand the
/// water above the lowest ground beside it. A Terraform River cuts its bed to them and a dressing channel fills to
/// them.
/// </summary>
public static class Reaches
{
    /// <summary>The lowest ground beside each station: the least of <paramref name="lowest"/> at the station and
    /// its two neighbours, or <paramref name="fallback"/> there where none of the three has ground (infinite with
    /// no fallback).</summary>
    public static double[] Low(IReadOnlyList<double> lowest, IReadOnlyList<double>? fallback)
    {
        var low = new double[lowest.Count];
        for (var j = 0; j < low.Length; j++)
        {
            var near = Math.Min(lowest[j], Math.Min(lowest[Math.Max(j - 1, 0)], lowest[Math.Min(j + 1, low.Length - 1)]));
            low[j] = double.IsFinite(near) || fallback is null ? near : fallback[j];
        }
        return low;
    }

    /// <summary>The floor at each station of a course: <paramref name="below"/> blocks under the lowest ground
    /// beside it (<paramref name="low"/>, where a station with none constrains nothing), floored to a block, with
    /// <paramref name="water"/> blocks of water over it never standing above that ground. <paramref name="arc"/>
    /// is each station's distance along the course. A course that <paramref name="bothWays"/> holds one floor the
    /// whole way, under the lowest ground anywhere along it. NaN throughout where no station has ground.</summary>
    public static double[] Floors(IReadOnlyList<double> arc, IReadOnlyList<double> low, double below, double water,
                                  bool bothWays)
    {
        var floors = new double[low.Count];
        var grounded = low.Where(double.IsFinite).ToList();
        if (grounded.Count == 0)
        {
            Array.Fill(floors, double.NaN);
            return floors;
        }
        if (bothWays)
        {
            Array.Fill(floors, Math.Floor(grounded.Min() - below));
            return floors;
        }

        var level = grounded[0] - below;
        double running = level, since = 0;
        for (var j = 0; j < low.Count; j++)
        {
            running = Math.Min(running, low[j] - below);
            var falls = running <= level - ModifierDefaults.RiverFall && since >= ModifierDefaults.RiverReach;
            if (j > 0 && (falls || Math.Floor(level) + water > low[j]))
            {
                level = running;
                since = 0;
            }
            floors[j] = Math.Floor(level);
            if (j > 0) since += arc[j] - arc[j - 1];
        }
        return floors;
    }
}
