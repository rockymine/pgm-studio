using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Derive;

/// <summary>How one objective is come at, and by whom. Distances are blocks.</summary>
/// <param name="Attack">The attacker's walk from their own spawn.</param>
/// <param name="Defend">The defender's walk from theirs — the same goal, from the other side.</param>
/// <param name="Ways">Distinct routes in, kept when the piece sequence differs.</param>
/// <param name="SharedRoad">True when the defender's own shortest walk runs through the attackers'
/// merge, so both sides come up the same road; false when going round it is shorter, which is a defence
/// arriving from behind the objective.</param>
/// <param name="MergeDetour">What bypassing the merge saves the defender. Zero is a shared road.</param>
public sealed record FlowLeg(
    string Goal, int Attack, int Defend, int Ways,
    (int X, int Z)? Split, (int X, int Z)? Fuse,
    int SplitWidth, int FuseWidth, int MergeToGoal,
    bool SharedRoad, int MergeDetour)
{
    /// <summary>How far the defender travels against how far the attacker does. The corpus reads match length
    /// off this before anything else geometric: under a quarter runs a median 7.6 minutes against 3.9 over
    /// 0.6 (`match-flow.md` §6.10).</summary>
    public double DefenderRatio => Attack <= 0 ? 0 : Defend / (double)Attack;
}

/// <summary>One stretch of ground no journey reaches, in blocks.</summary>
public sealed record DeadPlace(int Area, int CentroidX, int CentroidZ, IReadOnlyList<string> Pieces);

/// <summary>
/// What a board asks of the two sides, read off the plan before anything is built — and the account that
/// explains a coverage render rather than leaving a reader to infer it from red rectangles.
///
/// <para>Routes are <b>a capture board's question</b>. A wool is carried back, so the same ground is walked
/// out and in and the two sides meet somewhere definite; that is what a split, a merge and a defender's
/// relation to them describe. A destroy board has no carry and its own account, so this reports its dead
/// ground and stays quiet about flow rather than inventing a reading for it.</para>
/// </summary>
public static class PlanFlow
{
    /// <summary>Under this, a split or a merge is a doorway rather than a place: everyone arrives at one
    /// point and the fight has no room. Stated in blocks across the narrower axis.</summary>
    public const int TightPassage = 8;

    /// <summary>Dead stretches smaller than this are slivers between corridors, not places.</summary>
    public const int PlaceFloor = 100;

    public sealed record Result(
        string Gamemode, IReadOnlyList<FlowLeg> Legs,
        int GroundBlocks, int DeadBlocks, IReadOnlyList<DeadPlace> DeadPlaces, int Cell)
    {
        public double DeadShare => GroundBlocks <= 0 ? 0 : DeadBlocks / (double)GroundBlocks;
    }

    public static Result Read(PlanModel plan)
    {
        var nav = PlanNav.Of(plan);
        var cell = nav.Cell;
        var goals = nav.Waypoints().Where(w => w.Kind is "wool" or "destroyable" or "core").ToList();
        var gamemode = goals.Count == 0 ? "none"
            : goals.Any(g => g.Kind == "wool") ? "ctw" : "destroy";

        // The ground the match uses, on the coverage read's own rule: every waypoint pair claims a corridor.
        var waypoints = nav.Waypoints().Select(w => nav.Snap(w.Cell)).Where(c => c is not null)
            .Select(c => c!.Value).Distinct().ToList();
        var ground = nav.Walkable();
        var used = new HashSet<(int X, int Z)>();
        for (var i = 0; i < waypoints.Count; i++)
            for (var j = i + 1; j < waypoints.Count; j++)
                used.UnionWith(Walk.Corridor(waypoints[i], waypoints[j], ground, Walk.Detour));
        foreach (var w in waypoints)
            for (var dz = -2; dz <= 2; dz++)
                for (var dx = -2; dx <= 2; dx++)
                    if (nav.Navigable.Contains((w.X + dx, w.Z + dz))) used.Add((w.X + dx, w.Z + dz));

        var dead = nav.Ground.Where(c => !used.Contains(c)).ToHashSet();
        var places = new List<DeadPlace>();
        var seen = new HashSet<(int X, int Z)>();
        foreach (var start in dead)
        {
            if (!seen.Add(start)) continue;
            var patch = Cells.Flood([start], dead);
            seen.UnionWith(patch);
            if (patch.Count * cell * cell < PlaceFloor) continue;
            places.Add(new DeadPlace(
                patch.Count * cell * cell,
                (int)patch.Average(c => (double)c.Item1) * cell,
                (int)patch.Average(c => (double)c.Item2) * cell,
                // named by how much of the patch each piece is, so the first one read is the one to act on
                [.. patch.Select(c => PlanRouteCost.BaseId(nav.PieceAt.GetValueOrDefault(c, "—")))
                    .Where(n => n != "—")
                    .GroupBy(n => n).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => $"{g.Key} ({g.Count() * cell * cell})")]));
        }
        places.Sort((a, b) => b.Area.CompareTo(a.Area));

