namespace PgmStudio.Pgm.Render;

using PgmStudio.Vocabulary;

/// <summary>
/// The role/zone colours <see cref="PlanBoardSvg"/> and <see cref="PlanBoardPng"/> both draw from — public,
/// unlike the geometry in <see cref="PlanBoardScene"/>, because a colour that only the two renderers can name
/// is a colour a test proving the legibility bar cannot check against the real values.
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

    /// <summary>A build zone's own colour — a gap open to building from the first tick. Deliberately not a
    /// shade of blue: blue is the one colour a reader brings a fixed meaning for (water) whether or not this
    /// render intends it, and a zone that is not water must not wear its colour. Distinguished
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

    /// <summary>The board's key: each role swatch, then the two zone kinds, in the order a legend lists them. The
    /// PNG appends it under its raster, and a page showing board SVGs draws it once beside them, so the words and
    /// the colours a reader is given are the ones the renderers paint with.</summary>
    public static readonly IReadOnlyList<(string Label, int Rgb, bool Hatched)> Key =
    [
        ("Hub", 0xa78bfa, false), ("Spawn", 0x34d399, false), ("Wool", 0xfbbf24, false),
        ("Front line", 0xfb923c, false), ("Other", 0x64748b, false),
        ("Build zone", BuildZoneRgb, false), ("Water lane", WaterLaneRgb, true),
    ];

    /// <summary>A packed swatch as CSS hex.</summary>
    public static string Hex(int rgb) => $"#{rgb:x6}";

    /// <summary>A wool marker's swatch, from the one dye table (<see cref="WoolColors"/>) the validator, the
    /// compiler and the editor all read, so a marker draws as the block it stamps.</summary>
    public static string WoolColor(string? color) => WoolColors.SwatchOf(color);

    public static int WoolRgb(string? color) => WoolColors.RgbOf(color);
}
