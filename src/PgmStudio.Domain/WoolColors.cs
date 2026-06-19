namespace PgmStudio.Domain;

/// <summary>
/// Minecraft 1.8 wool/dye game data — damage values ⇄ colour names — plus colour-name normalization.
/// Damage 8 is stored as <c>silver</c> (PGM's name; modern Minecraft calls it <c>light_gray</c>).
/// </summary>
public static class WoolColors
{
    /// <summary>Wool block (id 35) damage → colour slug.</summary>
    public static readonly IReadOnlyDictionary<int, string> WoolDamageToColor = new Dictionary<int, string>
    {
        [0] = "white", [1] = "orange", [2] = "magenta", [3] = "light_blue",
        [4] = "yellow", [5] = "lime", [6] = "pink", [7] = "gray",
        [8] = "silver", [9] = "cyan", [10] = "purple", [11] = "blue",
        [12] = "brown", [13] = "green", [14] = "red", [15] = "black",
    };

    /// <summary>Dye (1.8 ink-sack) damage → colour slug — a different, roughly inverted scale.</summary>
    public static readonly IReadOnlyDictionary<int, string> DyeDamageToColor = new Dictionary<int, string>
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

    /// <summary>Wool colour for a damage nibble; unknown damage falls back to <c>white</c>.</summary>
    public static string WoolColor(int damage) => WoolDamageToColor.GetValueOrDefault(damage, "white");

    /// <summary>Normalize a wool colour name to its canonical slug.</summary>
    public static string Normalize(string color)
    {
        var key = color.Trim().ToLowerInvariant();
        return Aliases.TryGetValue(key, out var a) ? a : key.Replace(' ', '_').Replace('-', '_');
    }
}
