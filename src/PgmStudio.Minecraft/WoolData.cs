namespace PgmStudio.Minecraft;

/// <summary>
/// Minecraft 1.8 wool/dye game data — damage values ⇄ colour names. Damage 8 is stored as
/// <c>silver</c> (PGM's name; modern Minecraft calls it <c>light_gray</c>). Port of
/// <c>minecraft/wool.py</c>.
/// </summary>
public static class WoolData
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

    /// <summary>Wool colour for a damage nibble; unknown damage falls back to <c>white</c> (matches Python).</summary>
    public static string WoolColor(int damage) => WoolDamageToColor.GetValueOrDefault(damage, "white");
}
