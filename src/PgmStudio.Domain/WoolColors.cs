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

    // Keyed on the separator-normalized slug (spaces/dashes already collapsed to '_'). Two kinds:
    // modern/display spellings of a real wool colour, and chat colours (the team palette) that have no wool
    // of their own and coerce to the nearest one.
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["light_gray"] = "silver",                       // modern name for damage 8
        ["gold"] = "orange", ["aqua"] = "cyan",          // chat colours → nearest wool
        ["dark_aqua"] = "cyan", ["light_purple"] = "purple", ["dark_purple"] = "purple",
        ["dark_red"] = "red", ["dark_green"] = "green", ["dark_blue"] = "blue", ["dark_gray"] = "gray",
    };

    private static readonly Dictionary<string, int> ColorToWoolDamage =
        WoolDamageToColor.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Wool colour for a damage nibble; unknown damage falls back to <c>white</c>.</summary>
    public static string WoolColor(int damage) => WoolDamageToColor.GetValueOrDefault(damage, "white");

    /// <summary>Wool/stained-block damage nibble for a colour slug; unknown falls back to <c>0</c> (white).
    /// The same 0–15 scale applies to wool (35), stained clay (159), and stained glass + panes (95/160).</summary>
    public static int WoolDamage(string color) => ColorToWoolDamage.GetValueOrDefault(Normalize(color), 0);

    /// <summary>Normalize a wool colour name to its canonical slug: lowercased, spaces/dashes to <c>_</c>,
    /// then display-name and chat-colour aliases mapped to their wool equivalent (e.g. "Dark Aqua" → cyan).</summary>
    public static string Normalize(string color)
    {
        var key = color.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return Aliases.GetValueOrDefault(key, key);
    }
}
