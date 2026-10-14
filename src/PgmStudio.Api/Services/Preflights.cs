using PgmStudio.Vocabulary;
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

        // The objectives the map carries, whichever they are, and nothing it does not.
        var objectives = new (int Count, string Noun)[]
        {
            (intent.Wools?.Count ?? 0, "wool"), (intent.Destroyables?.Count ?? 0, "monument"), (intent.Cores?.Count ?? 0, "core"),
            (intent.ControlPoints?.Count ?? 0, "capture point"), (intent.Flags?.Count ?? 0, "flag"),
            (intent.ScoreBoxes?.Count ?? 0, "score box"),
        }.Where(entry => entry.Count > 0).Select(entry => Wording.Count(entry.Count, entry.Noun, entry.Noun == "score box" ? "score boxes" : null));
        var log = new List<string>
        {
            $"generated: {string.Join(" · ", [Wording.Count(ListCount(doc, "teams"), "team"), .. objectives])}"
                + (intent.KillPoints > 0 ? " · kills score" : "")
                + $" · {DictCount(doc, "regions")} regions · {DictCount(doc, "filters")} filters · {ListCount(doc, "apply_rules")} apply-rules",
        };

        var roundTrip = Preflight.RoundTrip(doc);
        var mirror = Preflight.Mirror(doc, intent);

        var (zones, _) = await feature.ZonesAsync(map.Id, doc, ct);
        var build = BuildabilityCheck(zones, intent);
        // A map read from a world states its ground in the world itself, so all of it is ground a build zone was
        // meant to reach; a drawn map without a plan says nothing about which void is a crossing it asked for.
        var planned = await feature.PlannedGroundAsync(map.Id, ct)
            ?? (await artifacts.HasAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) ? null : EveryColumn(zones));
        var reach = (intent.Build ?? new BuildIntent()).ModeFor(intent) == BuildModes.Void
            ? BuildZoneReachCheck(zones, planned)
            : new Preflight.Check("buildzone", "Build zone reach", "skip", "players don't build over the void on this map");

        var trav = Traversability.Check(doc, await feature.WalkGroundAsync(map.Id, doc, ct: ct), DeclaredGoals.Of(doc, intent));
        var travCheck = TraversabilityCheck(trav, intent);
        var woolTeams = WoolTeamsCheck(intent, await IslandTeamLookup.OfAsync(feature, map.Id, intent, ct));

        foreach (var c in new[] { roundTrip, mirror, build, reach, travCheck, woolTeams })
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
            new[] { roundTrip, mirror, build, reach, travCheck, woolTeams }.Select(c => new PreflightCheckDto(c.Key, c.Label, c.Status, c.Detail)).ToList(),
            log, travDto);
    }

    // Each wool is defended by its owner and captured by every other team at a monument of its own, on the
    // islands of those teams (OB34, OB35). It does not gate the export: the map plays, just not as intended.
    private static Preflight.Check WoolTeamsCheck(MapIntent intent, Func<double, double, string?>? islandTeamAt)
    {
        if (intent.Wools is not { Count: > 0 }) return new("wool-teams", "Wool teams", "skip", "no wools");
        var findings = WoolTeams.Check(intent, islandTeamAt);
        return findings.Count == 0
            ? new("wool-teams", "Wool teams", "pass", "every wool is captured by each team but its defender"
                + (islandTeamAt is null ? "" : ", each on its own team's island"))
            : new("wool-teams", "Wool teams", "fail", string.Join("; ", findings.Select(finding => finding.Message)));
    }

    // Every authored placement (spawn / wool source / monument) must sit over solid ground, not open void.
    // Asks the Y=0 read directly — the same question PGM's own <void/> filter asks — rather than reading it
    // off an edit zone, which answers why a column is editable and not whether anything is under it. Skips
    // when there's no Y=0 layer: void can't be told from solid without it (xml-only / un-scanned map). Asked
    // only of a map built on over the void: on one where nobody builds, the void rule never fires, and a map
    // floating above y=0 stands every spawn "over the void" while every one of them is on solid floor.
    private static Preflight.Check BuildabilityCheck(Editability.Result res, MapIntent intent)
    {
        if ((intent.Build ?? new BuildIntent()).ModeFor(intent) != BuildModes.Void)
            return new("buildability", "Buildability", "skip", "players don't build over the void on this map");
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
    private static HashSet<(int X, int Z)> EveryColumn(Editability.Result zones) =>
        [.. Enumerable.Range(0, zones.Height).SelectMany(iz => Enumerable.Range(0, zones.Width)
            .Select(ix => (zones.MinX + ix, zones.MinZ + iz)))];

    private static Preflight.Check BuildZoneReachCheck(Editability.Result zones, HashSet<(int X, int Z)>? planned)
    {
        if (!zones.HasY0)
            return new("buildzone", "Build zone reach", "skip", "no Y=0 layer — can't tell void from ground");
        if (planned is null)
            return new("buildzone", "Build zone reach", "skip", "a drawn map with no plan — nothing says where the ground met a build zone");
        var gaps = BuildZoneGap.Check(zones, planned);
        if (gaps.Count == 0)
            return new("buildzone", "Build zone reach", "pass", "every coast within reach of a build zone meets it");
        return new("buildzone", "Build zone reach", "fail",
            $"EZ2 — {string.Join(" · ", gaps.Select(gap => gap.Message))}");
    }

    // On a map built on over the void a cut-off place is reached by bridging to it; on one where nobody builds,
    // the place itself has to move, or the world has to join it.
    private static Preflight.Check TraversabilityCheck(Traversability.Result trav, MapIntent intent)
    {
        if (trav.Connected)
            return new("traversability", "Traversability", "pass",
                trav.HaveLayers ? "Every spawn and objective can be reached on foot." : "Every spawn and objective is connected (region centres).");
        var isolated = string.Join(", ", trav.Isolated.Select(i => i.For is { } team ? $"{Named(i)} (for {team})" : Named(i)).Take(6));
        var builds = (intent.Build ?? new BuildIntent()).ModeFor(intent) == BuildModes.Void;
        return new("traversability", "Traversability", "fail",
            isolated.Length == 0 ? trav.Message
            : $"Can't be reached on foot: {isolated}. " + (builds ? "Add a bridge in Build." : "Move it, or join it in the world and scan again."));

        static string Named(Traversability.IsolatedPoint point) => point.Kind switch
        {
            "spawn" => $"{point.Name} spawn",
            "wool" => $"{point.Name} wool",
            "flag" => $"{point.Name} post",
            _ => point.Name,
        };
    }

    private static int ListCount(Dict doc, string key) => (doc.GetValueOrDefault(key) as List<object?>)?.Count ?? 0;
    private static int DictCount(Dict doc, string key) => (doc.GetValueOrDefault(key) as Dict)?.Count ?? 0;
}
