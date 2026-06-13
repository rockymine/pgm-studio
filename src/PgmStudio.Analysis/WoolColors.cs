namespace PgmStudio.Analysis;

/// <summary>Minecraft wool/dye colour tables (port of minecraft/wool.py).</summary>
public static class WoolColors
{
    public static readonly Dictionary<int, string> WoolDamageToColor = new()
    {
        [0] = "white", [1] = "orange", [2] = "magenta", [3] = "light_blue",
        [4] = "yellow", [5] = "lime", [6] = "pink", [7] = "gray",
        [8] = "silver", [9] = "cyan", [10] = "purple", [11] = "blue",
        [12] = "brown", [13] = "green", [14] = "red", [15] = "black",
    };

    // Dye (1.8 ink-sack) damage is on a different scale, mapped to the same wool slugs.
    public static readonly Dictionary<int, string> DyeDamageToColor = new()
    {
        [0] = "black", [1] = "red", [2] = "green", [3] = "brown",
        [4] = "blue", [5] = "purple", [6] = "cyan", [7] = "silver",
        [8] = "gray", [9] = "pink", [10] = "lime", [11] = "yellow",
        [12] = "light_blue", [13] = "magenta", [14] = "orange", [15] = "white",
    };

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["light_gray"] = "silver", ["light gray"] = "silver", ["light blue"] = "light_blue",
    };

    /// <summary>Normalize a wool colour name to its canonical slug.</summary>
    public static string Normalize(string color)
    {
        var key = color.Trim().ToLowerInvariant();
        return Aliases.TryGetValue(key, out var a) ? a : key.Replace(' ', '_').Replace('-', '_');
    }
}
