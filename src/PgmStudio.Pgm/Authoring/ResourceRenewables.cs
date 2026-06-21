using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Export-time enrichment that adds <c>&lt;renewables&gt;</c> for a generated map's resource blocks (iron,
/// gold, diamond) so mined ore regrows (the standard CTW economy), keeping each renewable region tight
/// (only around its blocks) for performance. PGM snapshots the original world, so a region only has to
/// <i>contain</i> its blocks' (x,z) — a flat region is enough.
/// <para>Per resource type: if all of its blocks sit in the team spawns, the spawns union is reused as the
/// renewable region; otherwise a rectangle per spatial cluster, unioned when there's more than one. When
/// any resource lives in the spawns, the spawn's <c>block=never</c> protection is relaxed once to "only
/// break those resources / only the renewable (<c>cause=world</c>) may place them" (the corpus pattern).</para>
/// </summary>
public static class ResourceRenewables
{
    private const int AvoidPlayers = 2;
    private const int ClusterGap = 8;   // blocks within this Chebyshev gap (x & z) are one deposit

    // Resource block ids in a fixed output order; material/slug derive from the type ("iron_block" →
    // material "iron block", slug "iron").
    private static readonly string[] Order = ["iron_block", "gold_block", "diamond_block"];

    public static void Apply(MapXml m, IReadOnlyList<(string Type, int X, int Y, int Z)> resourceBlocks)
    {
        if (resourceBlocks.Count == 0) return;

        var spawns = SpawnProtections(m);
        var inSpawn = new List<(string Slug, string Material)>();

        var added = false;
        foreach (var type in Order)
        {
            var blocks = resourceBlocks.Where(b => b.Type == type).Select(b => (b.X, b.Y, b.Z)).ToList();
            if (blocks.Count == 0) continue;
            var slug = type.Replace("_block", "");   // iron / gold / diamond
            var material = type.Replace('_', ' ');    // iron block / gold block / diamond block

            if (!added) { AddMaterialFilter(m, "only-air", "air"); added = true; }
            AddMaterialFilter(m, $"only-{slug}", material);

            var allInSpawns = spawns.Count > 0 && blocks.All(b => spawns.Any(s => Covers(s.Box, b.X, b.Z)));
            string region;
            if (allInSpawns) { region = "spawns"; inSpawn.Add((slug, material)); }
            else region = ClusterRegion(m, blocks, slug);

            m.Renewables.Add(new Renewable
            {
                RegionId = region, RenewFilter = $"only-{slug}", ReplaceFilter = "only-air", AvoidPlayers = AvoidPlayers,
            });
        }

        if (inSpawn.Count > 0) RelaxSpawnProtection(m, spawns, inSpawn);
    }

    // ── iron-in-spawns: reuse spawns + relax protection ───────────────────────────────
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
        m.ApplyRules.RemoveAll(r => r.BlockFilter == "never" && spawnIds.Contains(r.RegionId));
        m.ApplyRules.Add(new ApplyRule
        {
            BlockBreakFilter = breakId, BlockPlaceFilter = placeId, RegionId = "spawns", Message = "You may not edit spawn!",
        });
    }

    // ── resource elsewhere: tight per-cluster region ──────────────────────────────────
    private static string ClusterRegion(MapXml m, IReadOnlyList<(int X, int Y, int Z)> blocks, string slug)
    {
        var clusters = Cluster(blocks);
        var rectIds = new List<string>();
        for (var i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            var id = clusters.Count == 1 ? $"{slug}-renewable" : $"{slug}-renewable-{i + 1}";
            int minX = c.Min(b => b.X), minZ = c.Min(b => b.Z), maxX = c.Max(b => b.X), maxZ = c.Max(b => b.Z);
            m.Regions[id] = new Region
            {
                Id = id, Type = "rectangle", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
                Bounds2d = Bounds2d.Of(minX, minZ, maxX, maxZ),
            };
            rectIds.Add(id);
        }
        if (rectIds.Count == 1) return rectIds[0];
        m.Regions[$"{slug}-renewable"] = new Region { Id = $"{slug}-renewable", Type = "union", Children = rectIds };
        return $"{slug}-renewable";
    }

    // Connected-components: blocks within ClusterGap (Chebyshev, x & z) are one deposit.
    private static List<List<(int X, int Y, int Z)>> Cluster(IReadOnlyList<(int X, int Y, int Z)> blocks)
    {
        var parent = Enumerable.Range(0, blocks.Count).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (var i = 0; i < blocks.Count; i++)
            for (var j = i + 1; j < blocks.Count; j++)
                if (Math.Abs(blocks[i].X - blocks[j].X) <= ClusterGap && Math.Abs(blocks[i].Z - blocks[j].Z) <= ClusterGap)
                    parent[Find(i)] = Find(j);
        return blocks.Select((b, i) => (b, root: Find(i)))
            .GroupBy(t => t.root).Select(g => g.Select(t => t.b).ToList()).ToList();
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
