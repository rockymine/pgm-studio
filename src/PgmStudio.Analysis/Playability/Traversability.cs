namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;
using PgmStudio.Geom.Algorithms;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Objective-chain traversability: is the spawn↔wool chain connected over the navigability map (walkable
/// surface ∪ bridgeable buildable)? The navigable cells are split into 4-connected components
/// (<see cref="GridComponents"/>) and every objective point is snapped to the component it sits in; the chain
/// is traversable when they all share one.
///
/// <para>A destroyable and a core are read as navigation points too, and every <see cref="NavPoint"/> list
/// this returns carries them — but they do not gate <see cref="Result.Connected"/>. A destroyable is broken
/// from range and floats a few blocks above its terrain by design (the same design PGM has always had), so
/// "reachable the way a wool is" is not a question this checker answers for one; it is reported so a caller
/// can see whether a destroyable's own footprint sits on navigable ground, without the map being refused for
/// it.</para>
/// </summary>
public static class Traversability
{
    public sealed record NavPoint(string Kind, string Name, int X, int Z, int Component);
    public sealed record IsolatedPoint(string Kind, string Name);
    public sealed record Result(bool Connected, int ComponentCount, string Severity, string Message,
        bool HaveLayers, List<NavPoint> Points, List<IsolatedPoint> Isolated);

    public static Result Check(Dict data, HashSet<(int, int)>? surfaceColumns, HashSet<(int, int)>? y0Columns,
        (int, int, int, int)? bbox = null, int margin = 16)
    {
        // Size the grid to the walkable terrain, not just the region AABB. Objectives on terrain that
        // extends past the build regions (a wool far out on an island) would otherwise fall outside the
        // grid and read as isolated regardless of how the terrain/build layer actually connects them.
        var grid = bbox ?? TerrainInclusiveBbox(data, surfaceColumns, y0Columns, margin);
        var b = Buildability.Compute(data, y0Columns, grid, margin);
        int nx = b.Width, nz = b.Height, minX = b.MinX, minZ = b.MinZ, n = nx * nz;

        var navigable = new bool[n];
        for (var i = 0; i < n; i++) navigable[i] = b.Verdict[i] == 0 || b.Verdict[i] == 3;   // bridgeable
        if (surfaceColumns is not null)
            foreach (var (x, z) in surfaceColumns)
            {
                int ix = x - minX, iz = z - minZ;
                if (ix >= 0 && ix < nx && iz >= 0 && iz < nz) navigable[iz * nx + ix] = true;  // walkable surface
            }
        var haveLayers = surfaceColumns is { Count: > 0 };

        var labels = LabelComponents(navigable, nx, nz);
        var points = NavigationPoints(data, (b.MinX, b.MinZ, b.MaxX, b.MaxZ));

        var placed = new List<NavPoint>();
        foreach (var p in points)
        {
            int ix = p.X - minX, iz = p.Z - minZ;
            var comp = (ix >= 0 && ix < nx && iz >= 0 && iz < nz) ? LabelAt(labels, navigable, nx, nz, ix, iz) : 0;
            placed.Add(p with { Component = comp });
        }

        // Only spawns and wools gate the export refusal. A destroyable or core floats a few blocks above the
        // terrain by design and is broken from range rather than walked to, so its reachability is not the
        // same question a spawn/wool chain answers — it is reported here (in every Points list a caller
        // reads) but does not, on its own, turn a map traversable.
        var gating = placed.Where(p => p.Kind is "spawn" or "wool").ToList();
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

        var severity = connected ? "ok" : "warning";
        var message = connected
            ? "spawn ↔ wool objective chain is traversable"
            : comps.Count == 0
                ? "no spawn/wool point is on navigable ground — check build regions / bridgeable gaps"
                : $"{isolated.Count} spawn/wool point(s) are not reachable from the rest — check build regions / bridgeable gaps";
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

    // ── navigation points: spawn region centres, wool locations, and — reported but not gating,
    // see Check — destroyable/core region centres ─────────────────────────────────────
    private static List<NavPoint> NavigationPoints(Dict data, (double, double, double, double) bounds)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var pts = new List<NavPoint>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            var r = sp.GetValueOrDefault("region");
            var region = r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
            if (RegionCentre(region, regions, bounds) is { } c) pts.Add(new NavPoint("spawn", sp.GetValueOrDefault("team") as string ?? "", c.x, c.z, 0));
        }
        foreach (var w in AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = w.GetValueOrDefault("color") as string ?? "";
            var loc = AsDict(w.GetValueOrDefault("location"));
            if (Num(loc.GetValueOrDefault("x")) is { } lx && Num(loc.GetValueOrDefault("z")) is { } lz)
                pts.Add(new NavPoint("wool", color, (int)lx, (int)lz, 0));
            else if (RegionCentre(regions.GetValueOrDefault(w.GetValueOrDefault("wool_room_region") as string ?? "") as Dict, regions, bounds) is { } c)
                pts.Add(new NavPoint("wool", color, c.x, c.z, 0));
        }
        foreach (var d in AsList(data.GetValueOrDefault("destroyables")).OfType<Dict>())
        {
            if (d.GetValueOrDefault("show") is false) continue;   // not an objective — see Destroyable.IsObjective
            var r = d.GetValueOrDefault("region");
            var region = r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
            if (RegionCentre(region, regions, bounds) is { } c)
                pts.Add(new NavPoint("destroyable", d.GetValueOrDefault("name") as string ?? d.GetValueOrDefault("owner") as string ?? "", c.x, c.z, 0));
        }
        foreach (var core in AsList(data.GetValueOrDefault("cores")).OfType<Dict>())
        {
            var r = core.GetValueOrDefault("region");
            var region = r is string s ? regions.GetValueOrDefault(s) as Dict : r as Dict;
            if (RegionCentre(region, regions, bounds) is { } c)
                pts.Add(new NavPoint("core", core.GetValueOrDefault("owner") as string ?? "", c.x, c.z, 0));
        }
        return pts;
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

    private static int LabelAt(int[] labels, bool[] navigable, int nx, int nz, int ix, int iz, int snap = 3)
    {
        for (var r = 0; r <= snap; r++)
            for (var dz = -r; dz <= r; dz++)
                for (var dx = -r; dx <= r; dx++)
                {
                    int x = ix + dx, z = iz + dz;
                    if (x >= 0 && x < nx && z >= 0 && z < nz && navigable[z * nx + x]) return labels[z * nx + x];
                }
        return 0;
    }

    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
