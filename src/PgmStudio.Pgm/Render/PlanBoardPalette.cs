namespace PgmStudio.Pgm.Render;

using PgmStudio.Vocabulary;

/// <summary>
/// The four inks <see cref="PlanBoardSvg"/> and <see cref="PlanBoardPng"/> both draw from: ground grey for every
/// piece that is only terrain, one accent for each of the two kinds of room (spawn, wool) and one for the build
/// zone. The values are the plan figures' on white paper; <see cref="PlanInk.FillCss"/> and
/// <see cref="PlanInk.EdgeCss"/> read the page's <c>--board-*</c> tokens first, so a dark page re-states the same
/// four inks without the picture changing. Public, unlike the geometry in <see cref="PlanBoardScene"/>, so a
/// test can hold the legibility bar against the real values.
/// </summary>
public static class PlanBoardPalette
{
    /// <summary>One ink: the token stem a page restates it under, and its packed <c>0xRRGGBB</c> fill and edge
    /// on a white ground.</summary>
    public sealed record PlanInk(string Name, int Fill, int Edge)
    {
        public string FillCss => $"var(--board-{Name},{Hex(Fill)})";
        public string EdgeCss => $"var(--board-{Name}-edge,{Hex(Edge)})";
    }

    public static readonly PlanInk Ground = new("ground", 0xe7eaee, 0xa8b0b9);
    /// <summary>The spawn and wool-room inks restate the plan editor's <c>--canvas-role-spawn</c> and
    /// <c>--canvas-role-wool-room</c>: the edge is that colour and the fill is 30% of it over white. The tokens
    /// derive the same pair from the editor's, so these values only matter where no page supplies them.</summary>
    public static readonly PlanInk Spawn = new("spawn", 0xddd7f3, 0x8f7bd6);
    public static readonly PlanInk WoolRoom = new("wool", 0xc5e7d5, 0x3fae74);

    /// <summary>A build zone: an outline in one colour, with no fill of its own beyond a tint of that colour.</summary>
    public static readonly PlanInk Zone = new("zone", 0x0072b2, 0x0072b2);

    /// <summary>The grey of the symmetry axis, and of the iron pips drawn on a board.</summary>
    public const int AxisRgb = 0x9aa1a9;

    /// <summary>The paper the raster is drawn on.</summary>
    public const int PaperRgb = 0xffffff;

    /// <summary>The board role of a piece: its authored role when that is a room, otherwise what its id names
    /// — the hub, the front line, or a corridor leading to a room (an approach) — and <c>other</c> for the rest.</summary>
    public static string RoleOf(string planRole, string id) =>
        planRole == PlanRoles.Spawn ? BoardRoles.Spawn
        : planRole == PlanRoles.WoolRoom ? BoardRoles.Wool
        : id.StartsWith("hub") ? BoardRoles.Hub
        : id.StartsWith("frontline") ? BoardRoles.Frontline
        : id.StartsWith("wool") || id.StartsWith("spawn") ? BoardRoles.Approach
        : BoardRoles.Other;

    /// <summary>The ink a board role is drawn in.</summary>
    public static PlanInk InkOf(string boardRole) => boardRole switch
    {
        BoardRoles.Spawn => Spawn,
        BoardRoles.Wool => WoolRoom,
        _ => Ground,
    };

    /// <summary>One row of the key: a label, the ink it reads in, and whether the board draws it as a dashed
    /// outline rather than a filled piece.</summary>
    public sealed record KeyEntry(string Label, PlanInk Ink, bool Dashed);

    /// <summary>The board's key, in the order a legend lists it. The PNG appends it under its raster, and a page
    /// showing board SVGs draws it once beside them, so the words and the colours a reader is given are the ones
    /// the renderers paint with.</summary>
    public static readonly IReadOnlyList<KeyEntry> Key =
    [
        new("Spawn", Spawn, false), new("Wool room", WoolRoom, false),
        new("Ground (rest)", Ground, false), new("Build zone", Zone, true),
    ];

    /// <summary>A packed swatch as CSS hex.</summary>
    public static string Hex(int rgb) => $"#{rgb:x6}";

    /// <summary>A wool marker's swatch, from the one dye table (<see cref="WoolColors"/>) the validator, the
    /// compiler and the editor all read, so a marker draws as the block it stamps.</summary>
    public static string WoolColor(string? color) => WoolColors.SwatchOf(color);

    public static int WoolRgb(string? color) => WoolColors.RgbOf(color);
}