        var legs = gamemode == "ctw" ? Legs(nav, plan, cell) : [];
        return new Result(gamemode, legs, nav.Ground.Count * cell * cell, dead.Count * cell * cell, places, cell);
    }

    private static List<FlowLeg> Legs(PlanNav nav, PlanModel plan, int cell)
    {
        var legs = new List<FlowLeg>();
        var attacker = nav.Waypoints().FirstOrDefault(w => w.Kind == "spawn" && w.K == 0);
        var defender = nav.Waypoints().FirstOrDefault(w => w.Kind == "spawn" && w.K == 1);
        if (attacker is null || defender is null) return legs;
        if (nav.Snap(attacker.Cell) is not { } from || nav.Snap(defender.Cell) is not { } den) return legs;
        var ground = nav.Walkable();

        foreach (var goal in nav.Waypoints().Where(w => w.Kind == "wool" && w.K == 1))
        {
            if (nav.Snap(goal.Cell) is not { } to) continue;
            var read = PlanRoutes.Read(nav, from, to);
            if (read.Shortest is not { } attack) continue;
            var defend = Walk.Between(den, to, ground)?.Cost.Distance ?? 0;

            var split = read.Fork?.Split;
            var fuse = read.Fork?.Fuse;
            var viaMerge = fuse is { } f
                ? (Walk.Between(den, f, ground)?.Cost.Distance ?? 0) + (Walk.Between(f, to, ground)?.Cost.Distance ?? 0)
                : 0;
            var detour = fuse is null ? 0 : Math.Max(0, viaMerge - defend);

            legs.Add(new FlowLeg(
                $"{goal.Kind} at ({goal.X * cell}, {goal.Z * cell})",
                attack, defend, read.Options.Count,
                split, fuse,
                split is { } s ? Width(nav, s) * cell : 0,
                fuse is { } g ? Width(nav, g) * cell : 0,
                fuse is { } h ? Walk.Between(h, to, ground)?.Cost.Distance ?? 0 : 0,
                fuse is not null && detour == 0, detour));
        }
        return legs;
    }

    /// <summary>How wide the ground is where a journey passes through it: the narrower of the cell's own
    /// horizontal and vertical run. A split or a merge is a place players meet, and one no wider than a
    /// doorway is a queue rather than a fight.</summary>
    private static int Width(PlanNav nav, (int X, int Z) at) => Cells.MinRunWidthRaw(nav.Navigable, [at]);

    /// <summary>The account in prose: what the board asks of each side, and what that leaves unused. Written
    /// to be read before the picture, because a render says <em>where</em> ground is dead and only the flow
    /// says <em>why</em>.</summary>
    public static string Describe(Result flow)
    {
        var text = new StringBuilder();
        var cell = flow.Cell;

        if (flow.Gamemode == "none")
        {
            text.AppendLine("This plan states no objective, so there is no journey to read and nothing to call dead.");
            return text.ToString();
        }

        if (flow.Gamemode == "ctw")
        {
            text.AppendLine("HOW THIS BOARD IS COME AT");
            text.AppendLine();
            if (flow.Legs.Count == 0)
                text.AppendLine("  Both spawns and an enemy wool are needed to read a route, and this plan is missing one.");
            foreach (var leg in flow.Legs)
            {
                text.AppendLine($"  {leg.Goal}");
                text.AppendLine($"    The attacker walks {leg.Attack} blocks to it; the defender {leg.Defend}, "
                    + $"a ratio of {leg.DefenderRatio:F2}.");
                text.AppendLine("      " + (leg.DefenderRatio <= 0.25
                    ? "A defence that close is on the objective long before the attack is, which is the shape of a long match."
                    : leg.DefenderRatio >= 0.6
                        ? "A defence with that far to come arrives about when the attack does, which is the shape of a short one."
                        : "Neither side is obviously first to it."));

                if (leg.Split is null || leg.Fuse is null)
                {
                    text.AppendLine("    One way in, end to end: nothing forks and nothing merges, so the whole "
                        + "approach is one road to hold.");
                }
                else
                {
                    text.AppendLine($"    {leg.Ways} ways in. They part at ({leg.Split.Value.X * cell}, "
                        + $"{leg.Split.Value.Z * cell}) and meet again at ({leg.Fuse.Value.X * cell}, "
                        + $"{leg.Fuse.Value.Z * cell}), {leg.MergeToGoal} blocks short of the objective.");
                    text.AppendLine("      " + (leg.SharedRoad
                        ? "The defender's own walk runs through that merge, so both sides come up the same road. "
                          + "A defence has to be pushed forward to hold it, and the further forward the merge sits "
                          + "the more of the board it gives up to do so."
                        : $"Going round the merge saves the defender {leg.MergeDetour} blocks, so the defence arrives "
                          + "from behind the objective while the attack arrives at its front. Those are different "
                          + "ground and the objective is the easier for it."));
                    foreach (var (what, width, at) in new[]
                        { ("split", leg.SplitWidth, leg.Split.Value), ("merge", leg.FuseWidth, leg.Fuse.Value) })
                        if (width > 0 && width < TightPassage)
                            text.AppendLine($"      The {what} is {width} blocks across. Everyone meets at one point "
                                + $"and there is no room to fight over it — widen ({at.X * cell}, {at.Z * cell}) or move it.");
                }
                text.AppendLine();
            }
        }
        else
        {
            text.AppendLine("HOW THIS BOARD IS COME AT");
            text.AppendLine();
            text.AppendLine("  A destroy board is not read for routes here. There is no wool to carry back, so the "
                + "two sides do not meet at a definite place the way a capture board's do, and a split and a merge "
                + "would not mean what they mean there. The ground read below still holds.");
            text.AppendLine();
        }

        text.AppendLine("WHAT NO JOURNEY REACHES");
        text.AppendLine();
        if (flow.DeadBlocks == 0)
        {
            text.AppendLine("  Every piece of ground is on somebody's way somewhere.");
            return text.ToString();
        }

        text.AppendLine($"  {flow.DeadBlocks} of {flow.GroundBlocks} blocks "
            + $"({100 * flow.DeadShare:F0}%) sit off every route between the places this board has. "
            + "Reachable, and on the way to nothing.");
        if (flow.DeadPlaces.Count == 0)
        {
            text.AppendLine($"    None of it is a place — every stretch is under {PlaceFloor} blocks, which is a "
                + "sliver between corridors rather than ground anyone would notice.");
            return text.ToString();
        }
        foreach (var place in flow.DeadPlaces.Take(6))
            text.AppendLine($"    {place.Area,6} blocks at ({place.CentroidX}, {place.CentroidZ})"
                + (place.Pieces.Count == 0 ? "" : $" — {string.Join(", ", place.Pieces)}"));
        if (flow.DeadPlaces.Count > 6)
            text.AppendLine($"    and {flow.DeadPlaces.Count - 6} smaller.");
        text.AppendLine();
        text.AppendLine("  Ground is dead because no journey passes it, not because it is far out. Bring an "
            + "objective to it, put a route through it, or take it off the board — decorating it only means "
            + "players look at it on the way past.");
        return text.ToString();
    }
}
