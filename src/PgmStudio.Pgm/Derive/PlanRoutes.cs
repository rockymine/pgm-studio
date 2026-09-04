using PgmStudio.Geom;

namespace PgmStudio.Pgm.Derive;

/// <summary>One way of making a journey: the cells walked, how long it is, and the piece sequence it reads as.
/// <see cref="Label"/> says what distinguishes it — the direct walk, or which side of which hole it commits
/// to.</summary>
public sealed record RouteOption(
    string Label, int Length, IReadOnlyList<(int X, int Z)> Path, IReadOnlyList<string> Pieces)
{
    /// <summary>How much longer than the shortest this way is — a second way at 1.3× is a route someone takes,
    /// one at 4× is an escape hatch.</summary>
    public double Ratio(int shortest) => shortest <= 0 ? 1 : (double)Length / shortest;
}

/// <summary>A hole read against one journey: how big it is, where it sits, and whether the journey may pass on
/// either side of it. <see cref="Ways"/> is the ray-cut count — 2 is a genuine choice, 1 is one committed way.
/// <see cref="OnCorridor"/> says whether the journey goes anywhere near it at all; a hole off the corridor is
/// reported and not enumerated, because a ray cast from it says nothing about this journey.</summary>
public sealed record HoleRead(
    int Index, int Area, int CentroidX, int CentroidZ, bool OnCorridor, int Ways, string RayAxis);

/// <summary>Where a journey stops being one way and where it becomes one again.
///
/// <para><see cref="Split"/> is the last cell every option shares walking out from the origin — the point a
/// player actually chooses at. <see cref="Fuse"/> is the first cell they share again walking back from the
/// target — past it there is one way in whatever was decided. The two are different questions and a board
/// answers them separately: kanto's approach splits early and fuses in front of the wool, townside's does the
/// same thing over a shorter run, and a board with one option has neither.</para>
///
/// <para><see cref="Between"/> is how far apart they stand along the shortest walk — the length of the stretch
/// the choice is live over. A split and a fuse one cell apart is a formality; a hundred blocks of it is two
/// lanes.</para></summary>
public sealed record RouteFork((int X, int Z) Split, (int X, int Z) Fuse, int Between);

/// <summary>Everything one journey answers: how far, along which ways, through how much of the board.</summary>
public sealed record StrokeRead(
    (int X, int Z) From, (int X, int Z) To, int? Shortest,
    IReadOnlyList<RouteOption> Options,
    IReadOnlySet<(int X, int Z)> Corridor,
    double CorridorShare,
    IReadOnlyList<HoleRead> Holes,
    RouteFork? Fork);

/// <summary>
/// A journey across a plan, read before anything is built: from any cell of the board to any other, how far it
/// is, how wide the way is, and <b>how many ways there are</b>.
///
/// <para>The last of those is the one nothing else answers. Every reachability read in the studio stops at
/// yes — the piece graph says a wool can be got to, the traversability gate says every objective connects —
/// and a board where the only way in is one door passes both exactly as a board offering a choice does. A
/// second way is not an escape hatch: an attacker taking the far leg of a frontline is making a decision the
/// defender has to cover, and the difference between one way and two is most of what a hole in a shape is
/// for.</para>
///
/// <para>The options are enumerated by cutting, not by searching. A ray is cast from a hole out to the board's
/// edge, perpendicular to the journey, and the walk is repeated with that ray removed: what survives is a
/// route committed to the other side. Two survivors is a choice. Options are kept when the <b>piece
/// sequence</b> genuinely differs, because two walks a cell apart are one way described twice.</para>
/// </summary>
public static class PlanRoutes
{
    /// <summary>How much longer than the shortest a route may be and still be one people walk — the corridor
    /// slack the flow reading is stated in.</summary>
    public const double CorridorSlack = 0.30;

    /// <summary>Holes smaller than this are gaps between rectangles, not places a route goes round.</summary>
    public const int HoleFloor = 2;

    public static StrokeRead Read(PlanNav nav, (int X, int Z) from, (int X, int Z) to,
        double slack = CorridorSlack)
    {
        var within = nav.Navigable;
        var ground = nav.Walkable();
        var seat = ground.Stand(from);
        var target = ground.Stand(to);
        var direct = seat is { } a && target is { } b ? Walk.Between(a, b, ground) : null;
        if (direct is null)
            return new StrokeRead(from, to, null, [], new HashSet<(int X, int Z)>(), 0,
                ReadHoles(nav, new HashSet<(int X, int Z)>(), from, to, within), null);

        var (start, goal) = (seat!.Value, target!.Value);
        var shortest = direct.Cost.Distance;
        var corridor = Walk.Corridor(start, goal, ground, slack).Select(place => place.Cell).ToHashSet();
        var share = within.Count == 0 ? 0 : (double)corridor.Count / within.Count;

        var options = new List<RouteOption> { Option("direct", direct, nav) };
        var seen = new HashSet<string> { Signature(options[0]) };

        var holes = ReadHoles(nav, corridor, from, to, within);
        foreach (var hole in holes.Where(h => h.OnCorridor && h.Ways == 2))
        {
            var cells = nav.Holes[hole.Index];
            var horizontal = hole.RayAxis == "x";
            foreach (var (forward, side) in Sides(horizontal))
            {
                var open = Without(within, Cells.RayCut(cells, nav.Bounds, horizontal, forward), from, to);
                if (Walk.Between(start, goal, ground.Narrowed(open)) is not { } path) continue;
                var option = Option($"hole {hole.Index}: {side}", path, nav);
                if (seen.Add(Signature(option))) options.Add(option);
            }
        }

        options.Sort((a, b) => a.Length.CompareTo(b.Length));
        return new StrokeRead(from, to, shortest, options, corridor, share, holes, Fork(options, ground));
    }

