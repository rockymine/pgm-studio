using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The CTW strait, re-read off the ground the board was actually drawn as (<c>CT12</c>).
///
/// <para>The plan's own reading is taken over rectangles, before a shape exists: the direct crossing between
/// the two team islands, which a plan states as a build region carrying that pair and no third landmass. A
/// finish is free to move it — a shape drawn across the gap bridges it, one drawn back from the edge widens
/// it, a fill closes it altogether — and the plan's verdict then stands over ground that no longer matches
/// it. So the same pairs are measured again on the raster, and the same band decides.</para>
///
/// <para><b>The pairs come from the plan and never from the raster.</b> Which crossing is the strait is a
/// fact about the board's roles and its build regions, neither of which a rasterized footprint carries: a
/// board of six landmasses has fifteen gaps and one of them is the strait. Taking the plan's own answer is
/// what keeps this to the crossing <c>CT12</c> judged rather than to every pair of masses on the board.</para>
///
/// <para>Only a pair the plan passed is re-read. A strait already out of band is the plan's finding and is
/// raised there; saying it twice from two measurements would read as two faults.</para>
/// </summary>
public static class StraitReadback
{
    /// <summary>The band a CTW strait is held to, in blocks — the author's rule, restated at the second
    /// measurement site rather than re-derived.</summary>
    public const int Narrowest = 15, Widest = 40;

    /// <summary>Re-read every strait the plan passed against the board the layout builds. Empty for a board
    /// the rule does not govern — no wool, a symmetry that is not a mirror pair — and for a plan the deriver
    /// cannot read, which the plan's own structural findings already name.</summary>
    public static Findings Check(PlanModel? plan, string? layoutJson)
    {
        if (plan is null || string.IsNullOrWhiteSpace(layoutJson)) return Findings.None;
        if (plan.Placements.Wools.Count == 0 || Symmetry.Order(plan.Globals.Symmetry) != 2) return Findings.None;

        BoardStructure board;
        try { board = BoardDeriver.Derive(plan); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or NullReferenceException or IndexOutOfRangeException or KeyNotFoundException)
        { return Findings.None; }

        var straits = PieceInterfaces.IslandGaps(board)
            .Where(gap => gap.Direct && gap.RoleA == "team" && gap.RoleB == "team")
            .Where(gap => gap.Blocks >= Narrowest && gap.Blocks <= Widest)
            .ToList();
        if (straits.Count == 0) return Findings.None;

        var footprint = SketchRasterizer.Rasterize(layoutJson).ToHashSet();
        if (footprint.Count == 0) return Findings.None;
        var landmassOf = Landmasses(footprint);

        var findings = new List<Finding>();
        foreach (var strait in straits)
        {
            var sideA = Landmass(board, strait.IslandA, landmassOf);
            var sideB = Landmass(board, strait.IslandB, landmassOf);
            if (sideA is null || sideB is null) continue;   // a side the layout does not draw at all

            var built = sideA == sideB ? 0 : Gap(footprint, landmassOf, sideA.Value, sideB.Value);
            if (built < 0) continue;                        // no empty route between them to measure
            if (built >= Narrowest && built <= Widest) continue;

            var names = $"[{string.Join(", ", strait.PiecesA)}] and [{string.Join(", ", strait.PiecesB)}]";
            findings.Add(new Finding("CT12",
                built == 0
                    ? $"the plan put team islands {names} {strait.Blocks} blocks apart and the drawn board "
                      + $"joins them into one landmass — the strait the plan was checked against is not in "
                      + $"the ground, and the walls that guarded it guard nothing"
                    : $"the plan put team islands {names} {strait.Blocks} blocks apart and the drawn board "
                      + $"puts them {built} — the CTW strait wants {Narrowest}–{Widest}, and the plan's "
                      + "verdict was taken before a shape existed",
                Severity.Complaint, Subjects: [.. strait.PiecesA.Concat(strait.PiecesB)]));
        }
        return new Findings(findings);
    }

    /// <summary>Every drawn cell labelled with the landmass it belongs to. Eight-connected, the connectivity
    /// the board's own island decomposition uses, so a mass here is a mass there.</summary>
    private static Dictionary<(int X, int Z), int> Landmasses(HashSet<(int X, int Z)> footprint)
    {
        var label = new Dictionary<(int X, int Z), int>();
        var id = 0;
        var frontier = new Queue<(int X, int Z)>();
        foreach (var start in footprint)
        {
            if (label.ContainsKey(start)) continue;
            label[start] = ++id;
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var (x, z) = frontier.Dequeue();
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var side = (x + dx, z + dz);
                    if (footprint.Contains(side) && label.TryAdd(side, id)) frontier.Enqueue(side);
                }
            }
        }
        return label;
    }

    /// <summary>Which drawn landmass a plan island became: the one holding most of its cells, read at each
    /// plan cell's own middle in blocks. Null where the layout draws none of it.</summary>
    private static int? Landmass(BoardStructure board, int island, IReadOnlyDictionary<(int X, int Z), int> landmassOf)
    {
        var votes = new Dictionary<int, int>();
        foreach (var (cx, cz) in board.Islands[island])
        {
            var middle = (cx * board.Cell + board.Cell / 2, cz * board.Cell + board.Cell / 2);
            if (!landmassOf.TryGetValue(middle, out var mass)) continue;
            votes[mass] = votes.GetValueOrDefault(mass) + 1;
        }
        return votes.Count == 0 ? null : votes.MaxBy(vote => vote.Value).Key;
    }

    /// <summary>The walk between two landmasses over the empty cells around them, in blocks, or -1 where
    /// there is no empty route at all. The plan-side measure taken again: a span a builder crosses rather
    /// than a line through a third mass.</summary>
    private static int Gap(HashSet<(int X, int Z)> footprint,
                           IReadOnlyDictionary<(int X, int Z), int> landmassOf, int sideA, int sideB)
    {
        int minX = footprint.Min(cell => cell.X) - 1, maxX = footprint.Max(cell => cell.X) + 1;
        int minZ = footprint.Min(cell => cell.Z) - 1, maxZ = footprint.Max(cell => cell.Z) + 1;
        bool Empty((int X, int Z) cell) => !footprint.Contains(cell)
            && cell.X >= minX && cell.X <= maxX && cell.Z >= minZ && cell.Z <= maxZ;

        var distance = new Dictionary<(int X, int Z), int>();
        var frontier = new Queue<(int X, int Z)>();
        foreach (var (cell, mass) in landmassOf)
        {
            if (mass != sideA) continue;
            foreach (var side in Cells.N4(cell))
                if (Empty(side) && distance.TryAdd(side, 1)) frontier.Enqueue(side);
        }
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var side in Cells.N4(current))
                if (Empty(side) && distance.TryAdd(side, distance[current] + 1)) frontier.Enqueue(side);
        }

        var nearest = int.MaxValue;
        foreach (var (cell, mass) in landmassOf)
        {
            if (mass != sideB) continue;
            foreach (var side in Cells.N4(cell))
                if (distance.TryGetValue(side, out var here)) nearest = Math.Min(nearest, here);
        }
        return nearest == int.MaxValue ? -1 : nearest;
    }
}
