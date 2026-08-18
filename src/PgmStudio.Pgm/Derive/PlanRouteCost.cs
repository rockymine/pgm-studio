using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Derive;

/// <summary>What a step costs beyond being a step, in blocks where a number is stated in blocks.
///
/// <para><see cref="CorridorWidth"/> is how wide a lane a player wants under them; half of it is the
/// clearance a route is charged for missing, so a walk down the middle of a band pays nothing and one
/// hugging its border pays the most. <see cref="BowRange"/> is how far a held position reaches.</para>
///
/// <para><see cref="ClimbWeight"/> is what a block of climb costs. A step up of one is free — a player walks
/// it — and anything steeper is paid per block, because that is the ground they have to place under
/// themselves to get up. Coming down is free at any height.</para>
///
/// <para><b>The climb weight is uncalibrated and cannot be calibrated from recorded play.</b> Nearly half the
/// standing samples on a long match sit on structure the players built, and the terrain is dug out as well as
/// built on, so any weight fitted to movement is fitted to a surface the players authored rather than to the
/// map's. The rule it encodes is the author's; the number is a placeholder. `docs/gameplay/match-flow.md`
/// §6.12 carries the measurement and the reasoning.</para></summary>
public sealed record RouteCosts(
    int CorridorWidth = 20,
    int EdgeWeight = 3,
    int BridgeWeight = 2,
    int ThreatWeight = 12,
    int BowRange = 30,
    int ClimbWeight = 2)
{
    /// <summary>Nothing charged but the step — the plain walk, for comparing against.</summary>
    public static readonly RouteCosts Flat = new(0, 0, 0, 0, 0, 0);

    /// <summary>How far a player steps up for nothing. One block is a walk; two is a jump onto ground you
    /// first have to make.</summary>
    public const int FreeRise = 1;
}

/// <summary>
/// The cost of standing somewhere, for a walk that is not only trying to be short.
///
/// <para>Three things make ground dear. <b>The edge</b>: a route that only minimises length hugs every
/// border it passes, because a border is the short way round a bend — and on these boards a border is void,
/// which is the one place a player will not walk. <b>The bridge</b>: crossing a build zone means placing
/// blocks under yourself, which is slower and exposed however short it is. And <b>the threat</b>: a
/// defended seam reaches as far as a bow does, so bridged ground running parallel to a wall inside that
/// range is ground a defender covers without moving.</para>
///
/// <para>The third is the one nothing else in the studio can express, and it is the reason a plan-tier walk
/// gets outback backwards. Two ways to a wool are the same length and the same width; one of them lies
/// alongside the wall the defence holds, and the walk that counts only cells calls them equal.</para>
///
/// <para>What this does <b>not</b> model: occlusion. An arrow is stopped by terrain and this has no
/// heights, so exposure here is line-of-flight and not line-of-sight — it over-charges ground a rise
/// happens to shelter.</para>
/// </summary>
public static class PlanRouteCost
{
    /// <summary>The per-cell entry cost over <paramref name="nav"/>. Cells absent from the result cost the
    /// bare step.</summary>
    public static Dictionary<(int X, int Z), int> Build(PlanNav nav, PlanModel plan, RouteCosts costs)
    {
        var extra = new Dictionary<(int X, int Z), int>();
        if (costs == RouteCosts.Flat) return extra;

        var cell = Math.Max(1, nav.Cell);
        var comfort = costs.CorridorWidth / 2 / cell;      // cells of clearance a route wants either side
        var bow = costs.BowRange / cell;

        // Ground only: standing over a build zone is standing over void, so a zone is a boundary for the
        // clearance read as much as the board's edge is.
        var clearance = Cells.Clearance(nav.Ground, nav.Bounds);

        var threat = Threatened(nav, plan, bow);

        foreach (var c in nav.Navigable)
        {
            var add = 0;
            if (comfort > 0)
                add += costs.EdgeWeight * Math.Max(0, comfort - clearance.GetValueOrDefault(c, 0));
            if (nav.Bridge.Contains(c))
            {
                add += costs.BridgeWeight;
                if (threat.Contains(c)) add += costs.ThreatWeight;
            }
            if (add > 0) extra[c] = add;
        }
        return extra;
    }

    /// <summary>A per-cell cost function over <paramref name="nav"/> — everything but the climb, which is a
    /// property of a step rather than of a place. Use <see cref="StepOf"/> to charge that too.</summary>
    public static Func<(int X, int Z), int> Of(PlanNav nav, PlanModel plan, RouteCosts costs)
    {
        var extra = Build(nav, plan, costs);
        return c => 1 + extra.GetValueOrDefault(c, 0);
    }

    /// <summary>The full cost, as a <b>step</b>: what a cell costs to be in, plus what the rise into it cost.
    /// A step up of <see cref="RouteCosts.FreeRise"/> or less is free and so is any descent; a steeper rise is
    /// charged per block over that, because those are the blocks a player places to get up. This is
    /// <c>B246</c>'s rule read at the plan tier, where the height comes from the piece's stated surface rather
    /// than from a built world's columns.</summary>
    public static Func<(int X, int Z), (int X, int Z), int> StepOf(PlanNav nav, PlanModel plan, RouteCosts costs)
    {
        var extra = Build(nav, plan, costs);
        var surface = nav.SurfaceAt;
        var climb = costs.ClimbWeight;
        return (from, to) =>
        {
            var cost = 1 + extra.GetValueOrDefault(to, 0);
            if (climb <= 0) return cost;
            if (!surface.TryGetValue(from, out var was) || !surface.TryGetValue(to, out var now)) return cost;
            var rise = now - was;
            return rise <= RouteCosts.FreeRise ? cost : cost + climb * (rise - RouteCosts.FreeRise);
        };
    }

    /// <summary>The cells a declared wall covers: everything within <paramref name="bowCells"/> of the seam
    /// between the wall's two pieces, over every orbit image. A wall is stated as a pair of pieces, so the
    /// seam is each one's cells that touch the other — the line the structure actually stands on.</summary>
    public static HashSet<(int X, int Z)> Threatened(PlanNav nav, PlanModel plan, int bowCells)
    {
        var covered = new HashSet<(int X, int Z)>();
        if (bowCells <= 0) return covered;

        foreach (var wall in plan.Walls)
            foreach (var seat in Seam(nav, wall.A, wall.B))
                for (var dz = -bowCells; dz <= bowCells; dz++)
                    for (var dx = -bowCells; dx <= bowCells; dx++)
                        if (dx * dx + dz * dz <= bowCells * bowCells)
                            covered.Add((seat.X + dx, seat.Z + dz));
        return covered;
    }

    /// <summary>The seam between two named pieces: each one's cells that touch the other, over every orbit
    /// image. Deleting or charging for both whole pieces measures what happens when two landmasses vanish,
    /// which is a different and much larger question.</summary>
    public static HashSet<(int X, int Z)> Seam(PlanNav nav, string a, string b)
    {
        var seam = new HashSet<(int X, int Z)>();
        foreach (var (cell, piece) in nav.PieceAt)
        {
            var mine = BaseId(piece);
            if (mine != a && mine != b) continue;
            var other = mine == a ? b : a;
            foreach (var n in Cells.N4(cell))
                if (nav.PieceAt.TryGetValue(n, out var np) && BaseId(np) == other) { seam.Add(cell); break; }
        }
        return seam;
    }

    /// <summary>A fanned piece name back to the id the plan states (<c>piece-7#1</c> → <c>piece-7</c>).</summary>
    public static string BaseId(string name)
    {
        var hash = name.IndexOf('#');
        return hash < 0 ? name : name[..hash];
    }
}
