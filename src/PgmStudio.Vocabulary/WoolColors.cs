namespace PgmStudio.Vocabulary;

/// <summary>
/// The sixteen dyes a wool objective can be, and the swatch each one draws as — a closed set because four
/// parties spell it: the plan validator refuses a word outside it, the compiler picks from it, two renderers
/// colour a marker by it, and the plan editor offers it.
///
/// <para>The names are the underscore form PGM's <c>DyeColors</c> resolves and the studio's <c>map.xml</c>
/// carries. <c>silver</c> is 1.8's name for light gray; PGM accepts <c>light_gray</c> for the same dye, so
/// <see cref="Normalize"/> folds it, along with the space-separated spelling the Bukkit tables use.</para>
/// </summary>
public static class WoolColors
{
    /// <summary>The dyes in <c>org.bukkit.DyeColor</c>'s data-value order — white 0 through black 15, which is
    /// also the order a picker reads.</summary>
    public static readonly string[] All =
    [
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "silver", "cyan", "purple", "blue", "brown", "green", "red", "black",
    ];

    /// <summary>Each dye's swatch, from Bukkit's own <c>DyeColor</c> table, so a wool draws as the block a
    /// player will stand in front of rather than as an approximation of it.</summary>
    public static readonly Dictionary<string, string> Swatch = new()
    {
        ["white"] = "#FFFFFF", ["orange"] = "#D87F33", ["magenta"] = "#B24CD8", ["light_blue"] = "#6699D8",
        ["yellow"] = "#E5E533", ["lime"] = "#7FCC19", ["pink"] = "#F27FA5", ["gray"] = "#4C4C4C",
        ["silver"] = "#999999", ["cyan"] = "#4C7F99", ["purple"] = "#7F3FB2", ["blue"] = "#334CB2",
        ["brown"] = "#664C33", ["green"] = "#667F33", ["red"] = "#993333", ["black"] = "#191919",
    };

    /// <summary>What a name that is not a dye draws as: amber, which is in no swatch above, so a colour
    /// nothing recognised is visible as one rather than passing for a dye.</summary>
    public const string UnknownSwatch = "#fbbf24";

    /// <summary>The canonical spelling of a colour name — lowercased, spaces folded to underscores, and
    /// <c>light_gray</c> read as the <c>silver</c> the wire carries. Returns the input's canonical form
    /// whether or not it names a dye, so a caller can normalise before asking.</summary>
    public static string Normalize(string? name)
    {
        var slug = (name ?? "").Trim().Replace(' ', '_').ToLowerInvariant();
        return slug == "light_gray" ? "silver" : slug;
    }

    /// <summary>Whether the name is one of the sixteen, in any accepted spelling.</summary>
    public static bool IsColor(string? name) => Swatch.ContainsKey(Normalize(name));

    /// <summary>A dye's swatch, or <see cref="UnknownSwatch"/> for a name that is not one.</summary>
    public static string SwatchOf(string? name)
        => Swatch.TryGetValue(Normalize(name), out var hex) ? hex : UnknownSwatch;

    /// <summary>A dye's swatch as a packed RGB int, for the raster renderers.</summary>
    public static int RgbOf(string? name) => Convert.ToInt32(SwatchOf(name)[1..], 16);

    /// <summary>The title-cased label a picker shows — <c>Light Blue</c> for <c>light_blue</c>.</summary>
    public static string Label(string name)
        => string.Join(' ', Normalize(name).Split('_').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