    /// <summary>Where the options part company and where they meet again. The split is the end of the run
    /// every option walks identically out of the origin; the fuse is the start of the run they all walk
    /// identically into the target. Null when there is only one way, which is the common case — over 490
    /// recorded approach bundles the flow reading found 63% single-corridor end to end.</summary>
    private static RouteFork? Fork(List<RouteOption> options, WalkGround ground)
    {
        if (options.Count < 2) return null;

        var shortestPath = options[0].Path;
        var prefix = options.Min(o => Common(o.Path, shortestPath, fromEnd: false));
        var suffix = options.Min(o => Common(o.Path, shortestPath, fromEnd: true));

        // A path that shares everything with the shortest is not a second way; the caller deduped by piece
        // sequence, so this only guards a degenerate walk.
        if (prefix == 0 || suffix == 0 || prefix + suffix > shortestPath.Count) return null;

        var split = shortestPath[prefix - 1];
        var fuse = shortestPath[^suffix];
        var between = ground.Stand(split) is { } from && ground.Stand(fuse) is { } to
            ? Walk.Between(from, to, ground)?.Cost.Distance ?? 0
            : 0;
        return new RouteFork(split, fuse, between);

        static int Common(IReadOnlyList<(int X, int Z)> a, IReadOnlyList<(int X, int Z)> b, bool fromEnd)
        {
            var n = 0;
            while (n < a.Count && n < b.Count
                   && (fromEnd ? a[a.Count - 1 - n] == b[b.Count - 1 - n] : a[n] == b[n])) n++;
            return n;
        }
    }

    /// <summary>Every hole the board carries, read against this journey. A hole is <em>on</em> the corridor
    /// when the ribbon runs against its border — a route that never comes near a hole is not choosing a side
    /// of it, and a ray cast from a distant hole cuts ground this journey never sees.</summary>
    private static List<HoleRead> ReadHoles(PlanNav nav, IReadOnlySet<(int X, int Z)> corridor,
        (int X, int Z) from, (int X, int Z) to, IReadOnlySet<(int X, int Z)> within)
    {
        var reads = new List<HoleRead>();
        // A journey running mostly along x is offered a way over and a way under, and the cut that separates
        // those two is the one running along z — the ray goes across the direction of travel, never with it.
        var alongX = Math.Abs(to.X - from.X) >= Math.Abs(to.Z - from.Z);

        for (var index = 0; index < nav.Holes.Count; index++)
        {
            var hole = nav.Holes[index];
            if (hole.Count < HoleFloor) continue;

            var centroidX = (int)Math.Round(hole.Average(c => (double)c.X));
            var centroidZ = (int)Math.Round(hole.Average(c => (double)c.Z));
            var touching = corridor.Count > 0
                && hole.Any(c => Cells.N4(c).Any(corridor.Contains));

            var ways = 0;
            var axis = alongX ? "z" : "x";
            if (touching)
            {
                // Try the cut across the journey first; a hole long in the other direction may still separate
                // only the other way, so the second orientation is asked before the answer is "one way".
                foreach (var horizontal in (bool[])[!alongX, alongX])
                {
                    var count = Cells.WaysRound(from, to, [.. hole], within, horizontal);
                    if (count <= ways) continue;
                    ways = count;
                    axis = horizontal ? "x" : "z";
                    if (ways == 2) break;
                }
            }
            reads.Add(new HoleRead(index, hole.Count, centroidX, centroidZ, touching, ways, axis));
        }
        return reads;
    }

    private static IEnumerable<(bool Forward, string Side)> Sides(bool horizontal) =>
        horizontal
            ? [(true, "west of it"), (false, "east of it")]
            : [(true, "north of it"), (false, "south of it")];

    private static HashSet<(int X, int Z)> Without(IReadOnlySet<(int X, int Z)> within,
        IReadOnlyCollection<(int X, int Z)> cut, (int X, int Z) from, (int X, int Z) to)
    {
        var open = new HashSet<(int X, int Z)>(within);
        foreach (var cell in cut) if (cell != from && cell != to) open.Remove(cell);
        return open;
    }

    private static RouteOption Option(string label, WalkPath walked, PlanNav nav)
    {
        var path = walked.Cells.Select(cell => (cell.X, cell.Z)).ToList();
        var pieces = new List<string>();
        foreach (var cell in path)
        {
            var name = nav.PieceAt.TryGetValue(cell, out var piece) ? piece : "«gap»";
            if (pieces.Count == 0 || pieces[^1] != name) pieces.Add(name);
        }
        return new RouteOption(label, walked.Cost.Distance, path, pieces);
    }

    // Two walks a cell apart are one way described twice; two walks over different pieces are two ways.
    private static string Signature(RouteOption option) => string.Join(" → ", option.Pieces);
}
