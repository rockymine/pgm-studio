using System.Text.RegularExpressions;

namespace PgmStudio.Analysis;

using Dict = Dictionary<string, object?>;

/// <summary>
/// One region's derived categorisation: a gameplay <see cref="Category"/>, usage <see cref="Roles"/>,
/// and an optional <see cref="Subtype"/> refining the category (spec §2 — e.g. spawn → point|protection).
/// </summary>
public sealed record RegionFacet(string Category, List<string> Roles, string? Subtype = null);

/// <summary>
/// Two-facet region categorisation (port of studio/services/region_categorizer.py, contract
/// region-categorization.md). Operates on the map document dict (xml_data.json shape) so the
/// input is identical to Python's. Category = what a region *is*; roles = what it's *used for*.
/// </summary>
public static partial class RegionCategorizer
{
    private static readonly string[] RuleEvents = ["block", "block_break", "block_place", "enter"];
    private static readonly HashSet<string> CompoundTypes = ["union", "complement", "negative", "intersect", "mirror", "translate"];
    private static readonly HashSet<string> TimeFilterTypes = ["after", "time", "pulse"];

    private static readonly (string[] needles, string category)[] MessageRules =
    [
        (["wool room", "wool rooms", "woolroom", "woolrooms"], "wool_room"),
        (["spawner"], "mechanic"),
        (["spawn"], "spawn"),
        (["enemy's base", "enemies' base", "opponent's base", "enemy team's base"], "spawn"),
    ];

    // ── public API ──────────────────────────────────────────────────────────────────
    /// <summary>Flat {region_id: category} with region_categories overrides (contract §10).</summary>
    public static Dictionary<string, string> Categorize(Dict data)
    {
        var cats = DeriveFacets(data).ToDictionary(kv => kv.Key, kv => kv.Value.Category);
        if (data.GetValueOrDefault("region_categories") is Dict overrides)
            foreach (var (category, ids) in overrides)
                foreach (var rid in AsList(ids).OfType<string>())
                    cats[rid] = category;
        return cats;
    }

    /// <summary>Derive {region_id: {category, roles}} for every region (named and synthetic).</summary>
    public static Dictionary<string, RegionFacet> DeriveFacets(Dict data)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var filters = AsDict(data.GetValueOrDefault("filters"));

        // ── direct gameplay-signal sets ──────────────────────────────────────────────
        var spawnIds = AsList(data.GetValueOrDefault("spawns"))
            .OfType<Dict>().Select(s => RefId(s.GetValueOrDefault("region"))).Where(x => x.Length > 0).ToHashSet();
        var obs = AsDict(data.GetValueOrDefault("observer_spawn"));
        var observerId = obs.Count > 0 ? RefId(obs.GetValueOrDefault("region")) : "";

        var monumentIds = new HashSet<string>();
        var woolRoomIds = new HashSet<string>();
        foreach (var wool in IterWools(data))
        {
            if (wool.GetValueOrDefault("wool_room_region") is string wr && wr.Length > 0) woolRoomIds.Add(wr);
            foreach (var mon in AsList(wool.GetValueOrDefault("monuments")).OfType<Dict>())
                if (mon.GetValueOrDefault("monument_region") is string mr && mr.Length > 0) monumentIds.Add(mr);
        }

        var woolSpawnerIds = new HashSet<string>();
        var mechanicIds = new HashSet<string>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawners")).OfType<Dict>())
        {
            var spawnRegion = sp.GetValueOrDefault("spawn_region") as string;
            var playerRegion = sp.GetValueOrDefault("player_region") as string;
            if (SpawnerDispensesWool(sp))
            {
                if (!string.IsNullOrEmpty(spawnRegion)) woolSpawnerIds.Add(spawnRegion);
                if (!string.IsNullOrEmpty(playerRegion)) woolRoomIds.Add(playerRegion);
            }
            else if (!string.IsNullOrEmpty(spawnRegion)) mechanicIds.Add(spawnRegion);
        }

        // ── apply-rule wiring ────────────────────────────────────────────────────────
        var rulesByRegion = new Dictionary<string, List<(string ev, string fid)>>();
        var enterOnly = new Dictionary<string, string>();
        var timeGated = new Dictionary<string, string>();
        var msgHint = new Dictionary<string, string>();
        var ironSpawnIds = new HashSet<string>();
        var spawnKitIds = new HashSet<string>();
        var placementBuildRegions = new HashSet<string>();
        var actionMechanicRegions = new HashSet<string>();

