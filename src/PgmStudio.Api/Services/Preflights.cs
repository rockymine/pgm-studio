using PgmStudio.Analysis.Playability;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>The Review phase's pre-flight gate over a stored map — the checks <c>GET /map/{slug}/preflight</c>
/// answers and the report reads, and the export verdict they come to.</summary>
public static class Preflights
{
    /// <summary>The pre-flight of <paramref name="map"/>: what each check found, the log a reader scans, and
    /// whether the export gate is open. A map not authored from intent has nothing to pre-flight and says
    /// so.</summary>
    public static async Task<PreflightDto> OfAsync(
        MapRow map, MapReader reader, FeatureData feature, MapArtifactStore artifacts, CancellationToken ct)
    {
        var slug = map.Slug;
        var doc = await reader.ReadDocAsync(map, ct);

        if (!await artifacts.HasAsync(map.Id, ArtifactKind.MapIntentJson, ct))
        {
            return new PreflightDto(false, false, [], ["this map was not authored from intent — nothing to pre-flight"], null);
        }
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);

        var log = new List<string>
        {
            $"intent → generate: {ListCount(doc, "teams")} teams · {intent.Wools?.Count ?? 0} wools · " +
            $"{DictCount(doc, "regions")} regions · {DictCount(doc, "filters")} filters · {ListCount(doc, "apply_rules")} apply-rules",
        };

        var roundTrip = Preflight.RoundTrip(doc);
        var mirror = Preflight.Mirror(doc, intent);

        var (zones, _) = await feature.ZonesAsync(map.Id, doc, ct);
        var build = BuildabilityCheck(zones, intent);
        var reach = BuildZoneReachCheck(zones, await feature.PlannedGroundAsync(map.Id, ct));

        var trav = Traversability.Check(doc, await feature.WalkGroundAsync(map.Id, doc, ct: ct), DeclaredGoals.Of(doc, intent));
        var travCheck = TraversabilityCheck(trav);

        foreach (var c in new[] { roundTrip, mirror, build, reach, travCheck })
            log.Add($"{c.Label.ToLowerInvariant()}: {c.Detail}");

        var exportReady = roundTrip.Status == "pass" && trav.Connected;
        log.Add(exportReady
            ? $"export gate OPEN — GET /map/{slug}/xml → 200"
            : $"export gate BLOCKED — GET /map/{slug}/xml → {(roundTrip.Status != "pass" ? "HTTP 500 (codec)" : "HTTP 409 (traversability)")}");

        var travDto = new TraversabilityDto(
            trav.Connected, trav.ComponentCount, trav.Severity, trav.Message, trav.HaveLayers,
            trav.Points.Select(p => new NavPointDto(p.Point.Kind, p.Point.Name, p.Point.X, p.Point.Z, p.Component)).ToList(),
            trav.Isolated.Select(i => new IsolatedPointDto(i.Kind, i.Name, i.For)).ToList());

        return new PreflightDto(
            true, exportReady,
            new[] { roundTrip, mirror, build, reach, travCheck }.Select(c => new PreflightCheckDto(c.Key, c.Label, c.Status, c.Detail)).ToList(),
            log, travDto);
    }

    // Every authored placement (spawn / wool source / monument) must sit over solid ground, not open void.
    // Asks the Y=0 read directly — the same question PGM's own <void/> filter asks — rather than reading it
    // off an edit zone, which answers why a column is editable and not whether anything is under it. Skips
    // when there's no Y=0 layer: void can't be told from solid without it (xml-only / un-scanned map).
    private static Preflight.Check BuildabilityCheck(Editability.Result res, MapIntent intent)
    {
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
        var overVoid = placements.Where(p => res.VoidAt(p.X, p.Z) == true).Select(p => p.Label).ToList();
        if (overVoid.Count == 0)
            return new("buildability", "Buildability", "pass", $"all {placements.Count} spawn / wool / monument placements on solid ground");
        return new("buildability", "Buildability", "fail",
            $"{overVoid.Count} placement(s) over open void: {string.Join(", ", overVoid.Take(6))} — add a bridge in Build");

        static string Team(string id) => string.IsNullOrWhiteSpace(id) ? "team" : id;
        static string WoolName(WoolIntent w) => string.IsNullOrWhiteSpace(w.Color) ? w.Owner : w.Color;
    }

    // Every coast the plan put against a build zone must still reach it: void between the two that nobody may
    // build across is a crossing in sight and out of reach (EZ2). A complaint, so the check fails without
    // closing the export gate.
    private static Preflight.Check BuildZoneReachCheck(Editability.Result zones, HashSet<(int X, int Z)>? planned)
    {
        if (!zones.HasY0)
            return new("buildzone", "Build zone reach", "skip", "no Y=0 layer — can't tell void from ground");
        if (planned is null)
            return new("buildzone", "Build zone reach", "skip", "no plan — nothing says where the ground met a build zone");
        var gaps = BuildZoneGap.Check(zones, planned);
        if (gaps.Count == 0)
            return new("buildzone", "Build zone reach", "pass", "every coast within reach of a build zone meets it");
        return new("buildzone", "Build zone reach", "fail",
            $"EZ2 — {string.Join(" · ", gaps.Select(gap => gap.Message))}");
    }

    private static Preflight.Check TraversabilityCheck(Traversability.Result trav)
    {
        if (trav.Connected)
            return new("traversability", "Traversability", "pass",
                trav.HaveLayers ? "spawn ↔ objective chain connected across the build geometry" : "spawn ↔ objective chain connected (region centres)");
        var isolated = string.Join(" · ", trav.Isolated.Select(i => i.For is { } team ? $"{i.Name} (for {team})" : i.Name).Take(6));
        return new("traversability", "Traversability", "fail",
            isolated.Length > 0 ? $"not connected — isolated: {isolated}. Add a bridge in Build" : trav.Message);
    }

    private static int ListCount(Dict doc, string key) => (doc.GetValueOrDefault(key) as List<object?>)?.Count ?? 0;
    private static int DictCount(Dict doc, string key) => (doc.GetValueOrDefault(key) as Dict)?.Count ?? 0;
}
