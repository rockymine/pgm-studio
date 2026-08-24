
namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;
using PgmStudio.Geom;
using PgmStudio.Analysis.Scan;
using PgmStudio.Geom.Algorithms;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Objective-chain traversability: is the spawn↔wool chain connected over the ground a walk runs on? The
/// places are split into components by <see cref="Walk.Components"/> — the flood over the same edges a
/// distance is solved across — and every objective point is seated on the storey it names and read for the
/// component it lands in; the chain is traversable when they all share one.
///
/// <para>It is the <b>walk's</b> ground and not a second reading of the same board, which is what keeps this
/// verdict and a measured distance from disagreeing about whether there is a way. A column with two standable
/// surfaces is two places here as it is there, so a deck and the gallery under it are one component only
/// where something joins them.</para>
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
/// with that team's own spawn over it. A filter that cannot be resolved to a team denies nobody, so an exotic
/// wiring can only ever under-refuse, never invent a blockage.</para>
///
/// <para><b>A team's own goal is asked for the border rather than the goal.</b> A wool room bars its defender
/// by design — that rule is what makes defending it mean anything — so standing on the wool is a journey the
/// defender never makes and cannot be what the verdict asks of it. What it must still be able to do is walk
/// up to the room: the ground it may stand on has to reach the barred patch the goal stands in, which is the
/// protection's own border. The patch is the goal's 4-connected area of that team's denied cells, so a second
/// protection on the way is a wall and not a way round, and a team cut off from its own wool room by terrain
/// is refused exactly as an attacker cut off from the wool is.</para>
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

    /// <param name="declared">Goals the document cannot carry — see <see cref="NavPoints.Of"/>. Absent, the
    /// verdict is over what the document states, which on a map whose goals are not placed yet is its spawns
    /// and nothing else.</param>
    public static Result Check(Dict data, SegmentIndex? segments,
        (int, int, int, int)? bbox = null, int margin = 16, IReadOnlyList<NavPoint>? declared = null)
    {
        var ground = WorldWalk.Ground(data, segments, margin, bbox);
        var box = ground.Bounds;
        var haveLayers = ground.Ground.Count > 0;

        var components = Walk.Components(ground);
        var owned = NavPoints.Of(data, (box.X, box.Z, box.MaxX, box.MaxZ), declared);
        var placed = owned.Select(point => new Landing(point, ComponentOf(point, ground, components))).ToList();

        // Every goal gates the export refusal, destroyables and cores included (the author's ruling). The
        // goal itself floats a few blocks above the terrain by design, so what is judged is not its own
        // column but the ground around it — the seat below reads the nearest place a player stands on — and a
        // goal whose approach ground is cut off from the spawns is a match nobody can finish, exactly as an
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
        // A point off the ground entirely (Component == 0) is isolated whatever `main` came out to —
        // including the degenerate case where every gating point is off it, so `main` itself stays 0 and a
        // check against `!= main` alone would call zero points isolated despite none of them being reachable.
        var isolated = gating.Where(p => p.Component == 0 || p.Component != main)
            .Select(p => new IsolatedPoint(p.Point.Kind, p.Point.Name)).ToList();
        var connected = distinct.Count <= 1 && !gating.Any(p => p.Component == 0);

        // The per-team half: where an enter rule bars a team somewhere, that team walks its own map. Only run
        // when the whole-map chain holds — a globally isolated point is already named above, and naming it
        // again per team would report one cause twice.
        if (connected)
            foreach (var barred in TeamIsolations(data, owned, ground))
            {
                isolated.Add(barred);
                connected = false;
            }

        var severity = connected ? "ok" : "warning";
        var message = connected
            ? "spawn ↔ objective chain is traversable"
            : isolated.Any(p => p.For is not null)
                ? $"{isolated.Count} objective(s) a team cannot walk to over the ground an enter rule leaves it — check the protection regions"
                : comps.Count == 0
                    ? "no spawn or objective point is on navigable ground — check build regions / bridgeable gaps"
                    : $"{isolated.Count} spawn/objective point(s) are not reachable from the rest — check build regions / bridgeable gaps";
        return new Result(connected, distinct.Count, severity, message, haveLayers, placed, isolated);
    }

    /// <summary>The component a point lands in, or 0 where nothing near it is standable. A marker's own cell
    /// can be off the ground — a wool a block into a wall, a goal floating over its terrain — so the cell is
    /// snapped within <see cref="SnapRadius"/> first and the point is then seated on the storey it names.
    /// </summary>
    private static int ComponentOf(NavPoint point, WalkGround ground,
        IReadOnlyDictionary<WalkPlace, int> components)
        => Cells.SnapToWalkable(point.Cell, ground.Footprint, SnapRadius) is { } cell
            && (point with { X = cell.X, Z = cell.Z }).Seat(ground) is { } place
            ? components.GetValueOrDefault(place)
            : 0;

    /// <summary>How far a marker's cell may be off the ground and still be read as standing on it.</summary>
    private const int SnapRadius = 3;

    /// <summary>Every goal a team cannot get to over the ground an <c>enter</c> denial leaves it, across all
    /// teams. Each team an apply rule denies somewhere walks the navigable set minus its denied cells, from
    /// its own spawns to every goal — a goal it must contest has to be stood on, and its own has to be
    /// walked up to.</summary>
    private static IEnumerable<IsolatedPoint> TeamIsolations(Dict data, List<NavPoint> owned, WalkGround shared)
    {
        var over = shared.Bounds;
        if (over.Width <= 0 || over.Height <= 0) yield break;

        var teams = owned.Where(point => point.Kind == "spawn" && point.Owner.Length > 0)
            .Select(point => point.Owner).Distinct().ToList();

        foreach (var team in teams)
        {
            // The team's own walk, narrowed the same way a measured distance for that team is — one rule for
            // what an enter denial takes away, whether the question is "is there a way" or "how far".
            if (EntryDenials.Cells(data, team, over) is not { Count: > 0 } denied) continue;

            var ground = WorldWalk.Without(shared, denied);
            var components = Walk.Components(ground);
            var spawns = owned.Where(point => point.Kind == "spawn" && point.Owner == team).ToList();
            var spawnComponents = spawns
                .Select(point => ComponentOf(point, ground, components))
                .Where(component => component > 0)
                .ToHashSet();

            foreach (var point in owned)
            {
                if (point.Kind == "spawn") continue;
                var component = ComponentOf(point, ground, components);
                if (component > 0 && spawnComponents.Contains(component)) continue;
                if (point.Owner == team && Approaches(point, shared, denied, spawns)) continue;
                yield return new IsolatedPoint(point.Kind, point.Name, For: team);
            }
        }
    }

    /// <summary>Whether a team can walk up to a goal of its own it may not stand on: the same journey from its
    /// own spawns, over its own ground plus the one barred patch the goal stands in. Reaching that patch is
    /// reaching the protection's border, which is as far as a defender of a wool room ever goes and is
    /// therefore the whole of what the goal asks of its own team. The patch is the goal's 4-connected area of
    /// this team's denied cells and nothing else is given back, so a route that would have to cross a second
    /// protection is still cut. False where nothing bars the team at the goal at all — the goal is then simply
    /// out of reach, whoever walks.</summary>
    private static bool Approaches(NavPoint goal, WalkGround shared, IReadOnlySet<(int X, int Z)> denied,
        List<NavPoint> spawns)
    {
        var patch = Cells.Flood([goal.Cell], denied);
        if (patch.Count == 0) return false;

        var elsewhere = new HashSet<(int X, int Z)>(denied.Where(cell => !patch.Contains(cell)));
        var upTo = WorldWalk.Without(shared, elsewhere);
        var components = Walk.Components(upTo);
        var border = ComponentOf(goal, upTo, components);
        return border > 0 && spawns.Any(spawn => ComponentOf(spawn, upTo, components) == border);
    }
}
