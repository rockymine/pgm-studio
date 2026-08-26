namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// Whether a building stands <b>across</b> a road or merely at the end of one (DR-CROSS).
///
/// <para>A road is meant to run to a porch, so a building taking the ground a road covers is ordinary and the
/// road simply ends at its wall. A building the road carries on <em>past</em> is the other thing entirely:
/// what was one way through the board becomes two dead ends facing a building, and every gate on the board
/// answers 200 because the ground beside it is wide and the objectives still connect by some other way.</para>
///
/// <para>The two are told apart by what is left of the paving once the footprint is out of it. One run of
/// road is an end; two or more is a crossing. The count is taken <b>before and after</b> rather than against
/// one, because a stroke's own coverage leaves cells out — a worn road is holes by design, and a stepping-stone
/// crossing is nothing but holes — so what fires is the building <em>adding</em> a break, not the road having
/// one. For the same reason two paved cells within <see cref="DressingRules.RouteRunGap"/> blocks of each
/// other count as one run.</para>
/// </summary>
public static class RouteCrossing
{
    /// <summary>Whether <paramref name="footprint"/> breaks <paramref name="paving"/> into more runs than it
    /// already had. False where the two do not touch at all.</summary>
    public static bool Crosses(IReadOnlyCollection<(int X, int Z)> paving,
                               IReadOnlySet<(int X, int Z)> footprint)
    {
        var road = paving.ToHashSet();
        if (!road.Overlaps(footprint)) return false;

        var rest = road.Where(cell => !footprint.Contains(cell)).ToHashSet();
        if (rest.Count == 0) return false;              // the building took the whole road: an end, not a crossing
        return Runs(rest) > Runs(road);
    }

    /// <summary>How many separate runs of paving a set of cells falls into, joining any two within
    /// <see cref="DressingRules.RouteRunGap"/> blocks.</summary>
    private static int Runs(HashSet<(int X, int Z)> cells)
    {
        var gap = Math.Max(1, DressingRules.RouteRunGap);
        var seen = new HashSet<(int X, int Z)>();
        var runs = 0;
        var frontier = new Queue<(int X, int Z)>();
        foreach (var start in cells)
        {
            if (!seen.Add(start)) continue;
            runs++;
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var (x, z) = frontier.Dequeue();
                for (var dz = -gap; dz <= gap; dz++)
                for (var dx = -gap; dx <= gap; dx++)
                {
                    var side = (x + dx, z + dz);
                    if (cells.Contains(side) && seen.Add(side)) frontier.Enqueue(side);
                }
            }
        }
        return runs;
    }
}
