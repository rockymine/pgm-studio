
namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;
using PgmStudio.Geom;
using PgmStudio.Analysis.Layer;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Budget-aware reachability: can a freshly-spawned player bridge from their spawn to each wool with only
/// the placeable blocks their spawn kit grants?
///
/// <para>It asks <see cref="Walk"/> under <see cref="WalkAim.Reach"/> — the aim that minimises blocks placed
/// rather than distance — over the board's own <see cref="WorldWalk.Ground"/>. That answer is the minimum
/// blocks needed to cross, and comparing it against the kit's placeable-block count is the per-life
/// crossing-feasibility signal. What it adds over the count is the rest of what the walk knows: how far round
/// the cheapest crossing goes, and what it falls down on the way.</para>
///
/// <para>The blocks are no longer only bridged void: a rise steeper than a step is blocks a player places
/// too, so a wool up a scarp now costs what climbing it costs. Water is not read here, because swimming is
/// slower and neither a block nor a wall — it moves no verdict this endpoint gives.</para>
/// </summary>
public static class KitReach
{
    /// <summary>What one wool costs this team to reach: the cheapest crossing, and whether the kit pays
    /// for it.</summary>
    /// <param name="Color">The wool to be captured.</param>
    /// <param name="X">Where it stands, east–west.</param>
    /// <param name="Z">Where it stands, north–south.</param>
    /// <param name="BlocksNeeded">How many blocks the cheapest path asks the player to place — bridging
    /// void, and climbing anything steeper than a step.</param>
    /// <param name="Blocks">How far round that cheapest crossing goes, in blocks walked.</param>
    /// <param name="Drops">How many falls over the free height it takes on the way.</param>
    /// <param name="Reachable">Whether any path exists at all, at any cost.</param>
    /// <param name="WithinBudget">Whether the kit grants enough blocks to pay for it.</param>
    /// <param name="Severity">What the verdict is worth — the same word a finding carries.</param>
    /// <param name="Message">The verdict in a sentence, with the numbers in it.</param>
    public sealed record WoolReach(string Color, int X, int Z, int BlocksNeeded, int Blocks, int Drops,
        bool Reachable, bool WithinBudget, string Severity, string Message);
    /// <summary>What one team's spawn kit can reach.</summary>
    /// <param name="Team">The team, by id.</param>
    /// <param name="Kit">The kit it spawns with, by name.</param>
    /// <param name="Budget">How many placeable blocks that kit grants.</param>
    /// <param name="WaterBucket">Whether the kit carries a water bucket, which crosses a gap no block
    /// budget would.</param>
    /// <param name="Wools">One reading per wool the team must capture.</param>
    public sealed record TeamReach(string Team, string Kit, int Budget, bool WaterBucket, List<WoolReach> Wools);
    /// <summary>What every team's kit can reach.</summary>
    /// <param name="HaveLayers">Whether the map has scanned column data. False means nothing was measured,
    /// not that nothing is reachable.</param>
    /// <param name="Severity">The worst verdict over every team.</param>
    /// <param name="Message">That verdict in a sentence.</param>
    /// <param name="Teams">One reading per team.</param>
    public sealed record Result(bool HaveLayers, string Severity, string Message, List<TeamReach> Teams);

    public static Result Check(Dict data, SegmentIndex? segments, int margin = 16)
    {
        var ground = WorldWalk.Ground(data, segments, margin);
        var bounds = ((double)ground.Bounds.X, (double)ground.Bounds.Z,
                      (double)ground.Bounds.MaxX, (double)ground.Bounds.MaxZ);
        var haveLayers = ground.Ground.Count > 0;

        var kitBudgets = KitBudgets(data);
        var regions = AsDict(data.GetValueOrDefault("regions"));

        var teams = new List<TeamReach>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            if (Truthy(sp.GetValueOrDefault("observer"))) continue;
            var team = sp.GetValueOrDefault("team") as string ?? "";
            var kitId = sp.GetValueOrDefault("kit") as string ?? "";
            var (budget, water) = kitBudgets.GetValueOrDefault(kitId, (0, false));

            var start = RegionCentre(SpawnRegion(sp, regions), regions, bounds) is { } seat
                ? Cells.SnapToWalkable(seat, ground.Passable, SnapRadius)
                : null;
            var field = start is { } from
                ? Walk.Field(from, ground, WalkAim.Reach)
                : [];

            var wools = new List<WoolReach>();
            foreach (var (color, wx, wz) in WoolPoints(data, regions, bounds))
            {
                var target = Cells.SnapToWalkable((wx, wz), ground.Passable, SnapRadius);
                var cost = target is { } to && field.TryGetValue(to, out var reached) ? reached : (WalkCost?)null;
                var reachable = cost is not null;
                var need = cost?.Blocks ?? -1;
                var within = reachable && need <= budget;
                var (sev, msg) = (reachable, within) switch
                {
                    (false, _) => ("error", "no bridgeable path from spawn (blocked by void / no-build)"),
                    (_, true) => ("ok", $"{need} block(s) to place over {cost!.Value.Distance} walked "
                                      + $"— kit gives {budget}"),
                    _ => ("warning", $"needs {need} blocks to place but kit gives only {budget}"),
                };
                wools.Add(new WoolReach(color, wx, wz, need, cost?.Distance ?? -1, cost?.Drops ?? 0,
                    reachable, within, sev, msg));
            }
            teams.Add(new TeamReach(team, kitId, budget, water, wools));
        }

