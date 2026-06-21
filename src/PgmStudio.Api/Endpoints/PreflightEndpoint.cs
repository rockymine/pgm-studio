using FastEndpoints;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/preflight — the Review phase's pre-flight gate (new-map-authoring.md §9). Runs the
/// four generated-map checks and reports the export verdict:
/// <list type="number">
/// <item><b>Round-trip</b> + <b>Mirror</b> — pure codec/categorizer checks (<see cref="Preflight"/>).</item>
/// <item><b>Buildability</b> — every spawn / wool / monument placement must sit over solid ground (not
/// open void), reusing <see cref="Buildability"/>.</item>
/// <item><b>Traversability</b> — the spawn↔wool chain must be connected, reusing <see cref="Traversability"/>
/// — the same check <c>GET /xml</c> enforces as its 409 gate.</item>
/// </list>
/// <c>ExportReady</c> is true iff round-trip passes (else <c>GET /xml</c> would throw) and traversability is
/// connected (else it returns 409); mirror + buildability are advisory. Scoped to intent-authored maps —
/// a corpus map (no intent blob) has nothing to pre-flight and reports <c>IntentMap=false</c>.
/// </summary>
public sealed class PreflightEndpoint(MapRepository repo, MapReader reader, FeatureData feature, PgmDb db)
    : EndpointWithoutRequest<PreflightDto>
{
    private const byte Void = 2;   // Buildability verdict for an open-void column (no ground)

    public override void Configure() { Get("/map/{slug}/preflight"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var slug = map.Slug;
        var doc = await reader.ReadDocAsync(map, ct);

        if (!await IntentStore.HasAsync(db, map.Id, ct))
        {
            await Send.OkAsync(new PreflightDto(false, false, [], ["this map was not authored from intent — nothing to pre-flight"], null), ct);
            return;
        }
        var intent = await IntentStore.LoadAsync(db, map.Id, ct);

        var log = new List<string>
        {
            $"intent → generate: {ListCount(doc, "teams")} teams · {intent.Wools?.Count ?? 0} wools · " +
            $"{DictCount(doc, "regions")} regions · {DictCount(doc, "filters")} filters · {ListCount(doc, "apply_rules")} apply-rules",
        };

        var roundTrip = Preflight.RoundTrip(doc);
        var mirror = Preflight.Mirror(doc, intent);

        var segs = await feature.SegmentsAsync(map.Id, ct);
        var build = await BuildabilityCheckAsync(map.Id, doc, intent, segs?.Y0Columns(), ct);

        var trav = Traversability.Check(doc, segs?.SurfaceColumns(), segs?.Y0Columns());
        var travCheck = TraversabilityCheck(trav);

        foreach (var c in new[] { roundTrip, mirror, build, travCheck })
            log.Add($"{c.Label.ToLowerInvariant()}: {c.Detail}");

        var exportReady = roundTrip.Status == "pass" && trav.Connected;
        log.Add(exportReady
            ? $"export gate OPEN — GET /map/{slug}/xml → 200"
            : $"export gate BLOCKED — GET /map/{slug}/xml → {(roundTrip.Status != "pass" ? "HTTP 500 (codec)" : "HTTP 409 (traversability)")}");

        var travDto = new TraversabilityDto(
            trav.Connected, trav.ComponentCount, trav.Severity, trav.Message, trav.HaveLayers,
            trav.Points.Select(p => new NavPointDto(p.Kind, p.Name, p.X, p.Z, p.Component)).ToList(),
            trav.Isolated.Select(i => new IsolatedPointDto(i.Kind, i.Name)).ToList());

        await Send.OkAsync(new PreflightDto(
            true, exportReady,
            new[] { roundTrip, mirror, build, travCheck }.Select(c => new PreflightCheckDto(c.Key, c.Label, c.Status, c.Detail)).ToList(),
            log, travDto), ct);
    }

    // Every authored placement (spawn / wool source / monument) must sit over solid ground, not open void.
    // Reuses the per-column buildability grid (void = no ground under the cell). Skips when there's no
    // Y=0 layer — void can't be told from solid without it (xml-only / un-scanned map).
    private async Task<Preflight.Check> BuildabilityCheckAsync(long mapId, Dict doc, MapIntent intent, HashSet<(int, int)>? y0, CancellationToken ct)
    {
        var bb = (await feature.MapBboxAsync(mapId, ct))?.bounds;
        var res = Buildability.Compute(doc, y0,
            bb is { } v ? ((int)v.Item1, (int)v.Item2, (int)v.Item3, (int)v.Item4) : null);
        if (!res.HasY0)
            return new("buildability", "Buildability", "skip", "no Y=0 layer — can't verify ground under placements");

        var placements = new List<(string Label, double X, double Z)>();
        foreach (var s in intent.Spawns) placements.Add(($"{Team(s.Team)} spawn", s.Point.X, s.Point.Z));
        foreach (var w in intent.Wools ?? [])
        {
            placements.Add(($"{WoolName(w)} wool", w.Spawn.X, w.Spawn.Z));
            foreach (var m in w.Monuments) placements.Add(($"{WoolName(w)} monument", m.Location.X, m.Location.Z));
        }

        // Only an explicit void verdict fails (off-grid placements are outside the analysed box — left to
        // the connectivity check rather than flagged here, to avoid edge-rounding false positives).
        var overVoid = placements.Where(p => CellVerdict(res, p.X, p.Z) == Void).Select(p => p.Label).ToList();
        if (overVoid.Count == 0)
            return new("buildability", "Buildability", "pass", $"all {placements.Count} spawn / wool / monument placements on solid ground");
        return new("buildability", "Buildability", "fail",
            $"{overVoid.Count} placement(s) over open void: {string.Join(", ", overVoid.Take(6))} — add a bridge in Build");

        static string Team(string id) => string.IsNullOrWhiteSpace(id) ? "team" : id;
        static string WoolName(WoolIntent w) => string.IsNullOrWhiteSpace(w.Color) ? w.Owner : w.Color;
    }

    private static byte? CellVerdict(Buildability.Result res, double x, double z)
    {
        int ix = (int)Math.Floor(x) - res.MinX, iz = (int)Math.Floor(z) - res.MinZ;
        if (ix < 0 || iz < 0 || ix >= res.Width || iz >= res.Height) return null;
        return res.Verdict[iz * res.Width + ix];
    }

    private static Preflight.Check TraversabilityCheck(Traversability.Result trav)
    {
        if (trav.Connected)
            return new("traversability", "Traversability", "pass",
                trav.HaveLayers ? "spawn ↔ wool chain connected across the build geometry" : "spawn ↔ wool chain connected (region centres)");
        var isolated = string.Join(" · ", trav.Isolated.Select(i => i.Name).Take(6));
        return new("traversability", "Traversability", "fail",
            isolated.Length > 0 ? $"not connected — isolated: {isolated}. Add a bridge in Build" : trav.Message);
    }

    private static int ListCount(Dict doc, string key) => (doc.GetValueOrDefault(key) as List<object?>)?.Count ?? 0;
    private static int DictCount(Dict doc, string key) => (doc.GetValueOrDefault(key) as Dict)?.Count ?? 0;
}
