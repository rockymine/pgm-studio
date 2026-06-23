using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Export-time enrichment that adds <c>&lt;renewables&gt;</c> for a generated map's resource blocks (iron,
/// gold, diamond) that sit <b>inside a team spawn</b> — the safe, intended CTW economy (spawn ore regrows
/// after it's mined). Ore scanned <i>elsewhere</i> is left as-is: its intent is ambiguous (decoration vs
/// resource), so we don't force a renewable on it. PGM snapshots the original world, so the reused spawns
/// union only has to <i>contain</i> the ore's (x,z).
/// <para>When any resource lives in the spawns, the spawn's <c>block=never</c> protection is relaxed once
/// to "only break those resources / only the renewable (<c>cause=world</c>) may place them" (the corpus
/// pattern), so the in-spawn ore is mineable + renewable rather than locked.</para>
/// </summary>
public static class ResourceRenewables
{
    private const int AvoidPlayers = 2;

    // Resource block ids in a fixed output order; material/slug derive from the type ("iron_block" →
    // material "iron block", slug "iron").
    private static readonly string[] Order = ["iron_block", "gold_block", "diamond_block"];

    public static void Apply(MapXml m, IReadOnlyList<(string Type, int X, int Y, int Z)> resourceBlocks)
    {
        if (resourceBlocks.Count == 0) return;

        var spawns = SpawnProtections(m);
        if (spawns.Count == 0) return;   // no spawn protection to anchor a renewable to

        var inSpawn = new List<(string Slug, string Material)>();
        var added = false;
        foreach (var type in Order)
        {
            var blocks = resourceBlocks.Where(b => b.Type == type).Select(b => (b.X, b.Y, b.Z)).ToList();
            if (blocks.Count == 0) continue;
            // Only ore that sits in a spawn is a renewable candidate; ignore the rest (ambiguous intent).
            if (!blocks.Any(b => spawns.Any(s => Covers(s.Box, b.X, b.Z)))) continue;

            var slug = type.Replace("_block", "");   // iron / gold / diamond
            var material = type.Replace('_', ' ');    // iron block / gold block / diamond block

            if (!added) { AddMaterialFilter(m, "only-air", "air"); added = true; }
            AddMaterialFilter(m, $"only-{slug}", material);
            inSpawn.Add((slug, material));

            m.Renewables.Add(new Renewable
            {
                RegionId = "spawns", RenewFilter = $"only-{slug}", ReplaceFilter = "only-air", AvoidPlayers = AvoidPlayers,
            });
        }

        if (inSpawn.Count > 0) RelaxSpawnProtection(m, spawns, inSpawn);
    }

    // ── reuse the spawns union + relax its block protection to only the in-spawn resources ────────────
    private static void RelaxSpawnProtection(MapXml m, List<(string Id, Box Box)> spawns, List<(string Slug, string Material)> inSpawn)
    {
        if (!m.Regions.ContainsKey("spawns"))
            m.Regions["spawns"] = new Region { Id = "spawns", Type = "union", Children = spawns.Select(s => s.Id).ToList() };

        var seq = 0;
        string Synth(Filter f) { f.Id = $"__renew-{seq++}"; m.Filters[f.Id] = f; return f.Id; }
        string SynthMatch() => inSpawn.Count == 1
            ? Synth(new Filter { Type = "material", Material = inSpawn[0].Material })
            : Synth(new Filter { Type = "any", Children = inSpawn.Select(r => Synth(new Filter { Type = "material", Material = r.Material })).ToList() });

        // break: only the in-spawn resources may be broken; place: only the renewable (cause=world) may place them
        string breakId;
        if (inSpawn.Count == 1) breakId = $"only-{inSpawn[0].Slug}";   // the existing top-level material filter
        else
        {
            breakId = "spawn-resources";
            m.Filters[breakId] = new Filter { Id = breakId, Type = "any", Children = inSpawn.Select(r => Synth(new Filter { Type = "material", Material = r.Material })).ToList() };
        }
        var placeId = inSpawn.Count == 1 ? $"only-{inSpawn[0].Slug}-cause-world" : "spawn-resources-cause-world";
        m.Filters[placeId] = new Filter { Id = placeId, Type = "all", Children = [SynthMatch(), Synth(new Filter { Type = "cause", Cause = "world" })] };

        var spawnIds = spawns.Select(s => s.Id).ToHashSet();
        spawnIds.Add("spawns");   // block=never now sits on the shared spawns union (TeamsGenerator)
        m.ApplyRules.RemoveAll(r => r.BlockFilter == "never" && spawnIds.Contains(r.RegionId));
        m.ApplyRules.Add(new ApplyRule
        {
            BlockBreakFilter = breakId, BlockPlaceFilter = placeId, RegionId = "spawns", Message = "You may not edit spawn!",
        });
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────
    private readonly record struct Box(double MinX, double MinZ, double MaxX, double MaxZ);

    private static bool Covers(Box b, int x, int z) => x >= b.MinX && x <= b.MaxX && z >= b.MinZ && z <= b.MaxZ;

    // Spawn-protection regions = rectangles referenced by an apply rule whose enter filter is only-<team>
    // (the spawn enter rule; wool rooms use not-<team>).
    private static List<(string Id, Box Box)> SpawnProtections(MapXml m)
    {
        var seen = new HashSet<string>();
        var result = new List<(string, Box)>();
        foreach (var rule in m.ApplyRules)
            if (rule.EnterFilter.StartsWith("only-") && seen.Add(rule.RegionId)
                && m.Regions.TryGetValue(rule.RegionId, out var r) && r.Type == "rectangle"
                && r.MinX is { } mnx && r.MinZ is { } mnz && r.MaxX is { } mxx && r.MaxZ is { } mxz)
                result.Add((rule.RegionId, new Box(mnx, mnz, mxx, mxz)));
        return result;
    }

    private static void AddMaterialFilter(MapXml m, string id, string material)
    {
        if (!m.Filters.ContainsKey(id)) m.Filters[id] = new Filter { Id = id, Type = "material", Material = material };
    }
}
