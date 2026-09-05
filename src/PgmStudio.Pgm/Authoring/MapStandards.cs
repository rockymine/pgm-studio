using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The standard boilerplate that nearly every corpus map carries but the intent generator doesn't author by
/// hand: keep-on-death / drop-on-death item rules derived from the spawn kit, the shared golden-apple
/// kill-reward include, hunger depletion off, and what a destroy objective drops. Applied to a generated map
/// <b>at export</b> (not persisted), so corpus-map exports — which have their own hand-authored versions we
/// don't round-trip — are left untouched.
///
/// <para><b>None of it is about what a map is played for.</b> A kit's armour is dropped and its loadout kept
/// whether the map's goal is carried or broken, and hunger is off on all three; the one part that reads the
/// objectives at all — <see cref="DestroyDrops"/> — reads them to answer a question every map is asked and
/// most answer with nothing. So the standards are the map's, not the gamemode's, which is what the name
/// says.</para>
///
/// <para>Derivation (grounded in the corpus, N=199): <b>itemkeep</b> = every non-armor kit item (keep your
/// loadout + blocks + golden apple), <b>toolrepair</b> = the kit's tools/weapons, <b>itemremove</b> = the
/// kit's armor (the kit re-applies team-coloured armor, so it's dropped rather than kept) plus the terrain's
/// own drops and every material a destroy objective ever is.</para>
/// </summary>
public static class MapStandards
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

    /// <summary>Add the standard item/tool rules + kill-reward include + hunger-off to a generated map.
    /// Replaces the lists, so re-applying is safe. When <paramref name="surfaceBlockIds"/> is supplied (the
    /// block ids present on the map's top surface), <c>itemremove</c> is <b>extended</b> with the terrain
    /// drops those blocks yield (on top of the kit armor).</summary>
    public static void Apply(MapXml m, IReadOnlySet<int>? surfaceBlockIds)
    {
        if (m.Kits.FirstOrDefault() is { } kit)
        {
            // The spawn kit's build blocks are the stacked items (wood, the team-coloured accent block);
            // tools/weapons/consumables come as a single item. Keep the WHOLE loadout on death — single items
            // AND blocks (docs/pgm/template.xml keeps `wood` + `stained clay` in <itemkeep>); farming *placed*
            // blocks is prevented by the block-drops chance=0 rule below, not by removing them on death (which
            // would leave players without building material). Only the team armour is dropped — the kit
            // re-applies it — so <itemremove> is just the armour (+ the terrain drops added later).
            var keep = kit.Items.Where(i => i.Amount <= 1).Select(i => i.Material).Where(s => s.Length > 0).ToList();
            var blocks = kit.Items.Where(i => i.Amount > 1).Select(i => i.Material).Where(s => s.Length > 0).Distinct().ToList();
            m.ItemKeep = keep.Concat(blocks).Distinct().ToList();
            m.ToolRepair = keep.Where(IsTool).Distinct().ToList();
            m.ItemRemove = kit.Armor.Select(a => a.Material).Where(s => s.Length > 0).Distinct().ToList();

            // The place-and-break trick: a kit block breaks into nothing (a single drop at chance 0 replaces
            // its natural drop), so players can't mine fresh building material off what they place.
            if (blocks.Count > 0)
                m.BlockDropRules.Add(new BlockDropRule
                {
                    FilterMaterials = blocks,
                    Items = [new BlockDropItem { Material = blocks[0], Chance = 0.0 }],
                });

            // Default kill-reward: a stack of building blocks per kill (the kit's blocks — wood + the
            // team-coloured block) on top of the golden-apple include. Amounts match the corpus norm
            // (~24 blocks across ~2 items: a neutral block at 16, a team-coloured one at 8).
            var rewardItems = kit.Items
                .Where(i => i.Amount > 1 && i.Material.Length > 0)
                .Select(i => new KillRewardItem { Material = i.Material, TeamColor = i.TeamColor, Amount = i.TeamColor ? 8 : 16 })
                .ToList();
            if (rewardItems.Count > 0) m.KillRewards = [new KillReward { Items = rewardItems }];
        }
        if (surfaceBlockIds is { Count: > 0 })
        {
            var drops = surfaceBlockIds.Where(SurfaceDrops.ContainsKey).SelectMany(id => SurfaceDrops[id]);
            m.ItemRemove = m.ItemRemove.Concat(drops).Distinct().ToList();
        }
        m.ItemRemove = m.ItemRemove.Concat(DestroyDrops(m)).Distinct().ToList();
        if (!m.Includes.Contains(KillRewardInclude)) m.Includes.Insert(0, KillRewardInclude);
        m.HungerDepletion = "off";
    }

    /// <summary>Every material a destroy objective <b>ever is</b> — what each monument and core starts as, and
    /// what every rung of the mode ladder turns it into — so none of it survives being broken as an item.
    ///
    /// <para><b>This is what stops a monument being rebuilt, and it is the only thing that can stop a core
    /// being plugged.</b> Breaking obsidian with the diamond pick a destroy kit hands out drops obsidian, and
    /// PGM lets the owning team place it back (<c>repairable</c> defaults true); a core has no
    /// <c>repairable</c> at all, and a block put back into its casing passes every check PGM makes. Cancelling
    /// the item at its spawn is upstream of both: the block never becomes something anyone can pick up.</para>
    ///
    /// <para>The corpus is near-unanimous. 309 of the 313 DTM/DTC maps in <c>CommunityMaps</c> and
    /// <c>PublicMaps</c> carry an <c>&lt;item-remove&gt;</c>, 202 of them list obsidian, and 190 list every
    /// material their objectives ever are — the ladder's included, which is why this reads the modes and not
    /// just the starting block. <c>alpine_mining_ii</c> removes obsidian, beacon and coal block and states
    /// <c>repairable="false"</c> besides.</para></summary>
    private static IEnumerable<string> DestroyDrops(MapXml m)
    {
        foreach (var destroyable in m.Destroyables)
            foreach (var material in destroyable.Materials.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
                if (Named(material) is { } name) yield return name;

        // A core states no material when it is obsidian, which is what CoreModule falls back to.
        foreach (var core in m.Cores)
            yield return Named(core.Material) ?? "obsidian";

        foreach (var mode in m.Modes)
            if (Named(mode.Material) is { } name) yield return name;
    }

    /// <summary>A match pattern as the item name it removes: the block, without the data nibble a
    /// <c>&lt;item&gt;</c> does not need to name a dropped stack. Empty and whitespace answer null.</summary>
    private static string? Named(string material)
    {
        var name = material.Split(':')[0].Trim();
        return name.Length > 0 ? name : null;
    }
}
