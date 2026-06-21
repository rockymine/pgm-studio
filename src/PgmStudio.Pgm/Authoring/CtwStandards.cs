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

    private static bool IsTool(string material)
    {
        var parts = material.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && ToolWords.Contains(parts[^1]);
    }

    /// <summary>Add the standard CTW item/tool rules + kill-reward include + hunger-off to a generated map.
    /// Replaces the four lists, so re-applying is safe.</summary>
    public static void Apply(MapXml m)
    {
        if (m.Kits.FirstOrDefault() is { } kit)
        {
            var items = kit.Items.Select(i => i.Material).Where(s => s.Length > 0).ToList();
            m.ItemKeep = items.Distinct().ToList();
            m.ToolRepair = items.Where(IsTool).Distinct().ToList();
            m.ItemRemove = kit.Armor.Select(a => a.Material).Where(s => s.Length > 0).Distinct().ToList();
        }
        if (!m.Includes.Contains(KillRewardInclude)) m.Includes.Insert(0, KillRewardInclude);
        m.HungerDepletion = "off";
    }
}
