using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The standard CTW boilerplate that nearly every corpus map carries but the intent generator doesn't
/// author by hand: keep-on-death / drop-on-death item rules derived from the spawn kit, the shared
/// golden-apple kill-reward include, and hunger depletion off. Applied to a generated map <b>at export</b>
/// (not persisted), so corpus-map exports — which have their own hand-authored versions we don't
/// round-trip — are left untouched.
/// <para>Derivation (grounded in the corpus, N=199): <b>itemkeep</b> = every non-armor kit item (keep your
/// loadout + blocks + golden apple), <b>toolrepair</b> = the kit's tools/weapons, <b>itemremove</b> = the
/// kit's armor (the kit re-applies team-coloured armor, so it's dropped rather than kept).</para>
/// </summary>
public static class CtwStandards
{
    /// <summary>The shared kill-reward include (golden apple on kill) defined on the server — present in
    /// ~97% of corpus maps.</summary>
    public const string KillRewardInclude = "gapple-kill-reward";

    // A durable tool/weapon, identified by the material's last word (e.g. "iron sword" → sword).
    private static readonly HashSet<string> ToolWords =
        new() { "sword", "bow", "pickaxe", "axe", "spade", "shovel", "shears", "hoe", "rod" };

    // Surface block id (1.8 numeric) → the items it yields that should be removed, so players can't farm
    // the decoration off the terrain (seeds from grass, apples/saplings from leaves, string from cobweb,
    // flint from gravel, …). Generous by design — removing an item that never drops is a harmless no-op,
    // and the corpus shows authors do this selectively per the surface palette. Material names match the
    // corpus's <itemremove> entries. See docs / the surface-layer correlation.
    private static readonly Dictionary<int, string[]> SurfaceDrops = new()
    {
        [30]  = ["string"],                 // cobweb
        [31]  = ["seeds", "long grass"],    // tall grass
        [175] = ["double plant", "seeds"],  // double plant (tall grass/fern/flowers)
        [18]  = ["sapling", "apple"],       // leaves (oak drops apples)
        [161] = ["sapling"],                // leaves2 (acacia/dark oak)
        [6]   = ["sapling"],                // sapling
        [38]  = ["red rose"],               // red flower
        [37]  = ["yellow flower"],          // dandelion
        [13]  = ["flint", "gravel"],        // gravel
        [39]  = ["brown mushroom"],         // brown mushroom
        [40]  = ["red mushroom"],           // red mushroom
        [73]  = ["redstone"],               // redstone ore
        [74]  = ["redstone"],               // glowing redstone ore
        [83]  = ["sugar cane"],             // sugar cane (reeds)
        [81]  = ["cactus"],                 // cactus
        [103] = ["melon", "melon seeds"],   // melon block
        [86]  = ["pumpkin"],                // pumpkin
        [106] = ["vine"],                   // vine
    };

    private static bool IsTool(string material)
    {
        var parts = material.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && ToolWords.Contains(parts[^1]);
    }

    /// <inheritdoc cref="Apply(MapXml, IReadOnlySet{int}?)"/>
    public static void Apply(MapXml m) => Apply(m, null);

    /// <summary>Add the standard CTW item/tool rules + kill-reward include + hunger-off to a generated map.
    /// Replaces the lists, so re-applying is safe. When <paramref name="surfaceBlockIds"/> is supplied (the
    /// block ids present on the map's top surface), <c>itemremove</c> is <b>extended</b> with the terrain
    /// drops those blocks yield (on top of the kit armor).</summary>
    public static void Apply(MapXml m, IReadOnlySet<int>? surfaceBlockIds)
    {
        if (m.Kits.FirstOrDefault() is { } kit)
        {
            var items = kit.Items.Select(i => i.Material).Where(s => s.Length > 0).ToList();
            m.ItemKeep = items.Distinct().ToList();
            m.ToolRepair = items.Where(IsTool).Distinct().ToList();
            m.ItemRemove = kit.Armor.Select(a => a.Material).Where(s => s.Length > 0).Distinct().ToList();
        }
        if (surfaceBlockIds is { Count: > 0 })
        {
            var drops = surfaceBlockIds.Where(SurfaceDrops.ContainsKey).SelectMany(id => SurfaceDrops[id]);
            m.ItemRemove = m.ItemRemove.Concat(drops).Distinct().ToList();
        }
        if (!m.Includes.Contains(KillRewardInclude)) m.Includes.Insert(0, KillRewardInclude);
        m.HungerDepletion = "off";
    }
}
