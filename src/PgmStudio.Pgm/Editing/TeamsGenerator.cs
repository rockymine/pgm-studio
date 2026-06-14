using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Forward generation for the Teams slice of the declarative authoring model
/// (docs/contracts/new-map-authoring.md). Projects a <see cref="MapIntent"/> into the PGM document:
/// teams, a spawn kit, per-team spawn-point + protection regions with the spawn-protection wiring
/// (an <c>only-&lt;team&gt;</c> filter + an <c>enter</c> apply-rule — filter-region-wiring.md template
/// 2), the <c>spawns[]</c> links, and the observer (<c>&lt;default&gt;</c>) spawn.
/// <para>Mirror of <c>RegionCategorizer</c>: the regions it emits read back as <c>spawn/point</c>,
/// <c>spawn/protection</c>, and <c>observer_spawn</c> (the consistency check).</para>
/// <para>Idempotent: clears its own prior output (by deterministic id) before regenerating, so
/// re-running after an intent edit never duplicates. The save path is entity-replace anyway, so the
/// doc is rebuilt from scratch each save — see region-data-flow.md.</para>
/// </summary>
public static class TeamsGenerator
{
    private const string ProtectionMessage = "You may not enter the enemy's spawn!";
    private const string BlockProtectionMessage = "You may not block/break spawns!";
    // One fixed spawn kit for now — the author never sees or picks it (the "Standard" preset below).
    private const string SpawnKitId = "spawn-kit";

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.Teams is { Count: > 0 }) GenerateTeams(doc, intent);
        GenerateKits(doc, intent);
        GenerateSpawns(doc, intent);
        GenerateObserver(doc, intent);
    }

    // ── teams ────────────────────────────────────────────────────────────────────────
    // Replace the team list from the intent (one shared cap; symmetric map). Spawns/filters reference
    // team ids, so the intent's team ids must match its spawn intents' Team values.
    private static void GenerateTeams(Dict doc, MapIntent intent)
    {
        doc["teams"] = new List<object?>();
        foreach (var t in intent.Teams!)
            TeamEditor.AddTeam(doc, new Dict
            {
                ["id"] = t.Id,
                ["name"] = string.IsNullOrEmpty(t.Name) ? t.Id : t.Name,
                ["color"] = t.Color,
                ["max_players"] = intent.MaxPlayers,
                ["min_players"] = 0,
            });
    }

    // ── kit ──────────────────────────────────────────────────────────────────────────
    // Clear-then-build: the kit is fully owned by the generator (a new map). Emit the single fixed
    // "Standard" preset whenever the map has spawns. Standard = the corpus norm (survey across 345
    // kitted maps): iron tier + infinity bow, planks + team-coloured clay, golden apple + water bucket,
    // team-coloured leather armour with chainmail leggings, all unbreakable.
    private static void GenerateKits(Dict doc, MapIntent intent)
    {
        var kits = EnsureList(doc, "kits");
        kits.Clear();
        if (intent.Spawns.Count > 0) kits.Add(StandardSpawnKit(SpawnKitId));
    }

    private static Dict StandardSpawnKit(string id) => new()
    {
        ["id"] = id,
        ["items"] = new List<object?>
        {
            // hotbar: tools/weapons, then the two staple items, then a stack of build blocks
            Item(0, "iron sword", unbreakable: true),
            Item(1, "bow", unbreakable: true, enchantments: "infinity:1"),
            Item(2, "iron pickaxe", unbreakable: true, enchantments: "efficiency:1"),
            Item(3, "iron axe", unbreakable: true, enchantments: "efficiency:1"),
            Item(4, "iron spade", unbreakable: true),
            Item(5, "shears", unbreakable: true),
            Item(6, "golden apple"),
            Item(7, "water bucket"),
            Item(8, "wood", amount: 64),
            // inventory: one arrow (the infinity bow makes it endless) + the team-coloured accent block
            Item(9, "arrow"),
            Item(10, "stained clay", amount: 32, teamColor: true),
        },
        ["armor"] = new List<object?>
        {
            Armor("helmet", "leather helmet", teamColor: true, enchantments: "projectile_protection:1"),
            Armor("chestplate", "leather chestplate", teamColor: true, enchantments: "projectile_protection:1"),
            Armor("leggings", "chainmail leggings", enchantments: "projectile_protection:1"),
            Armor("boots", "leather boots", teamColor: true, enchantments: "projectile_protection:1"),
        },
    };

    private static Dict Item(int slot, string material, int amount = 1, bool unbreakable = false, bool teamColor = false, string? enchantments = null)
    {
        var r = new Dict { ["slot"] = slot, ["material"] = material };
        if (amount != 1) r["amount"] = amount;
        if (unbreakable) r["unbreakable"] = true;
        if (teamColor) r["team_color"] = true;
        if (enchantments is not null) r["enchantments"] = enchantments;
        return r;
    }

    private static Dict Armor(string slotName, string material, bool teamColor = false, string? enchantments = null)
    {
        var r = new Dict { ["slot_name"] = slotName, ["material"] = material, ["unbreakable"] = true };
        if (teamColor) r["team_color"] = true;
        if (enchantments is not null) r["enchantments"] = enchantments;
        return r;
    }

    // ── spawns + protection ────────────────────────────────────────────────────────────
    private static void GenerateSpawns(Dict doc, MapIntent intent)
    {
        foreach (var sp in intent.Spawns) RemoveSpawn(doc, SlugOf(sp.Team));   // clear-then-build (idempotent)

        foreach (var sp in intent.Spawns)
        {
            var slug = SlugOf(sp.Team);
            var pointId = $"{slug}-spawn-point";
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "point", ["id"] = pointId, ["category"] = "spawn",
                ["x"] = sp.Point.X, ["y"] = sp.Point.Y, ["z"] = sp.Point.Z,
            });
            SpawnEditor.AddSpawnLink(doc, new Dict
            {
                ["region_id"] = pointId, ["team"] = sp.Team, ["kit"] = SpawnKitId, ["yaw"] = sp.Yaw,
            });

            if (sp.Protection is { } r)
            {
                var protId = $"{slug}-spawn";
                RegionEditor.CreateRegion(doc, new Dict
                {
                    ["type"] = "rectangle", ["id"] = protId, ["category"] = "spawn",
                    ["min_x"] = r.MinX, ["min_z"] = r.MinZ, ["max_x"] = r.MaxX, ["max_z"] = r.MaxZ,
                });
                var fid = $"only-{slug}";
                FilterEditor.CreateFilter(doc, new Dict { ["id"] = fid, ["type"] = "team", ["team"] = sp.Team });
                // spawn-protection wiring (template 2): keep enemies out (enter=only-<team>) AND stop
                // grief (block=never — the built-in deny filter, no new filter needed).
                ApplyRuleEditor.CreateApplyRule(doc, new Dict
                {
                    ["enter"] = fid, ["region"] = protId, ["message"] = ProtectionMessage,
                });
                ApplyRuleEditor.CreateApplyRule(doc, new Dict
                {
                    ["block"] = "never", ["region"] = protId, ["message"] = BlockProtectionMessage,
                });
            }
        }
    }

    // ── observer (<default>) spawn ─────────────────────────────────────────────────────
    private static void GenerateObserver(Dict doc, MapIntent intent)
    {
        Regions(doc).Remove("observer-spawn-point");
        doc.Remove("observer_spawn");
        if (intent.Observer is not { } o) return;

        RegionEditor.CreateRegion(doc, new Dict
        {
            ["type"] = "point", ["id"] = "observer-spawn-point", ["category"] = "observer_spawn",
            ["x"] = o.Point.X, ["y"] = o.Point.Y, ["z"] = o.Point.Z,
        });
        SpawnEditor.SetObserverSpawn(doc, new Dict { ["region_id"] = "observer-spawn-point", ["yaw"] = o.Yaw });
    }

    // ── cleanup / helpers ──────────────────────────────────────────────────────────────
    private static void RemoveSpawn(Dict doc, string slug)
    {
        var pointId = $"{slug}-spawn-point";
        var protId = $"{slug}-spawn";
        Regions(doc).Remove(pointId);
        Regions(doc).Remove(protId);
        Filters(doc).Remove($"only-{slug}");
        if (doc.GetValueOrDefault("spawns") is List<object?> spawns)
            spawns.RemoveAll(s => s is Dict d && d.GetValueOrDefault("region") as string == pointId);
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string == protId);
    }

    // Naming slug from the team *id*, not the raw colour: ids are stable single tokens (red, blue-team→red),
    // whereas a colour can be multi-word ("dark red") and would yield ids like "only-dark red".
    private static string SlugOf(string teamId)
    {
        var s = teamId.Trim().ToLowerInvariant();
        if (s.EndsWith("-team")) s = s[..^5];
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 0 ? s : teamId;
    }

    private static List<object?> EnsureList(Dict doc, string key) =>
        doc.TryGetValue(key, out var v) && v is List<object?> l ? l : (List<object?>)(doc[key] = new List<object?>());
    private static Dict Regions(Dict doc) =>
        doc.TryGetValue("regions", out var r) && r is Dict d ? d : (Dict)(doc["regions"] = new Dict());
    private static Dict Filters(Dict doc) =>
        doc.TryGetValue("filters", out var f) && f is Dict d ? d : (Dict)(doc["filters"] = new Dict());
}