        var allWools = teams.SelectMany(t => t.Wools).ToList();
        var worst = allWools.Any(w => !w.Reachable) ? "error"
            : allWools.Any(w => !w.WithinBudget) ? "warning" : "ok";
        var message = worst switch
        {
            "error" => "some wools are unreachable from spawn — the gap can't be bridged (void / no-build)",
            "warning" => "some wools need more bridging blocks than the spawn kit grants",
            _ => "every wool is reachable within the spawn kit's block budget",
        };
        return new Result(haveLayers, worst, message, teams);
    }

    /// <summary>How far a marker may sit off walkable ground and still be walked to. A wool's stated location
    /// is a block in a room, not a cell of terrain, so it lands inside a wall as often as on a floor.</summary>
    private const int SnapRadius = 16;

    // ── kit budget: count placeable blocks (and note a water bucket as a bridging aid) ──────────
    private static Dictionary<string, (int budget, bool water)> KitBudgets(Dict data)
    {
        var map = new Dictionary<string, (int, bool)>();
        foreach (var kit in AsList(data.GetValueOrDefault("kits")).OfType<Dict>())
        {
            var id = kit.GetValueOrDefault("id") as string ?? "";
            var blocks = 0;
            var water = false;
            foreach (var item in AsList(kit.GetValueOrDefault("items")).OfType<Dict>())
            {
                var mat = Normalize(item.GetValueOrDefault("material") as string ?? "");
                var amount = Num(item.GetValueOrDefault("amount")) is { } a ? (int)a : 1;
                if (KitBlocks.IsPlaceable(mat)) blocks += amount;
                else if (mat is "water bucket" or "water") water = true;
            }
            map[id] = (blocks, water);
        }
        return map;
    }

    // ── spawn / wool nav points (mirrors Traversability) ───────────────────────────────────────
    private static Dict? SpawnRegion(Dict sp, Dict regions)
    {
        var r = sp.GetValueOrDefault("region");
        return r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
    }

    private static IEnumerable<(string color, int x, int z)> WoolPoints(Dict data, Dict regions, (double, double, double, double) bounds)
    {
        foreach (var w in AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = w.GetValueOrDefault("color") as string ?? "";
            var loc = AsDict(w.GetValueOrDefault("location"));
            if (Num(loc.GetValueOrDefault("x")) is { } lx && Num(loc.GetValueOrDefault("z")) is { } lz)
                yield return (color, (int)lx, (int)lz);
            else if (RegionCentre(regions.GetValueOrDefault(w.GetValueOrDefault("wool_room_region") as string ?? "") as Dict, regions, bounds) is { } c)
                yield return (color, c.x, c.z);
        }
    }

    private static (int x, int z)? RegionCentre(Dict? region, Dict registry, (double, double, double, double) bounds)
    {
        if (region is null) return null;
        if (RegionGeometry2d.ToGeometry(region, bounds, registry) is { IsEmpty: false } geom)
        {
            var centroid = geom.Centroid;
            var p = geom.Contains(centroid) ? centroid : geom.InteriorPoint;
            return ((int)p.X, (int)p.Y);
        }
        var bb = AsDict(region.GetValueOrDefault("bounds_2d"));
        if (bb.Count == 0) return null;
        var mn = AsDict(bb.GetValueOrDefault("min"));
        var mx = AsDict(bb.GetValueOrDefault("max"));
        if (Num(mn.GetValueOrDefault("x")) is not { } mnx || Num(mn.GetValueOrDefault("z")) is not { } mnz
            || Num(mx.GetValueOrDefault("x")) is not { } mxx || Num(mx.GetValueOrDefault("z")) is not { } mxz)
            return null;
        return ((int)((mnx + mxx) / 2), (int)((mnz + mxz) / 2));
    }


    private static string Normalize(string s) => s.Trim().ToLowerInvariant().Replace('_', ' ');
    private static bool Truthy(object? v) => v is true || (v is string s && s is "true" or "1");
    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
