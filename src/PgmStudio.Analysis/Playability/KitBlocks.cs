namespace PgmStudio.Analysis.Playability;

/// <summary>
/// Classifies a spawn-kit item material as a placeable building block (counts toward the bridging
/// budget) vs. a tool / weapon / consumable / projectile (does not). Materials are PGM/Bukkit names
/// normalised to lower-case with spaces. This covers the building blocks CTW kits actually grant;
/// the exhaustive answer is the full Bukkit 1.8 material registry (legacy id &lt; 256, minus
/// hand-unplaceable technical blocks), which a production version should key off directly.
/// </summary>
public static class KitBlocks
{
    private static readonly HashSet<string> Placeable = new(StringComparer.Ordinal)
    {
        // wood / planks / logs
        "wood", "log", "log 2", "wood plank", "wood planks", "plank", "planks", "wood double step",
        // stone family
        "stone", "cobblestone", "mossy cobblestone", "smooth brick", "stone brick", "stone bricks",
        "brick", "brick block", "stonebrick", "smooth stairs",
        // earth / terrain
        "dirt", "grass", "grass block", "sand", "sandstone", "red sandstone", "gravel", "clay",
        "soul sand", "netherrack", "mycel", "mycelium", "end stone", "endstone", "snow block", "ice",
        "packed ice", "podzol",
        // hardened / stained clay + glass + wool + carpet
        "hard clay", "hardened clay", "stained clay", "stained glass", "glass", "thin glass",
        "glass pane", "stained glass pane", "wool", "carpet",
        // nether / quartz
        "nether brick", "nether brick block", "quartz", "quartz block",
        // mineral / decorative solid blocks
        "iron block", "gold block", "diamond block", "emerald block", "lapis block", "coal block",
        "redstone block", "obsidian", "glowstone", "sea lantern", "prismarine", "sponge",
        "bookshelf", "pumpkin", "melon block", "hay block", "slime block", "bedrock", "wood step",
        "step", "double step", "leaves", "leaves 2",
    };

    public static bool IsPlaceable(string normalizedMaterial) => Placeable.Contains(normalizedMaterial);
}
