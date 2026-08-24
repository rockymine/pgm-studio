using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Export-time enrichment that adds <c>&lt;renewables&gt;</c> for a generated map's resource blocks (iron,
/// gold, diamond) that sit <b>inside a team spawn</b> — the safe, intended CTW economy (spawn ore regrows
/// after it's mined). Ore scanned <i>elsewhere</i> is left as-is: its intent is ambiguous (decoration vs
/// resource), so we don't force a renewable on it. PGM snapshots the original world, so the reused spawns
/// union only has to <i>contain</i> the ore's (x,z).
/// <para>The ore it finds in a spawn is <b>returned</b> rather than acted on: the spawn's block protection
/// is one rule with one owner (<see cref="SpawnOreProtection"/>), stated once over everything that lives
/// there, because a second caller replacing that rule is the first caller silently losing.</para>
/// </summary>
public static class ResourceRenewables
{
    private const int AvoidPlayers = 2;

    // Resource block ids in a fixed output order; material/slug derive from the type ("iron_block" →
    // material "iron block", slug "iron").
    private static readonly string[] Order = ["iron_block", "gold_block", "diamond_block"];

    /// <summary>Adds a renewable per resource kind living in a spawn, and answers which kinds those were so
    /// the caller can state the spawn's block protection over all of them at once.</summary>
    public static List<SpawnOreProtection.Ore> Apply(MapXml m, IReadOnlyList<(string Type, int X, int Y, int Z)> resourceBlocks)
    {
        if (resourceBlocks.Count == 0) return [];

        var spawns = SpawnOreProtection.Protections(m);
        if (spawns.Count == 0) return [];   // no spawn protection to anchor a renewable to

        var inSpawn = new List<SpawnOreProtection.Ore>();
        var added = false;
        foreach (var type in Order)
        {
            var blocks = resourceBlocks.Where(b => b.Type == type).Select(b => (b.X, b.Y, b.Z)).ToList();
            if (blocks.Count == 0) continue;
            // Only ore that sits in a spawn is a renewable candidate; ignore the rest (ambiguous intent).
            if (!blocks.Any(b => spawns.Any(s => s.Box.Covers(b.X, b.Z)))) continue;

            var slug = type.Replace("_block", "");   // iron / gold / diamond
            var material = type.Replace('_', ' ');    // iron block / gold block / diamond block

            if (!added) { AddMaterialFilter(m, "only-air", "air"); added = true; }
            AddMaterialFilter(m, $"only-{slug}", material);
            inSpawn.Add(new SpawnOreProtection.Ore(slug, material));

            m.Renewables.Add(new Renewable
            {
                RegionId = "spawns", RenewFilter = $"only-{slug}", ReplaceFilter = "only-air", AvoidPlayers = AvoidPlayers,
            });
        }

        return inSpawn;
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────

    private static void AddMaterialFilter(MapXml m, string id, string material)
    {
        if (!m.Filters.ContainsKey(id)) m.Filters[id] = new Filter { Id = id, Type = "material", Material = material };
    }
}
