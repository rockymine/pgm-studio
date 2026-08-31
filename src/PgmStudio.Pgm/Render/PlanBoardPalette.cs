namespace PgmStudio.Pgm.Render;

using PgmStudio.Vocabulary;

/// <summary>
/// The role/zone colours <see cref="PlanBoardSvg"/> and <see cref="PlanBoardPng"/> both draw from — public,
/// unlike the geometry in <see cref="PlanBoardScene"/>, because a colour that only the two renderers can name
/// is a colour a test proving <c>B95</c>'s legibility bar cannot check against the real values.
/// </summary>
public static class PlanBoardPalette
{
    public static string PieceColor(string id) =>
        id.StartsWith("hub") ? "#a78bfa"
        : id.StartsWith("spawn") ? "#34d399"
        : id.StartsWith("wool") ? "#fbbf24"
        : id.StartsWith("frontline") ? "#fb923c"
        : "#64748b";

    /// <summary>The same swatches as <see cref="PieceColor"/>, packed <c>0xRRGGBB</c> for the raster path.</summary>
    public static int PieceRgb(string id) =>
        id.StartsWith("hub") ? 0xa78bfa
        : id.StartsWith("spawn") ? 0x34d399
        : id.StartsWith("wool") ? 0xfbbf24
        : id.StartsWith("frontline") ? 0xfb923c
        : 0x64748b;

    /// <summary>The role word a piece id's own colour stands for, in the order a legend lists them — read off
    /// the same prefix test <see cref="PieceColor"/> uses, so the key can never name a role the picture does
    /// not actually draw that way.</summary>
    public static string RoleName(string id) =>
        id.StartsWith("hub") ? "hub"
        : id.StartsWith("spawn") ? "spawn"
        : id.StartsWith("wool") ? "wool"
        : id.StartsWith("frontline") ? "frontline"
        : "other";

    /// <summary>A build zone's own colour — a gap open to building from the first tick. Deliberately not a
    /// shade of blue: blue is the one colour a reader brings a fixed meaning for (water) whether or not this
    /// render intends it, and a zone that is not water must not wear its colour (<c>B95</c>). Distinguished
    /// from every piece role above by hue, not merely by the shade/opacity/dash a still image loses.</summary>
    public const string BuildZoneColor = "#f472b6";
    public const int BuildZoneRgb = 0xf472b6;

    /// <summary>A water lane's own colour — kept blue, because a lane genuinely does open into water-lane
    /// crossing rules and blue is the one hue this render can spend on that without inventing a new
    /// convention. Distinguished from a build zone by hue (not merely shade) and, in <see cref="PlanBoardSvg"/>
    /// and <see cref="PlanBoardPng"/>, by a diagonal hatch a flat colour cannot lose to a screenshot or a
    /// greyscale print.</summary>
    public const string WaterLaneColor = "#2563eb";
    public const int WaterLaneRgb = 0x2563eb;

    /// <summary>A wool marker's swatch, from the one dye table (<see cref="WoolColors"/>) the validator, the
    /// compiler and the editor all read, so a marker draws as the block it stamps.</summary>
    public static string WoolColor(string? color) => WoolColors.SwatchOf(color);

    public static int WoolRgb(string? color) => WoolColors.RgbOf(color);
}
