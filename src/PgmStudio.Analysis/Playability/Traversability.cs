
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
    /// <summary>A navigation point and the component it landed in — the annotation this verdict computes
    /// over a point the document states, rather than anything the point itself carries.</summary>
    public sealed record Landing(NavPoint Point, int Component);
    /// <summary>A gating point the verdict could not connect. <see cref="For"/> names the team whose own
    /// navigable set cut it off, where the cause is a per-team entry denial — null where the whole map's
    /// navigability already fails to reach it, whoever walks.</summary>
    public sealed record IsolatedPoint(string Kind, string Name, string? For = null);
    public sealed record Result(bool Connected, int ComponentCount, string Severity, string Message,
        bool HaveLayers, List<Landing> Points, List<IsolatedPoint> Isolated);

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

    /// <param name="declared">Goals the document cannot carry — see <see cref="NavPoints.Of"/>. Absent, the
    /// verdict is over what the document states, which on a map whose goals are not placed yet is its spawns
    /// and nothing else.</param>
    public static Result Check(Dict data, HashSet<(int, int)>? surfaceColumns, HashSet<(int, int)>? y0Columns,
        (int, int, int, int)? bbox = null, int margin = 16, IReadOnlyList<NavPoint>? declared = null)
    {
        var ground = Ground(data, surfaceColumns, y0Columns, bbox, margin);
        var (minX, minZ, nx, nz, navigable) = (ground.MinX, ground.MinZ, ground.Nx, ground.Nz, ground.Cells);
        var n = nx * nz;
        var haveLayers = surfaceColumns is { Count: > 0 };

        var labels = LabelComponents(navigable, nx, nz);
        var navigableCells = new HashSet<(int X, int Z)>();
        for (var i = 0; i < n; i++) if (navigable[i]) navigableCells.Add((i % nx, i / nx));
        var owned = NavPoints.Of(data, (minX, minZ, minX + nx, minZ + nz), declared);

        var placed = new List<Landing>();
        foreach (var point in owned)
        {
            int ix = point.X - minX, iz = point.Z - minZ;
            var comp = ix >= 0 && ix < nx && iz >= 0 && iz < nz ? LabelAt(labels, navigableCells, nx, ix, iz) : 0;
            placed.Add(new Landing(point, comp));
        }

        // Every goal gates the export refusal, destroyables and cores included (the author's ruling). The
        // goal itself floats a few blocks above the terrain by design, so what is judged is not its own
        // column but the ground around it — the snap below reads the nearest navigable cell — and a goal
        // whose approach ground is cut off from the spawns is a match nobody can finish, exactly as an
        // unreachable wool is.
        var gating = placed.Where(p => p.Point.Kind is "spawn" or "wool" or "destroyable" or "core").ToList();
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
            .Select(p => new IsolatedPoint(p.Point.Kind, p.Point.Name)).ToList();
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

    /// <summary>Every goal a team is barred from reaching by an <c>enter</c> denial, across all teams. Each
    /// team whose entry an apply rule denies somewhere walks the navigable set minus its denied cells, from
    /// its own spawns to every goal it does not own — the defender is never required to reach its own wool,
    /// which its room's own rule bars by design.</summary>
    private static IEnumerable<IsolatedPoint> TeamIsolations(
        Dict data, List<NavPoint> owned, bool[] navigable, int minX, int minZ, int nx, int nz)
    {
        var teams = owned.Where(point => point.Kind == "spawn" && point.Owner.Length > 0)
            .Select(point => point.Owner).Distinct().ToList();
        if (teams.Count == 0) yield break;

        var denials = EntryDenials.Masks(data, teams, minX, minZ, nx, nz);

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
                .Where(point => point.Kind == "spawn" && point.Owner == team)
                .Select(ComponentOf)
                .Where(component => component > 0)
                .ToHashSet();

            foreach (var point in owned)
            {
                if (point.Kind == "spawn" || point.Owner == team) continue;
                var component = ComponentOf(point);
                if (component == 0 || !spawnComponents.Contains(component))
                    yield return new IsolatedPoint(point.Kind, point.Name, For: team);
            }
        }
    }



    private static (int x, int z)? BoundsMidpoint(Dict region)
    {
        var b = MapDoc.AsDict(region.GetValueOrDefault("bounds_2d"));
        if (b.Count == 0) return null;
        var mn = MapDoc.AsDict(b.GetValueOrDefault("min"));
        var mx = MapDoc.AsDict(b.GetValueOrDefault("max"));
        if (MapDoc.Num(mn.GetValueOrDefault("x")) is not { } mnx || MapDoc.Num(mn.GetValueOrDefault("z")) is not { } mnz
            || MapDoc.Num(mx.GetValueOrDefault("x")) is not { } mxx || MapDoc.Num(mx.GetValueOrDefault("z")) is not { } mxz)
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

}
