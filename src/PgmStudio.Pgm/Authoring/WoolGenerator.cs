namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool/objective slice of the declarative generator (new-map-authoring.md; filter-region-wiring.md
/// templates 3 + 4). Per wool it emits: the wool-room region, the wool element (with monuments), a wool
/// spawn point fed by a <c>&lt;spawner&gt;</c> (player-region = room, spawn-region = the point, item =
/// the dyed wool), and the room wiring — <c>enter=not-&lt;owner&gt;</c> (defenders kept out of their own
/// room) plus <c>block=not-&lt;owner&gt;</c> (only attackers may edit the room; simplified template 4,
/// no material whitelist).
/// <para>The wool's <c>location</c> is the int-floored <see cref="WoolIntent.Spawn"/> point.</para>
/// <para>Mirror of <c>RegionCategorizer</c>: the room reads back as <c>wool/room</c> and the spawn point
/// as <c>wool/spawner</c>. Idempotent clear-then-build.</para>
/// </summary>
public static class WoolGenerator
{
    private const string EnterMessage = "You may not enter your own wool room!";
    private const string EditMessage = "You may not modify the wool room!";
    private const string SpawnDelay = "1.5s";

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
        foreach (var w in intent.Wools) Remove(doc, ColorSlug(doc, w), IntentNaming.Slug(w.Owner));

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
                WoolEditor.AddMonument(doc, colorSlug, new Dict
                {
                    ["team"] = m.Team,
                    ["location"] = new Dict { ["x"] = m.Location.X, ["y"] = m.Location.Y, ["z"] = m.Location.Z },
                });

            if (w.Room is not { } room) continue;   // no room yet → skip the source side (region/spawner/wiring)

            // regions: the wool room + the wool spawn point
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

            // spawner: dispense the dyed wool in the room, dropping at the spawn point
            DocAccess.EnsureList(doc, "spawners").Add(new Dict
            {
                ["spawn_region"] = spawnId, ["player_region"] = roomId, ["delay"] = SpawnDelay,
                ["items"] = new List<object?> { new Dict { ["material"] = "wool", ["damage"] = DyeDamage.GetValueOrDefault(colorSlug, 0) } },
            });

            // room wiring: only-<owner> (reused from spawn protection if present) → not-<owner>. Both
            // filters are per-team, not per-wool, so a team that defends several wools shares them — guard
            // both creations (a second same-owner wool would otherwise collide on the filter id).
            var only = $"only-{ownerSlug}";
            var notOwner = $"not-{ownerSlug}";
            if (!DocAccess.Filters(doc).ContainsKey(only))
                FilterEditor.CreateFilter(doc, new Dict { ["id"] = only, ["type"] = "team", ["team"] = w.Owner });
            if (!DocAccess.Filters(doc).ContainsKey(notOwner))
                FilterEditor.CreateFilter(doc, new Dict { ["id"] = notOwner, ["type"] = "not", ["child"] = only });

            ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["enter"] = notOwner, ["region"] = roomId, ["message"] = EnterMessage });
            ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["block"] = notOwner, ["region"] = roomId, ["message"] = EditMessage });
        }
    }

    private static void Remove(Dict doc, string colorSlug, string ownerSlug)
    {
        var roomId = $"{colorSlug}-wool";
        var spawnId = $"{colorSlug}-wool-spawn";
        DocAccess.Regions(doc).Remove(roomId);
        DocAccess.Regions(doc).Remove(spawnId);
        DocAccess.Filters(doc).Remove($"not-{ownerSlug}");   // NOT only-<owner> — the spawn-protection slice may own it
        if (doc.GetValueOrDefault("wools") is List<object?> wools)
            wools.RemoveAll(x => x is Dict d && d.GetValueOrDefault("id") as string == colorSlug);
        if (doc.GetValueOrDefault("spawners") is List<object?> spawners)
            spawners.RemoveAll(x => x is Dict d && d.GetValueOrDefault("spawn_region") as string == spawnId);
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string == roomId);
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
