namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Objective-chain traversability: is the spawn↔wool chain connected over the navigability map (walkable
/// surface ∪ bridgeable buildable)? The navigable cells are split into 4-connected components
/// (<see cref="GridComponents"/>) and every objective point is snapped to the component it sits in; the chain
/// is traversable when they all share one.
///
/// <para>A destroyable and a core gate <see cref="Result.Connected"/> exactly as a spawn and a wool do (the
/// author's ruling). The goal itself floats a few blocks above its terrain by design, so the point judged is
/// the nearest navigable ground around it rather than its own column — what the gate refuses is a goal whose
/// approach ground the spawns cannot reach, which is a match nobody can finish.</para>
///
/// <para><b>Protection regions are checked per team.</b> One navigability map cannot see the one way a small
/// floating goal genuinely becomes unreachable: an <c>enter</c> rule barring the attacking team from the
/// ground its approach crosses — a goal tucked behind an oversized spawn protection. So where the map's apply
/// rules deny a team entry somewhere, that team gets its own navigable set with the denied cells removed, and
/// every goal that team must contest (the other teams' wools, destroyables and cores) has to share a component
/// with that team's own spawn over it. A defender barred from its own wool room is by design and is never
/// required to reach it; a filter that cannot be resolved to a team denies nobody, so an exotic wiring can
/// only ever under-refuse, never invent a blockage.</para>
/// </summary>
public static class Traversability
{
    public sealed record NavPoint(string Kind, string Name, int X, int Z, int Component);
    /// <summary>A gating point the verdict could not connect. <see cref="For"/> names the team whose own
    /// navigable set cut it off, where the cause is a per-team entry denial — null where the whole map's
    /// navigability already fails to reach it, whoever walks.</summary>
    public sealed record IsolatedPoint(string Kind, string Name, string? For = null);
    public sealed record Result(bool Connected, int ComponentCount, string Severity, string Message,
        bool HaveLayers, List<NavPoint> Points, List<IsolatedPoint> Isolated);

    /// <summary>The grid every playability read shares: its box, and which cells a player can cross —
    /// buildable or bridgeable by the map's rules, or standing terrain. One derivation, so the traversability
    /// verdict and the coverage read cannot disagree about what ground is.</summary>
    internal sealed record NavigableGround(int MinX, int MinZ, int Nx, int Nz, bool[] Cells)
    {
        public int Length => Cells.Length;
        public HashSet<(int X, int Z)> Set()
        {
            var cells = new HashSet<(int X, int Z)>();
            for (var i = 0; i < Cells.Length; i++)
                if (Cells[i]) cells.Add((MinX + i % Nx, MinZ + i / Nx));
            return cells;
        }
    }

    /// <summary>Compute the shared navigable grid. Sized to the walkable terrain, not just the region AABB:
    /// objectives on terrain past the build regions would otherwise fall outside it.</summary>
    internal static NavigableGround Ground(Dict data, HashSet<(int, int)>? surfaceColumns,
        HashSet<(int, int)>? y0Columns, (int, int, int, int)? bbox, int margin)
    {
        var grid = bbox ?? TerrainInclusiveBbox(data, surfaceColumns, y0Columns, margin);
        var b = Buildability.Compute(data, y0Columns, grid, margin);
        int nx = b.Width, nz = b.Height, minX = b.MinX, minZ = b.MinZ, n = nx * nz;

        // Navigable ground is what a player can stand on: terrain, plus the build zone they may bridge across.
        // A cell is only bridgeable if the map GRANTS building there — some apply rule's region covers it and
        // does not deny it. Reading an ungoverned cell as buildable made every cell outside every rule
        // walkable, so a board could pass "all objectives connected" over void nobody can cross (B247).
        var navigable = new bool[n];
        for (var i = 0; i < n; i++)
            navigable[i] = b.Governed[i] && (b.Verdict[i] == 0 || b.Verdict[i] == 3);   // bridgeable
        if (surfaceColumns is not null)
            foreach (var (x, z) in surfaceColumns)
            {
                int ix = x - minX, iz = z - minZ;
                if (ix >= 0 && ix < nx && iz >= 0 && iz < nz) navigable[iz * nx + ix] = true;  // walkable surface
            }
        return new NavigableGround(minX, minZ, nx, nz, navigable);
    }

    public static Result Check(Dict data, HashSet<(int, int)>? surfaceColumns, HashSet<(int, int)>? y0Columns,
        (int, int, int, int)? bbox = null, int margin = 16)
    {
        var ground = Ground(data, surfaceColumns, y0Columns, bbox, margin);
        var (minX, minZ, nx, nz, navigable) = (ground.MinX, ground.MinZ, ground.Nx, ground.Nz, ground.Cells);
        var n = nx * nz;
        var haveLayers = surfaceColumns is { Count: > 0 };

        var labels = LabelComponents(navigable, nx, nz);
        var navigableCells = new HashSet<(int X, int Z)>();
        for (var i = 0; i < n; i++) if (navigable[i]) navigableCells.Add((i % nx, i / nx));
        var owned = NavigationPoints(data, (minX, minZ, minX + nx, minZ + nz));

        var placed = new List<NavPoint>();
        foreach (var (p, _) in owned)
        {
            int ix = p.X - minX, iz = p.Z - minZ;
            var comp = (ix >= 0 && ix < nx && iz >= 0 && iz < nz) ? LabelAt(labels, navigableCells, nx, ix, iz) : 0;
            placed.Add(p with { Component = comp });
        }

        // Every goal gates the export refusal, destroyables and cores included (the author's ruling). The
        // goal itself floats a few blocks above the terrain by design, so what is judged is not its own
        // column but the ground around it — the snap below reads the nearest navigable cell — and a goal
        // whose approach ground is cut off from the spawns is a match nobody can finish, exactly as an
        // unreachable wool is.
        var gating = placed.Where(p => p.Kind is "spawn" or "wool" or "destroyable" or "core").ToList();
        var comps = gating.Where(p => p.Component > 0).Select(p => p.Component).ToList();
        var distinct = comps.ToHashSet();
        // most-common component; ties broken by first appearance in `comps` (matches Counter.most_common)
        var main = 0;
        if (comps.Count > 0)
        {
            var counts = comps.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var maxCount = counts.Values.Max();
            main = comps.First(c => counts[c] == maxCount);
        }
        // A point off the navigable grid entirely (Component == 0) is isolated whatever `main` came out to —
        // including the degenerate case where every gating point is off-grid, so `main` itself stays 0 and a
        // check against `!= main` alone would call zero points isolated despite none of them being reachable.
        var isolated = gating.Where(p => p.Component == 0 || p.Component != main)
            .Select(p => new IsolatedPoint(p.Kind, p.Name)).ToList();
        var connected = distinct.Count <= 1 && !gating.Any(p => p.Component == 0);

        // The per-team half: where an enter rule bars a team somewhere, that team walks its own map. Only run
        // when the whole-map chain holds — a globally isolated point is already named above, and naming it
        // again per team would report one cause twice.
        if (connected)
            foreach (var barred in TeamIsolations(data, owned, navigable, minX, minZ, nx, nz))
            {
                isolated.Add(barred);
                connected = false;
            }

        var severity = connected ? "ok" : "warning";
        var message = connected
            ? "spawn ↔ objective chain is traversable"
            : isolated.Any(p => p.For is not null)
                ? $"{isolated.Count} objective(s) sit behind ground an enter rule bars the attacking team from — check the protection regions"
                : comps.Count == 0
                    ? "no spawn or objective point is on navigable ground — check build regions / bridgeable gaps"
                    : $"{isolated.Count} spawn/objective point(s) are not reachable from the rest — check build regions / bridgeable gaps";
        return new Result(connected, distinct.Count, severity, message, haveLayers, placed, isolated);
    }

    // The buildability/navigability grid must span every walkable column, not just the region AABB — union
    // the region box with the surface + Y=0 terrain extents so objectives out on that terrain stay in-grid.
    private static (int, int, int, int) TerrainInclusiveBbox(Dict data, HashSet<(int, int)>? surfaceColumns,
        HashSet<(int, int)>? y0Columns, int margin)
    {
        var (minX, minZ, maxX, maxZ) = Buildability.RegionBbox(data, margin);
        foreach (var cols in (ReadOnlySpan<HashSet<(int, int)>?>)[surfaceColumns, y0Columns])
            if (cols is { Count: > 0 })
            {
                minX = Math.Min(minX, cols.Min(c => c.Item1) - margin);
                minZ = Math.Min(minZ, cols.Min(c => c.Item2) - margin);
                maxX = Math.Max(maxX, cols.Max(c => c.Item1) + margin);
                maxZ = Math.Max(maxZ, cols.Max(c => c.Item2) + margin);
            }
        return (minX, minZ, maxX, maxZ);
    }

    /// <summary>A navigation point with the team it belongs to — a spawn's own team, a goal's defending
    /// owner. The owner is what the per-team pass asks (an attacker contests every goal it does not own);
    /// the public <see cref="NavPoint"/> stays team-less because the whole-map verdict is.</summary>
    internal sealed record OwnedPoint(NavPoint Point, string Owner);

    // ── navigation points: spawn region centres, wool locations, destroyable/core region centres ─────────
    internal static List<OwnedPoint> NavigationPoints(Dict data, (double, double, double, double) bounds)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var pts = new List<OwnedPoint>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            var r = sp.GetValueOrDefault("region");
            var region = r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
            var team = sp.GetValueOrDefault("team") as string ?? "";
            if (RegionCentre(region, regions, bounds) is { } c)
                pts.Add(new OwnedPoint(new NavPoint("spawn", team, c.x, c.z, 0), team));
        }
        foreach (var w in AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = w.GetValueOrDefault("color") as string ?? "";
            var owner = w.GetValueOrDefault("team") as string ?? "";
            var loc = AsDict(w.GetValueOrDefault("location"));
            if (Num(loc.GetValueOrDefault("x")) is { } lx && Num(loc.GetValueOrDefault("z")) is { } lz)
                pts.Add(new OwnedPoint(new NavPoint("wool", color, (int)lx, (int)lz, 0), owner));
            else if (RegionCentre(regions.GetValueOrDefault(w.GetValueOrDefault("wool_room_region") as string ?? "") as Dict, regions, bounds) is { } c)
                pts.Add(new OwnedPoint(new NavPoint("wool", color, c.x, c.z, 0), owner));
        }
        foreach (var d in AsList(data.GetValueOrDefault("destroyables")).OfType<Dict>())
        {
            if (d.GetValueOrDefault("show") is false) continue;   // not an objective — see Destroyable.IsObjective
            var r = d.GetValueOrDefault("region");
            var region = r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
            var owner = d.GetValueOrDefault("owner") as string ?? "";
            if (RegionCentre(region, regions, bounds) is { } c)
                pts.Add(new OwnedPoint(
                    new NavPoint("destroyable", d.GetValueOrDefault("name") as string ?? owner, c.x, c.z, 0), owner));
        }
        foreach (var core in AsList(data.GetValueOrDefault("cores")).OfType<Dict>())
        {
            var r = core.GetValueOrDefault("region");
            var region = r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
            var owner = core.GetValueOrDefault("owner") as string ?? "";
            if (RegionCentre(region, regions, bounds) is { } c)
                pts.Add(new OwnedPoint(new NavPoint("core", owner, c.x, c.z, 0), owner));
        }
        return pts;
    }

    /// <summary>Every goal a team is barred from reaching by an <c>enter</c> denial, across all teams. Each
    /// team whose entry an apply rule denies somewhere walks the navigable set minus its denied cells, from
    /// its own spawns to every goal it does not own — the defender is never required to reach its own wool,
    /// which its room's own rule bars by design.</summary>
    private static IEnumerable<IsolatedPoint> TeamIsolations(
        Dict data, List<OwnedPoint> owned, bool[] navigable, int minX, int minZ, int nx, int nz)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var filters = AsDict(data.GetValueOrDefault("filters"));
        var bounds = ((double)minX, (double)minZ, (double)(minX + nx), (double)(minZ + nz));
        var teams = owned.Where(p => p.Point.Kind == "spawn" && p.Owner.Length > 0)
            .Select(p => p.Owner).Distinct().ToList();
        if (teams.Count == 0) yield break;

        // Each team's denied cells, unioned over every enter rule that resolves to barring it. A rule with no
        // region or no readable geometry denies nothing — an entry denial has to be provable to subtract.
        var denials = new Dictionary<string, bool[]>();
        foreach (var rule in AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>())
        {
            if (rule.GetValueOrDefault("enter") is not string enter || enter.Length == 0) continue;
            if (rule.GetValueOrDefault("region") is not { } regionRef) continue;
            bool[]? mask = null;
            foreach (var team in teams)
            {
                if (AllowsTeam(enter, filters, team)) continue;
                mask ??= Buildability.RegionMask(regionRef, regions, bounds, minX, minZ, nx, nz);
                if (mask is null) break;
                if (!denials.TryGetValue(team, out var denied)) denials[team] = denied = new bool[nx * nz];
                for (var i = 0; i < mask.Length; i++) denied[i] |= mask[i];
            }
        }

        foreach (var team in teams)
        {
            if (!denials.TryGetValue(team, out var denied)) continue;

            var walkable = new bool[navigable.Length];
            for (var i = 0; i < navigable.Length; i++) walkable[i] = navigable[i] && !denied[i];
            var labels = LabelComponents(walkable, nx, nz);
            var cells = new HashSet<(int X, int Z)>();
            for (var i = 0; i < walkable.Length; i++) if (walkable[i]) cells.Add((i % nx, i / nx));

            int ComponentOf(NavPoint point)
            {
                int ix = point.X - minX, iz = point.Z - minZ;
                return ix >= 0 && ix < nx && iz >= 0 && iz < nz ? LabelAt(labels, cells, nx, ix, iz) : 0;
            }

            var spawnComponents = owned
                .Where(p => p.Point.Kind == "spawn" && p.Owner == team)
                .Select(p => ComponentOf(p.Point))
                .Where(component => component > 0)
                .ToHashSet();

            foreach (var (point, ownerTeam) in owned)
            {
                if (point.Kind == "spawn" || ownerTeam == team) continue;
                var component = ComponentOf(point);
                if (component == 0 || !spawnComponents.Contains(component))
                    yield return new IsolatedPoint(point.Kind, point.Name, For: team);
            }
        }
    }

    /// <summary>Whether an <c>enter</c> filter lets <paramref name="team"/> in. Deliberately permissive: a
    /// team filter answers by its team, the boolean wrappers compose, and anything unresolvable answers yes —
    /// so a wiring this reader does not understand can only fail to subtract ground, never invent a barred
    /// region that is not there.</summary>
    private static bool AllowsTeam(string value, Dict filters, string team, HashSet<string>? seen = null)
    {
        seen ??= [];
        if (value.Length == 0 || !seen.Add(value)) return true;
        if (value is "always" or "allow") return true;
        if (value == "never") return false;
        if (filters.GetValueOrDefault(value) is not Dict filter) return true;

        return (filter.GetValueOrDefault("type") as string) switch
        {
            "team" => filter.GetValueOrDefault("team") as string == team,
            "not" => !AllowsTeam(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "allow" => AllowsTeam(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "deny" => !AllowsTeam(filter.GetValueOrDefault("child") as string ?? "", filters, team, seen),
            "any" => AsList(filter.GetValueOrDefault("children"))
                .Any(child => AllowsTeam(child as string ?? "", filters, team, seen)),
            "all" => AsList(filter.GetValueOrDefault("children"))
                .All(child => AllowsTeam(child as string ?? "", filters, team, seen)),
            "always" => true,
            "never" => false,
            _ => true,
        };
    }

    // A point that lies inside the region footprint. The area centroid is the natural centre and
    // equals the bounding-box midpoint for the convex rect/disc footprints; only when it falls
    // outside a non-convex or disjoint shape (union/complement/half — where the box midpoint can
    // land in an uncovered gap) do we use a guaranteed-interior representative point. Falls back to
    // the AABB midpoint when no footprint geometry resolves.
    private static (int x, int z)? RegionCentre(Dict? region, Dict registry, (double, double, double, double) bounds)
    {
        if (region is null) return null;
        if (RegionGeometry2d.ToGeometry(region, bounds, registry) is { IsEmpty: false } geom)
        {
            var centroid = geom.Centroid;
            var p = geom.Contains(centroid) ? centroid : geom.InteriorPoint;
            return ((int)p.X, (int)p.Y);
        }
        return BoundsMidpoint(region);
    }

    private static (int x, int z)? BoundsMidpoint(Dict region)
    {
        var b = AsDict(region.GetValueOrDefault("bounds_2d"));
        if (b.Count == 0) return null;
        var mn = AsDict(b.GetValueOrDefault("min"));
        var mx = AsDict(b.GetValueOrDefault("max"));
        if (Num(mn.GetValueOrDefault("x")) is not { } mnx || Num(mn.GetValueOrDefault("z")) is not { } mnz
            || Num(mx.GetValueOrDefault("x")) is not { } mxx || Num(mx.GetValueOrDefault("z")) is not { } mxz)
            return null;
        return ((int)((mnx + mxx) / 2), (int)((mnz + mxz) / 2));
    }

    // A cell→component-id grid over the navigable cells: 1-based ids for navigable cells (each connected
    // component one id), 0 for non-navigable. Only the partition matters here — which cells share a component —
    // so any consistent numbering serves.
    private static int[] LabelComponents(bool[] navigable, int nx, int nz)
    {
        var navCells = new List<(int X, int Z)>();
        for (var i = 0; i < navigable.Length; i++)
            if (navigable[i]) navCells.Add((i % nx, i / nx));

        var labels = new int[nx * nz];
        var components = GridComponents.Label(navCells, connectivity: 4);
        for (var c = 0; c < components.Count; c++)
            foreach (var (x, z) in components[c]) labels[z * nx + x] = c + 1;
        return labels;
    }

    // The component id of the nearest navigable cell within a small radius — an objective point's own cell
    // can land off the navigable grid (a wool marker a block into a wall), so the snap finds what it opens onto.
    private static int LabelAt(int[] labels, IReadOnlySet<(int X, int Z)> navigableCells, int nx, int ix, int iz, int snap = 3) =>
        Cells.SnapToWalkable((ix, iz), navigableCells, snap) is { } cell ? labels[cell.Z * nx + cell.X] : 0;

    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