        foreach (var rule in AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>())
        {
            foreach (var ev in new[] { "block_place", "block" })
                if (rule.GetValueOrDefault(ev) is string val && regions.ContainsKey(val) && !filters.ContainsKey(val))
                    placementBuildRegions.Add(val);

            var rid = rule.GetValueOrDefault("region") as string;
            if (string.IsNullOrEmpty(rid)) continue;

            var kit = (rule.GetValueOrDefault("kit") as string) ?? (rule.GetValueOrDefault("lend_kit") as string);
            if (!string.IsNullOrEmpty(kit) && IsSpawnKit(kit)) spawnKitIds.Add(rid);
            else if (!string.IsNullOrEmpty(rule.GetValueOrDefault("velocity") as string) || !string.IsNullOrEmpty(kit)) actionMechanicRegions.Add(rid);

            foreach (var ev in RuleEvents)
            {
                if (rule.GetValueOrDefault(ev) is not string fid || fid.Length == 0) continue;
                rulesByRegion.TryAdd(rid, []);
                rulesByRegion[rid].Add((ev, fid));
                if (FilterTypePresent(fid, filters, TimeFilterTypes) is { } hit)
                    timeGated[rid] = hit.GetValueOrDefault("duration") as string ?? "";
            }

            if (rule.GetValueOrDefault("enter") is string enter)
            {
                if (enter.StartsWith("only-")) enterOnly.TryAdd(rid, enter["only-".Length..]);
                else if (enter.StartsWith("not-")) woolRoomIds.Add(rid);
            }
            if (MessageCategory(rule.GetValueOrDefault("message")) is { } hint2) msgHint.TryAdd(rid, hint2);
            if (IsSpawnBlockPattern(rule, filters)) ironSpawnIds.Add(rid);
        }
        // stable sort by event only (matches Python's stable list.sort(key=event) — preserves
        // apply-rule order for equal events, e.g. two `enter=` entries on one region)
        foreach (var rid in rulesByRegion.Keys.ToList())
            rulesByRegion[rid] = rulesByRegion[rid].OrderBy(e => e.ev, StringComparer.Ordinal).ToList();

        var ruledRegions = rulesByRegion.Keys.ToHashSet();

        foreach (var ren in AsList(data.GetValueOrDefault("renewables")).OfType<Dict>())
            if (ren.GetValueOrDefault("region_id") is string rid && rid.Length > 0) actionMechanicRegions.Add(rid);
        var actionMechanicIds = actionMechanicRegions
            .Where(rid => (AsDict(regions.GetValueOrDefault(rid)).GetValueOrDefault("type") as string) is not ("negative" or "complement")).ToHashSet();

        // ── build regions ────────────────────────────────────────────────────────────
        var buildIds = DeriveBuildIds(regions, filters, rulesByRegion, timeGated.Keys.ToHashSet(), placementBuildRegions);

        // ── category assignment by precedence ────────────────────────────────────────
        var cat = new Dictionary<string, string?>();
        foreach (var rid in regions.Keys) cat[rid] = null;

        void Set(IEnumerable<string> ids, string value)
        {
            foreach (var rid in ids)
                if (cat.TryGetValue(rid, out var cur) && cur is null) cat[rid] = value;
        }

        if (observerId.Length > 0) cat[observerId] = "observer_spawn";
        Set(spawnIds, "spawn");
        Set(monumentIds, "monument");
        Set(woolSpawnerIds, "wool_spawner");
        Set(woolRoomIds, "wool_room");
        foreach (var (rid, hint) in msgHint) if (cat.TryGetValue(rid, out var c) && c is null) cat[rid] = hint;
        Set(ironSpawnIds, "spawn");
        Set(spawnKitIds, "spawn");
        Set(buildIds, "build");
        Set(mechanicIds, "mechanic");

        foreach (var (rid, _) in enterOnly)
        {
            if (cat.GetValueOrDefault(rid) is not null) continue;
            var name = rid.ToLowerInvariant();
            if (name.Contains("spawn")) cat[rid] = "spawn";
            else if (name.Contains("wool") || name.Contains("room") || name.Contains("monument")) cat[rid] = "wool_room";
        }

        foreach (var (rid, region) in regions)
        {
            if (cat.GetValueOrDefault(rid) is not null) continue;
            if (CompoundTypes.Contains(AsDict(region).GetValueOrDefault("type") as string ?? "")) continue;
            cat[rid] = NameHeuristic(rid);
        }

        ResolveCompounds(regions, cat);
        var ruleGroupIds = DetectRuleGroups(regions, cat, ruledRegions);

        foreach (var rid in actionMechanicIds)
            if (cat.GetValueOrDefault(rid) is null or "other") cat[rid] = "mechanic";

