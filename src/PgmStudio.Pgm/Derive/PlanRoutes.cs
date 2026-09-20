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

/// <summary>One decision on a journey: a hole it may pass either side of, where the choice is made, where
/// the two ways meet again, and what the other way costs.
///
/// <para><b>A journey has one of these per door, not one in total.</b> A board offering two choices offers
/// them at two places, and an envelope from the first parting to the last merge describes neither — on
/// townside it spans 177 blocks of a 266-block walk. <see cref="Live"/> is how far the choice stays open: a
/// split and a merge a cell apart is a formality, a hundred blocks of it is two lanes.</para>
///
/// <para>Which side the journey is measured from is the <b>shortest</b> route, so a door that changes nothing
/// against it reports nothing rather than a span of zero.</para></summary>
/// <param name="Hole">The hole this decision is about, indexed into the board's own list.</param>
/// <param name="Area">How big that hole is, in cells.</param>
/// <param name="At">Where it sits.</param>
/// <param name="Split">The last cell both ways share walking out — the cell a player chooses at.</param>
/// <param name="Merge">The first cell they share again.</param>
/// <param name="Live">How far apart those two stand along the walk.</param>
/// <param name="Shortest">What the shortest way costs.</param>
/// <param name="Other">What the other side of this door costs.</param>
public sealed record RouteFork(
    int Hole, int Area, (int X, int Z) At,
    (int X, int Z) Split, (int X, int Z) Merge, int Live,
    int Shortest, int Other)
{
    /// <summary>What the other way costs against the shortest — 1.0 where they are the same length. The
    /// corpus's own second ways sit at a median 1.31× and never past 1.92×.</summary>
    public double Ratio => Shortest <= 0 ? 1 : (double)Other / Shortest;
}

/// <summary>Everything one journey answers: how far, along which ways, through how much of the board.</summary>
public sealed record StrokeRead(
    (int X, int Z) From, (int X, int Z) To, int? Shortest,
    IReadOnlyList<RouteOption> Options,
    IReadOnlySet<(int X, int Z)> Corridor,
    double CorridorShare,
    IReadOnlyList<HoleRead> Holes,
    IReadOnlyList<RouteFork> Forks);

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

    /// <param name="walker">The orbit image making the journey, whose own ground it is then read over —
    /// another team's spawn and the wool room this one defends are shut to it. Null asks about the board
    /// rather than about a side, which is what a shape with no team on it can answer.</param>
    /// <param name="over">The ground this journey runs on, where the demand set narrows it further than the
    /// walker's own — a defence rotating behind its own hole does not bridge the neutral crossing to do it.
    /// Null walks the walker's whole ground.</param>
    public static StrokeRead Read(PlanNav nav, (int X, int Z) from, (int X, int Z) to,
        int? walker = null, WalkGround? over = null, double slack = CorridorSlack)
    {
        var ground = over ?? (walker is { } team ? nav.For(team) : nav.Walkable());
        var within = ground.Footprint;
        var seat = ground.Stand(from);
        var target = ground.Stand(to);
        var direct = seat is { } a && target is { } b ? Walk.Between(a, b, ground) : null;
        if (direct is null)
            return new StrokeRead(from, to, null, [], new HashSet<(int X, int Z)>(), 0,
                ReadHoles(nav, new HashSet<(int X, int Z)>(), from, to, within), []);

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
        return new StrokeRead(from, to, shortest, options, corridor, share, holes,
            Forks(nav, ground, holes, start, goal, direct, within, from, to));
    }

    /// <summary>Every decision the journey carries, one per door it may pass either side of. The shortest
    /// route is what each door is measured against: its ray is cut on whichever side changes that route, and
    /// the stretch the two walk apart is the choice. A door whose two sides walk the same route reports
    /// nothing, which is the honest answer at that reference rather than a span of zero.
    ///
    /// <para>A door separating the route in two places answers twice, because it is two choices over one
    /// hole.</para></summary>
    private static List<RouteFork> Forks(PlanNav nav, WalkGround ground, IReadOnlyList<HoleRead> holes,
        WalkPlace start, WalkPlace goal, WalkPath direct, IReadOnlySet<(int X, int Z)> within,
        (int X, int Z) from, (int X, int Z) to)
    {
        var forks = new List<RouteFork>();
        var lead = direct.Cells.Select(cell => (cell.X, cell.Z)).ToList();

        foreach (var hole in holes.Where(read => read.OnCorridor && read.Ways == 2))
        {
            var cells = nav.Holes[hole.Index];
            var horizontal = hole.RayAxis == "x";
            WalkPath? other = null;
            foreach (var forward in (bool[])[true, false])
            {
                var open = Without(within, Cells.RayCut(cells, nav.Bounds, horizontal, forward), from, to);
                if (Walk.Between(start, goal, ground.Narrowed(open)) is not { } walked) continue;
                if (walked.Cells.Select(cell => (cell.X, cell.Z)).SequenceEqual(lead)) continue;
                other = walked;
                break;
            }
            if (other is null) continue;

            var apart = other.Cells.Select(cell => (cell.X, cell.Z)).ToHashSet();
            var run = -1;
            for (var step = 0; step <= lead.Count; step++)
            {
                var shared = step < lead.Count && apart.Contains(lead[step]);
                if (!shared && run < 0 && step < lead.Count) run = step;
                if (!shared || run < 0) continue;
                var split = lead[Math.Max(0, run - 1)];
                var merge = lead[Math.Min(step, lead.Count - 1)];
                var live = ground.Stand(split) is { } one && ground.Stand(merge) is { } two
                    ? Walk.Between(one, two, ground)?.Cost.Distance ?? 0
                    : 0;
                forks.Add(new RouteFork(hole.Index, hole.Area, (hole.CentroidX, hole.CentroidZ),
                    split, merge, live, direct.Cost.Distance, other.Cost.Distance));
                run = -1;
            }
        }
        return forks;
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
