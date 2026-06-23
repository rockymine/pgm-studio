namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool/objective slice of the declarative generator (new-map-authoring.md; filter-region-wiring.md
/// templates 3 + 4). Per wool it emits: the wool-room region, the wool element (with monuments), a wool
/// spawn point fed by a <c>&lt;spawner&gt;</c> (player-region = room, spawn-region = the point, item =
/// the dyed wool). The room wiring follows the validated template (docs/template.xml): the rooms are grouped
/// per defending team into a <c>&lt;team&gt;s-woolrooms</c> union (all under a top <c>woolrooms</c> union),
/// with <c>enter=not-&lt;owner&gt;</c> (defenders kept out) and a <c>block</c> rule whose filter
/// <c>&lt;owner&gt;s-woolrooms-filter = all(not-&lt;owner&gt;, woolrooms-filter)</c> lets attackers edit only
/// the shared <c>woolrooms-filter</c> materials — placing spawn-kit blocks + water and breaking the
/// entrance decoration (cobweb, stained glass + panes) — rather than forbidding everything.
/// <para>The wool's <c>location</c> is the int-floored <see cref="WoolIntent.Spawn"/> point. Mirror of
/// <c>RegionCategorizer</c>: the room reads back as <c>wool/room</c>, the spawn point as <c>wool/spawner</c>.
/// Idempotent clear-then-build.</para>
/// </summary>
public static class WoolGenerator
{
    private const string EnterMessage = "You may not enter your own wool room!";
    private const string EditMessage = "You may not edit the wool room!";
    private const string SpawnDelay = "1.5s";
    private const string WoolroomsFilter = "woolrooms-filter";

    // The synthetic leaf/compound filters that make up the shared woolrooms-filter (all start with "__" so
    // the serializer inlines them).
    private static readonly string[] WoolroomsSynthetics =
        ["__wr-web", "__wr-glass", "__wr-pane", "__wr-wood", "__wr-clay", "__wr-water", "__wr-swater", "__wr-water-any", "__wr-cause-player", "__wr-water-all"];

    // wool block damage = dye id (1.8 metadata), keyed by the WoolEditor colour slug.
    private static readonly Dictionary<string, int> DyeDamage = new()
    {
        ["white"] = 0, ["orange"] = 1, ["magenta"] = 2, ["light_blue"] = 3, ["yellow"] = 4, ["lime"] = 5,
        ["pink"] = 6, ["gray"] = 7, ["silver"] = 8, ["cyan"] = 9, ["purple"] = 10, ["blue"] = 11,
        ["brown"] = 12, ["green"] = 13, ["red"] = 14, ["black"] = 15,
    };

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.Wools is null) return;
        ClearWoolrooms(doc, intent.Wools);

        var roomsByOwner = new Dictionary<string, List<string>>();
        var ownerOrder = new List<string>();
        var monumentBlockIds = new List<string>();

        foreach (var w in intent.Wools)
        {
            var colorSlug = ColorSlug(doc, w);
            var ownerSlug = IntentNaming.Slug(w.Owner);
            var roomId = $"{colorSlug}-wool";
            var spawnId = $"{colorSlug}-wool-spawn";

            // wool element + monuments (one per capturing team) — emitted even before the room is drawn,
            // so a partly-authored map still generates its objectives (new-map-authoring.md §11).
            WoolEditor.AddWool(doc, new Dict { ["color"] = colorSlug });
            var update = new Dict
            {
                ["team"] = w.Owner,
                ["location"] = new Dict { ["x"] = Floor(w.Spawn.X), ["y"] = Floor(w.Spawn.Y), ["z"] = Floor(w.Spawn.Z) },
            };
            if (w.Room is not null) update["wool_room_region"] = roomId;
            WoolEditor.UpdateWool(doc, colorSlug, update);
            foreach (var m in w.Monuments)
            {
                // The monument is a named <block> region the wool references (monument="…") rather than an
                // inline block, so it can also be subtracted from the spawns protection (SubtractMonuments…).
                var monBlockId = MonumentBlockId(colorSlug, IntentNaming.Slug(m.Team));
                RegionEditor.CreateRegion(doc, new Dict
                {
                    ["type"] = "block", ["id"] = monBlockId, ["category"] = "wool",
                    ["x"] = m.Location.X, ["y"] = m.Location.Y, ["z"] = m.Location.Z,
                });
                monumentBlockIds.Add(monBlockId);
                WoolEditor.AddMonument(doc, colorSlug, new Dict
                {
                    ["team"] = m.Team,
                    ["location"] = new Dict { ["x"] = m.Location.X, ["y"] = m.Location.Y, ["z"] = m.Location.Z },
                    ["monument_region"] = monBlockId,
                });
            }

            if (w.Room is not { } room) continue;   // no room yet → skip the source side (region/spawner/wiring)

            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "rectangle", ["id"] = roomId, ["category"] = "wool",
                ["min_x"] = room.MinX, ["min_z"] = room.MinZ, ["max_x"] = room.MaxX, ["max_z"] = room.MaxZ,
            });
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "point", ["id"] = spawnId, ["category"] = "wool",
                ["x"] = w.Spawn.X, ["y"] = w.Spawn.Y, ["z"] = w.Spawn.Z,
            });
            DocAccess.EnsureList(doc, "spawners").Add(new Dict
            {
                ["spawn_region"] = spawnId, ["player_region"] = roomId, ["delay"] = SpawnDelay,
                ["items"] = new List<object?> { new Dict { ["material"] = "wool", ["damage"] = DyeDamage.GetValueOrDefault(colorSlug, 0) } },
            });

            if (!roomsByOwner.ContainsKey(ownerSlug)) { roomsByOwner[ownerSlug] = []; ownerOrder.Add(ownerSlug); }
            roomsByOwner[ownerSlug].Add(roomId);

            // only-<owner> team filter (reused from spawn protection if present) — the child of not-<owner>.
            EnsureFilter(doc, $"only-{ownerSlug}", new Dict { ["type"] = "team", ["team"] = w.Owner });
        }

        SubtractMonumentsFromSpawns(doc, monumentBlockIds);   // independent of rooms — monuments exist either way

        if (roomsByOwner.Count == 0) return;
        EnsureWoolroomsFilter(doc);

        var ownerUnions = new List<string>();
        foreach (var ownerSlug in ownerOrder)
        {
            var not = $"not-{ownerSlug}";
            EnsureFilter(doc, not, new Dict { ["type"] = "not", ["child"] = $"only-{ownerSlug}" });
            // <owner>s-woolrooms-filter = all(not-<owner>, woolrooms-filter)
            var roomFilter = $"{ownerSlug}s-woolrooms-filter";
            EnsureFilter(doc, roomFilter, new Dict { ["type"] = "all", ["children"] = new List<object?> { not, WoolroomsFilter } });

            var union = $"{ownerSlug}s-woolrooms";
            AddUnion(doc, union, roomsByOwner[ownerSlug]);
            ownerUnions.Add(union);

            ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["enter"] = not, ["region"] = union, ["message"] = EnterMessage });
            ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["block"] = roomFilter, ["region"] = union, ["message"] = EditMessage });
        }
        // top-level union of all the per-team room unions
        AddUnion(doc, "woolrooms", ownerUnions);
    }

    // A union region from its children (allows a single child, unlike the editor's GroupRegions); bounds are
    // the derived footprint so the canvas can still frame it.
    private static void AddUnion(Dict doc, string id, List<string> childIds)
    {
        var regions = DocAccess.Regions(doc);
        var (bounds, _, _, _, _) = RegionBuilder.BuildUnionBounds(childIds.Select(c => (Dict)regions[c]!));
        var union = new Dict { ["id"] = id, ["type"] = "union", ["children"] = childIds.Cast<object?>().ToList() };
        if (bounds is not null) union["bounds_2d"] = bounds;
        regions[id] = union;
    }

    private static string MonumentBlockId(string colorSlug, string teamSlug) => $"{colorSlug}-{teamSlug}-monument";

    // Fold the wool monuments out of the spawns protection so placing a captured wool on its monument (which
    // sits inside a spawn) doesn't trip the spawn block rule — PGM allows the placement, we just suppress the
    // spurious deny. Mirrors template.xml: spawns becomes complement(spawn-areas, monument blocks). No-op when
    // there's no spawn protection (TeamsGenerator only builds the spawns union when spawns are protected).
    private static void SubtractMonumentsFromSpawns(Dict doc, List<string> monumentBlockIds)
    {
        var regions = DocAccess.Regions(doc);
        if (monumentBlockIds.Count == 0 || regions.GetValueOrDefault("spawns") is not Dict spawns
            || spawns.GetValueOrDefault("type") as string != "union") return;

        // move the spawn rectangles into spawn-areas, then make spawns the complement that subtracts the monuments
        var rects = (spawns.GetValueOrDefault("children") as List<object?>) ?? new();
        regions["spawn-areas"] = new Dict
        {
            ["id"] = "spawn-areas", ["type"] = "union", ["children"] = rects, ["bounds_2d"] = spawns.GetValueOrDefault("bounds_2d"),
        };
        spawns["type"] = "complement";
        spawns["children"] = new List<object?> { "spawn-areas" }.Concat(monumentBlockIds.Cast<object?>()).ToList();
    }

    // The shared whitelist of materials editable in any wool room: place the spawn-kit blocks (wood, the
    // team-coloured clay) + water (player-caused), break the entrance decoration (cobweb, stained glass +
    // panes). One <any> of synthetic leaves so the serializer inlines it.
    private static void EnsureWoolroomsFilter(Dict doc)
    {
        if (DocAccess.Filters(doc).ContainsKey(WoolroomsFilter)) return;
        void Mat(string id, string material) => EnsureFilter(doc, id, new Dict { ["type"] = "material", ["material"] = material });
        Mat("__wr-web", "web");
        Mat("__wr-glass", "stained glass");
        Mat("__wr-pane", "stained glass pane");
        Mat("__wr-wood", "wood");
        Mat("__wr-clay", "stained clay");
        Mat("__wr-water", "water");
        Mat("__wr-swater", "stationary water");
        EnsureFilter(doc, "__wr-water-any", new Dict { ["type"] = "any", ["children"] = new List<object?> { "__wr-water", "__wr-swater" } });
        EnsureFilter(doc, "__wr-cause-player", new Dict { ["type"] = "cause", ["cause"] = "player" });
        EnsureFilter(doc, "__wr-water-all", new Dict { ["type"] = "all", ["children"] = new List<object?> { "__wr-cause-player", "__wr-water-any" } });
        EnsureFilter(doc, WoolroomsFilter, new Dict
        {
            ["type"] = "any", ["children"] = new List<object?> { "__wr-web", "__wr-glass", "__wr-pane", "__wr-wood", "__wr-clay", "__wr-water-all" },
        });
    }

    private static void EnsureFilter(Dict doc, string id, Dict payload)
    {
        if (DocAccess.Filters(doc).ContainsKey(id)) return;
        FilterEditor.CreateFilter(doc, new Dict(payload) { ["id"] = id });
    }

    private static void ClearWoolrooms(Dict doc, List<WoolIntent> wools)
    {
        var regions = DocAccess.Regions(doc);
        var filters = DocAccess.Filters(doc);
        var owners = new HashSet<string>();

        foreach (var w in wools)
        {
            var colorSlug = ColorSlug(doc, w);
            var ownerSlug = IntentNaming.Slug(w.Owner);
            owners.Add(ownerSlug);
            regions.Remove($"{colorSlug}-wool");
            regions.Remove($"{colorSlug}-wool-spawn");
            foreach (var m in w.Monuments) regions.Remove(MonumentBlockId(colorSlug, IntentNaming.Slug(m.Team)));
            if (doc.GetValueOrDefault("wools") is List<object?> ws)
                ws.RemoveAll(x => x is Dict d && d.GetValueOrDefault("id") as string == colorSlug);
            if (doc.GetValueOrDefault("spawners") is List<object?> sp)
                sp.RemoveAll(x => x is Dict d && d.GetValueOrDefault("spawn_region") as string == $"{colorSlug}-wool-spawn");
        }

        regions.Remove("woolrooms");
        filters.Remove(WoolroomsFilter);
        foreach (var syn in WoolroomsSynthetics) filters.Remove(syn);
        foreach (var o in owners)
        {
            regions.Remove($"{o}s-woolrooms");
            filters.Remove($"{o}s-woolrooms-filter");
            filters.Remove($"not-{o}");   // not only-<owner> — the spawn-protection slice may own it
        }
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") is string reg
                && (reg == "woolrooms" || reg.EndsWith("s-woolrooms") || reg.EndsWith("-wool")));
    }

    // Wool colour slug (underscore form, matching WoolEditor's ValidColors); defaults to the owner team's colour.
    private static string ColorSlug(Dict doc, WoolIntent w)
    {
        var color = w.Color.Length > 0 ? w.Color : TeamColor(doc, w.Owner);
        return color.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string TeamColor(Dict doc, string teamId)
    {
        if (doc.GetValueOrDefault("teams") is List<object?> teams)
            foreach (var t in teams.OfType<Dict>())
                if (t.GetValueOrDefault("id") as string == teamId)
                    return t.GetValueOrDefault("color") as string ?? "white";
        return "white";
    }

    private static int Floor(double v) => (int)Math.Floor(v);
}