        // ── assemble output ──────────────────────────────────────────────────────────
        var output = new Dictionary<string, RegionFacet>();
        foreach (var (rid, regionObj) in regions)
        {
            var region = AsDict(regionObj);
            var roles = new List<string>();
            if (region.GetValueOrDefault("type") as string == "negative") roles.Add("rule_container");
            if (ruleGroupIds.Contains(rid)) roles.Add("rule_group");
            if (timeGated.TryGetValue(rid, out var dur)) roles.Add(dur.Length > 0 ? $"time_gated={dur}" : "time_gated");
            if (rulesByRegion.TryGetValue(rid, out var evs)) roles.AddRange(evs.Select(e => $"{e.ev}={e.fid}"));

            // Map the fine internal category to the emitted (category, subtype). The precedence/compound
            // logic above works in fine terms (spawn/monument/wool_room/wool_spawner) for fidelity; here we
            // fold the objective trio into one `wool` category (subtypes monument|room|spawner) and split
            // `spawn` into point|protection. A spawn point is the region in spawns[].region; every other
            // spawn region is protection (disjoint across the whole corpus).
            var fine = cat.GetValueOrDefault(rid) ?? "other";
            (string category, string? subtype) = fine switch
            {
                "monument"     => ("wool",  "monument"),
                "wool_room"    => ("wool",  "room"),
                "wool_spawner" => ("wool",  "spawner"),
                "spawn"        => ("spawn", spawnIds.Contains(rid) ? "point" : "protection"),
                _              => (fine,    (string?)null),
            };
            output[rid] = new RegionFacet(category, roles, subtype);
        }
        return output;
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];

    private static string RefId(object? r) => r switch
    {
        string s => s,
        Dict d => d.GetValueOrDefault("id") as string ?? "",
        _ => "",
    };

    private static bool IsSynthetic(string rid) => rid.Contains("__anon_") || rid.Contains("__apply_");

    private static List<string> ChildIds(Dict region)
        => AsList(region.GetValueOrDefault("children")).Select(RefId).Where(s => s.Length > 0).ToList();

    private static List<Dict> IterWools(Dict data) => data.GetValueOrDefault("wools") switch
    {
        Dict d => d.Values.OfType<Dict>().ToList(),
        List<object?> l => l.OfType<Dict>().ToList(),
        _ => [],
    };

    private static bool SpawnerDispensesWool(Dict spawner)
        => AsList(spawner.GetValueOrDefault("items")).OfType<Dict>()
            .Any(item => (item.GetValueOrDefault("material") as string ?? "").ToLowerInvariant().Contains("wool"));

    private static string? MessageCategory(object? message)
    {
        if (message is not string s) return null;
        var text = s.ToLowerInvariant();
        foreach (var (needles, category) in MessageRules)
            if (needles.Any(text.Contains)) return category;
        return null;
    }

    private static bool IsSpawnKit(string kitId)
    {
        var k = kitId.ToLowerInvariant();
        if (new[] { "leave", "remove", "reset", "exit", "outof" }.Any(k.Contains)) return false;
        return k.Contains("spawn");
    }

    private static bool IsSpawnBlockPattern(Dict rule, Dict filters)
    {
        var bb = rule.GetValueOrDefault("block_break") as string;
        var bp = rule.GetValueOrDefault("block_place") as string;
        if (string.IsNullOrEmpty(bb) || string.IsNullOrEmpty(bp)) return false;
        var breaksMaterial = FilterTypePresent(bb, filters, ["material"]) is not null;
        var deniesPlace = FilterTypePresent(bp, filters, ["deny", "never"]) is not null
                          || (!filters.ContainsKey(bp) && (bp.ToLowerInvariant().Contains("deny") || bp.ToLowerInvariant().Contains("never")));
        return breaksMaterial && deniesPlace;
    }

    private static Dict? FilterTypePresent(string fid, Dict filters, HashSet<string> wanted, HashSet<string>? seen = null)
    {
        seen ??= [];
        if (string.IsNullOrEmpty(fid) || !seen.Add(fid)) return null;
        if (filters.GetValueOrDefault(fid) is not Dict f) return null;
        if (wanted.Contains(f.GetValueOrDefault("type") as string ?? "")) return f;
        var refs = new List<object?> { f.GetValueOrDefault("child") };
        refs.AddRange(AsList(f.GetValueOrDefault("children")));
        foreach (var r in refs)
        {
            var hit = FilterTypePresent(RefId(r), filters, wanted, seen);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static string? NameHeuristic(string rid)
    {
        var name = rid.ToLowerInvariant();
        if (name.Contains("monument")) return "monument";
        var tokens = NonAlnum().Split(name);
        if (name.Contains("wool") || name.Contains("room") || tokens.Any(t => WrToken().IsMatch(t))) return "wool_room";
        if (name.Contains("spawner")) return "mechanic";
        if (name.Contains("spawn")) return "spawn";
        return null;
    }

    private static HashSet<string> DeriveBuildIds(Dict regions, Dict filters,
        Dictionary<string, List<(string ev, string fid)>> rulesByRegion,
        HashSet<string> timeGatedIds, HashSet<string> placementBuildRegions)
    {
        var buildRoots = new HashSet<string>(timeGatedIds);
        buildRoots.UnionWith(placementBuildRegions);
        foreach (var (rid, regionObj) in regions)
        {
            var region = AsDict(regionObj);
            var typ = region.GetValueOrDefault("type") as string;
            if (typ is not ("negative" or "complement")) continue;
            if (!HasVoidRule(rid, filters, rulesByRegion)) continue;
            var kids = ChildIds(region);
            buildRoots.UnionWith(typ == "negative" ? kids : kids.Skip(1));
        }

        var buildIds = new HashSet<string>();
        var stack = new Stack<string>(buildRoots);
        while (stack.Count > 0)
        {
            var rid = stack.Pop();
            if (buildIds.Contains(rid) || !regions.ContainsKey(rid)) continue;
            var hint = NameHeuristic(rid);
            if (hint is "spawn" or "wool_room" or "monument" or "wool_spawner" or "mechanic") continue;
            buildIds.Add(rid);
            foreach (var k in ChildIds(AsDict(regions[rid]))) stack.Push(k);
        }
        return buildIds;
    }

    private static bool HasVoidRule(string rid, Dict filters, Dictionary<string, List<(string ev, string fid)>> rulesByRegion)
    {
        if (!rulesByRegion.TryGetValue(rid, out var rules)) return false;
        foreach (var (ev, fid) in rules)
        {
            if (ev is not ("block" or "block_break" or "block_place")) continue;
            if (FilterTypePresent(fid, filters, ["void"]) is not null) return true;
            if (!filters.ContainsKey(fid) && fid.ToLowerInvariant().Contains("void")) return true;
        }
        return false;
    }

    private static void ResolveCompounds(Dict regions, Dictionary<string, string?> cat)
    {
        var resolving = new HashSet<string>();

        string Resolve(string rid)
        {
            if (cat.GetValueOrDefault(rid) is { } existing) return existing;
            if (regions.GetValueOrDefault(rid) is not Dict region || resolving.Contains(rid)) return "other";
            resolving.Add(rid);
            var typ = region.GetValueOrDefault("type") as string;
            var kids = ChildIds(region);
            string result;
            if (typ == "negative") result = "other";
            else if (typ == "complement") result = kids.Count > 0 ? Resolve(kids[0]) : "other";
            else if (typ == "union")
            {
                var peers = NamedPeers(rid, regions, Resolve);
                var cats = peers.Values.ToHashSet();
                if (peers.Count >= 2 && cats.Count == 1 && cats.First() != "other") result = cats.First();
                else result = kids.Count > 0 ? Resolve(kids[0]) : "other";
            }
            else if (typ is "mirror" or "translate")
            {
                var src = RefId(region.GetValueOrDefault("source_id"));
                result = src.Length > 0 ? Resolve(src) : "other";
            }
            else result = kids.Count > 0 ? Resolve(kids[0]) : "other";
            resolving.Remove(rid);
            cat[rid] = result;
            return result;
        }

        foreach (var rid in regions.Keys.ToList())
            if (cat.GetValueOrDefault(rid) is null) Resolve(rid);
    }

    private static HashSet<string> DetectRuleGroups(Dict regions, Dictionary<string, string?> cat, HashSet<string> ruledRegions)
    {
        var ruleGroupIds = new HashSet<string>();
        foreach (var (rid, regionObj) in regions)
        {
            if (AsDict(regionObj).GetValueOrDefault("type") as string != "union" || !ruledRegions.Contains(rid)) continue;
            var peers = NamedPeers(rid, regions, c => cat.GetValueOrDefault(c) ?? "other");
            var cats = peers.Values.ToHashSet();
            if (peers.Count >= 2 && cats.Count == 1 && cats.First() != "other") ruleGroupIds.Add(rid);
        }
        return ruleGroupIds;
    }

    private static Dictionary<string, string> NamedPeers(string rid, Dict regions, Func<string, string> resolve)
    {
        var peers = new Dictionary<string, string>();
        var seen = new HashSet<string>();

        void Walk(string nodeId)
        {
            if (regions.GetValueOrDefault(nodeId) is not Dict region) return;
            foreach (var child in ChildIds(region))
            {
                if (!seen.Add(child)) continue;
                if (!IsSynthetic(child)) peers[child] = resolve(child);
                else if (AsDict(regions.GetValueOrDefault(child)).GetValueOrDefault("type") as string == "union") Walk(child);
            }
        }

        Walk(rid);
        return peers;
    }

    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NonAlnum();
    [GeneratedRegex(@"^wr\d*s?$")] private static partial Regex WrToken();
}
