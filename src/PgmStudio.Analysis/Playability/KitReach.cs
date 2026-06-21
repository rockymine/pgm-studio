namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Budget-aware reachability: can a freshly-spawned player bridge from their spawn to each wool with
/// only the placeable blocks their spawn kit grants? Builds the same navigability grid as
/// <see cref="Traversability"/> (walkable surface ∪ bridgeable buildable) but, instead of asking
/// "is there a path", finds the <em>cheapest</em> path via a 0-1 BFS where a walkable cell costs 0 and
/// a bridgeable cell costs 1 (one block you must place). That minimum = blocks needed to cross; compared
/// against the kit's placeable-block count it yields a per-life crossing-feasibility signal.
/// </summary>
public static class KitReach
{
    public sealed record WoolReach(string Color, int X, int Z, int BlocksNeeded, bool Reachable, bool WithinBudget, string Severity, string Message);
    public sealed record TeamReach(string Team, string Kit, int Budget, bool WaterBucket, List<WoolReach> Wools);
    public sealed record Result(bool HaveLayers, string Severity, string Message, List<TeamReach> Teams);

    private const int Unreachable = int.MaxValue;

    public static Result Check(Dict data, HashSet<(int, int)>? walkableColumns, HashSet<(int, int)>? y0Columns,
        (int, int, int, int)? bbox = null, int margin = 16)
    {
        var b = Buildability.Compute(data, y0Columns, bbox, margin);
        int nx = b.Width, nz = b.Height, minX = b.MinX, minZ = b.MinZ, n = nx * nz;
        var bounds = ((double)b.MinX, (double)b.MinZ, (double)b.MaxX, (double)b.MaxZ);
        var haveLayers = walkableColumns is { Count: > 0 };

        // place-cost per cell: 0 walkable ground · 1 bridgeable (buildable/restricted) · -1 impassable.
        // Walkable = the cleaned-base footprint (floating masses pruned), NOT raw surface — a build floating
        // over void must not pose as free standing-ground at its own high Y (the 2D grid is Y-agnostic).
        var walkable = new bool[n];
        if (walkableColumns is not null)
            foreach (var (x, z) in walkableColumns)
            {
                int ix = x - minX, iz = z - minZ;
                if (ix >= 0 && ix < nx && iz >= 0 && iz < nz) walkable[iz * nx + ix] = true;
            }
        var cost = new int[n];
        for (var i = 0; i < n; i++)
            cost[i] = walkable[i] ? 0 : (b.Verdict[i] == 0 || b.Verdict[i] == 3) ? 1 : -1;

        var kitBudgets = KitBudgets(data);
        var regions = AsDict(data.GetValueOrDefault("regions"));

        var teams = new List<TeamReach>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            if (Truthy(sp.GetValueOrDefault("observer"))) continue;
            var team = sp.GetValueOrDefault("team") as string ?? "";
            var kitId = sp.GetValueOrDefault("kit") as string ?? "";
            var (budget, water) = kitBudgets.GetValueOrDefault(kitId, (0, false));

            var start = RegionCell(SpawnRegion(sp, regions), regions, bounds, minX, minZ, nx, nz);
            var dist = start is { } s ? BridgeCost(cost, nx, nz, s.ix, s.iz) : null;

            var wools = new List<WoolReach>();
            foreach (var (color, wx, wz) in WoolPoints(data, regions, bounds))
            {
                var target = NearestNavigable(cost, nx, nz, wx - minX, wz - minZ);
                var need = (dist is not null && target is { } t) ? dist[t.iz * nx + t.ix] : Unreachable;
                var reachable = need != Unreachable;
                var within = reachable && need <= budget;
                var (sev, msg) = !reachable
                    ? ("error", "no bridgeable path from spawn (blocked by void / no-build)")
                    : within ? ("ok", $"{need} block(s) to bridge — kit gives {budget}")
                             : ("warning", $"needs {need} blocks to bridge but kit gives only {budget}");
                wools.Add(new WoolReach(color, wx, wz, reachable ? need : -1, reachable, within, sev, msg));
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

    private static (int ix, int iz)? RegionCell(Dict? region, Dict regions, (double, double, double, double) bounds, int minX, int minZ, int nx, int nz)
    {
        if (RegionCentre(region, regions, bounds) is not { } c) return null;
        return NearestNavigableSeed(c.x - minX, c.z - minZ, nx, nz);
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

    private static (int ix, int iz)? NearestNavigableSeed(int ix, int iz, int nx, int nz)
        => (ix >= 0 && ix < nx && iz >= 0 && iz < nz) ? (ix, iz) : null;

    // ── 0-1 BFS: min blocks placed to reach every navigable cell from a start ───────────────────
    private static int[] BridgeCost(int[] cost, int nx, int nz, int sx, int sz)
    {
        var dist = new int[nx * nz];
        Array.Fill(dist, Unreachable);
        var start = NearestNavigable(cost, nx, nz, sx, sz);
        if (start is not { } s) return dist;
        var si = s.iz * nx + s.ix;
        dist[si] = cost[si];                       // standing-cost of the start cell (0 if walkable)
        var deque = new LinkedList<int>();
        deque.AddFirst(si);
        while (deque.First is { } node)
        {
            deque.RemoveFirst();
            var i = node.Value;
            int x = i % nx, z = i / nx;
            foreach (var (dx, dz) in Neigh)
            {
                int ax = x + dx, az = z + dz;
                if (ax < 0 || ax >= nx || az < 0 || az >= nz) continue;
                var j = az * nx + ax;
                if (cost[j] < 0) continue;          // impassable (void / no-build)
                var nd = dist[i] + cost[j];
                if (nd >= dist[j]) continue;
                dist[j] = nd;
                if (cost[j] == 0) deque.AddFirst(j); else deque.AddLast(j);
            }
        }
        return dist;
    }

    private static readonly (int, int)[] Neigh = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    // snap a point to the nearest navigable (cost ≥ 0) cell within a small radius
    private static (int ix, int iz)? NearestNavigable(int[] cost, int nx, int nz, int ix, int iz, int snap = 4)
    {
        for (var r = 0; r <= snap; r++)
            for (var dz = -r; dz <= r; dz++)
                for (var dx = -r; dx <= r; dx++)
                {
                    int x = ix + dx, z = iz + dz;
                    if (x >= 0 && x < nx && z >= 0 && z < nz && cost[z * nx + x] >= 0) return (x, z);
                }
        return null;
    }

    private static string Normalize(string s) => s.Trim().ToLowerInvariant().Replace('_', ' ');
    private static bool Truthy(object? v) => v is true || (v is string s && s is "true" or "1");
    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
