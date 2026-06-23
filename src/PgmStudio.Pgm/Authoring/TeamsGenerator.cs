namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
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
        var kits = DocAccess.EnsureList(doc, "kits");
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
        foreach (var sp in intent.Spawns) RemoveSpawn(doc, IntentNaming.Slug(sp.Team));   // clear-then-build (idempotent)
        // The shared spawn-protection region + its block rule are rebuilt below; drop any prior copy.
        DocAccess.Regions(doc).Remove("spawns");
        DocAccess.Regions(doc).Remove("spawn-areas");
        if (doc.GetValueOrDefault("apply_rules") is List<object?> priorRules)
            priorRules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string == "spawns");

        var protIds = new List<string>();
        foreach (var sp in intent.Spawns)
        {
            var slug = IntentNaming.Slug(sp.Team);
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
                // keep enemies out, per team (enter=only-<team>); the block protection is shared, on the spawns union.
                ApplyRuleEditor.CreateApplyRule(doc, new Dict
                {
                    ["enter"] = fid, ["region"] = protId, ["message"] = ProtectionMessage,
                });
                protIds.Add(protId);
            }
        }

        // One shared spawns union carries the block protection (template structure). WoolGenerator folds the
        // wool monuments out of it (→ a complement) so capturing a wool doesn't trip this rule, and
        // ResourceRenewables relaxes block=never to "only the spawn ore is breakable" when ore lives here.
        if (protIds.Count > 0)
        {
            AddUnion(doc, "spawns", protIds);
            ApplyRuleEditor.CreateApplyRule(doc, new Dict
            {
                ["block"] = "never", ["region"] = "spawns", ["message"] = BlockProtectionMessage,
            });
        }
    }

    // A union region from its child ids, with derived bounds (allows a single child, unlike GroupRegions).
    private static void AddUnion(Dict doc, string id, List<string> childIds)
    {
        var regions = DocAccess.Regions(doc);
        var (bounds, _, _, _, _) = RegionBuilder.BuildUnionBounds(childIds.Select(c => (Dict)regions[c]!));
        var union = new Dict { ["id"] = id, ["type"] = "union", ["children"] = childIds.Cast<object?>().ToList() };
        if (bounds is not null) union["bounds_2d"] = bounds;
        regions[id] = union;
    }

    // ── observer (<default>) spawn ─────────────────────────────────────────────────────
    private const string ObserverRegionId = "observer-spawn";

    // PGM requires exactly one <default> spawn, so always emit one — synthesise a sensible default
    // (the spawn-points' centroid) when the author hasn't placed an observer spawn.
    private static void GenerateObserver(Dict doc, MapIntent intent)
    {
        DocAccess.Regions(doc).Remove(ObserverRegionId);
        doc.Remove("observer_spawn");

        var o = intent.Observer ?? SynthesizeObserver(intent.Spawns);
        if (o is null) return;   // no spawns and no explicit observer → not a playable map anyway

        RegionEditor.CreateRegion(doc, new Dict
        {
            ["type"] = "point", ["id"] = ObserverRegionId, ["category"] = "observer_spawn",
            ["x"] = o.Point.X, ["y"] = o.Point.Y, ["z"] = o.Point.Z,
        });
        SpawnEditor.SetObserverSpawn(doc, new Dict { ["region_id"] = ObserverRegionId, ["yaw"] = o.Yaw });
    }

    private static ObserverIntent? SynthesizeObserver(List<SpawnIntent> spawns)
    {
        if (spawns.Count == 0) return null;
        double sx = 0, sy = 0, sz = 0;
        foreach (var s in spawns) { sx += s.Point.X; sy += s.Point.Y; sz += s.Point.Z; }
        var n = spawns.Count;
        return new ObserverIntent { Point = new Pt(Math.Round(sx / n, 1), Math.Round(sy / n, 1), Math.Round(sz / n, 1)) };
    }

    // ── cleanup / helpers ──────────────────────────────────────────────────────────────
    private static void RemoveSpawn(Dict doc, string slug)
    {
        var pointId = $"{slug}-spawn-point";
        var protId = $"{slug}-spawn";
        DocAccess.Regions(doc).Remove(pointId);
        DocAccess.Regions(doc).Remove(protId);
        DocAccess.Filters(doc).Remove($"only-{slug}");
        if (doc.GetValueOrDefault("spawns") is List<object?> spawns)
            spawns.RemoveAll(s => s is Dict d && d.GetValueOrDefault("region") as string == pointId);
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string == protId);
    }
}
