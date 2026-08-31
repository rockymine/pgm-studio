using System.Globalization;
using System.Text;
using PgmStudio.Pgm.Plan;
using static PgmStudio.Pgm.Render.PlanBoardScene;
using static PgmStudio.Pgm.Render.PlanBoardPalette;

namespace PgmStudio.Pgm.Render;

/// <summary>
/// Renders a plan as a self-contained SVG of the <b>full fanned board</b> — every piece/zone fanned to its
/// orbit images, coloured by role (the base unit at full strength, the fanned images faint), zones drawn as
/// dashed build bands, and the spawn/wool/iron markers placed at their fanned cells. Pure over a
/// <see cref="PlanModel"/>, the geometry built once by <see cref="PlanBoardScene"/> and shared with
/// <see cref="PlanBoardPng"/> so the two encodings of one plan can never disagree with each other. This is the
/// browse feed's card image and, scaled up, its detail view.
/// </summary>
public static class PlanBoardSvg
{
    /// <summary>Role swatches, in the order <see cref="PlanBoardPalette.RoleName"/> would ever return them, plus
    /// the two zone kinds — the key every plan render carries baked in rather than left to a caption
    /// (<c>B95</c>, <c>docs/tools/capabilities.md</c>'s renderer section: an image is a check, not a source of
    /// meaning). A reader with the picture and not this key is the failure the legend exists to close.</summary>
    private static readonly (string Label, string Color)[] LegendRows =
    [
        ("hub", "#a78bfa"), ("spawn", "#34d399"), ("wool", "#fbbf24"), ("frontline", "#fb923c"), ("other", "#64748b"),
        ("build zone", BuildZoneColor), ("water lane (hatched)", WaterLaneColor),
    ];

    private const int LegendRowHeight = 16;
    private const int LegendSwatch = 10;

    /// <summary><b>scale</b> is pixels per proxy cell.
    /// <para><b>pad</b> — Pixel margin around the board.</para></summary>
    public static string Render(PlanModel plan, int scale = 9, int pad = 10)
    {
        var scene = PlanBoardScene.Build(plan);
        var legendHeight = pad + LegendRows.Length * LegendRowHeight;
        if (scene is null)
        {
            int emptyWidth = 2 * pad, emptyHeight = 2 * pad + legendHeight;
            var emptySvg = new StringBuilder();
            emptySvg.Append($"<svg viewBox='0 0 {emptyWidth} {emptyHeight}' width='{emptyWidth}' height='{emptyHeight}' xmlns='http://www.w3.org/2000/svg'>");
            AppendLegend(emptySvg, pad, 2 * pad);
            emptySvg.Append("</svg>");
            return emptySvg.ToString();
        }

        int boardWidth = scene.Width * scale + 2 * pad, boardHeight = scene.Height * scale + 2 * pad;
        int vw = boardWidth, vh = boardHeight + legendHeight;
        double X(double cx) => (cx - scene.MinX) * scale + pad;
        double Z(double cz) => (cz - scene.MinZ) * scale + pad;

        var svg = new StringBuilder();
        svg.Append($"<svg viewBox='0 0 {vw} {vh}' width='{vw}' height='{vh}' xmlns='http://www.w3.org/2000/svg' role='img'>");
        svg.Append("<defs><pattern id='waterLaneHatch' width='6' height='6' patternTransform='rotate(45)' patternUnits='userSpaceOnUse'>"
            + $"<rect width='6' height='6' fill='{WaterLaneColor}' fill-opacity='0.30'/>"
            + $"<rect width='3' height='6' fill='{WaterLaneColor}' fill-opacity='0.62'/></pattern></defs>");

        // A water lane and a build zone are the same gap with different crossing rules — one open from the
        // first minute, one opening 45 minutes in — so the picture separates them by hue (blue only ever
        // means the lane) and, for the lane, by a hatch a shade/opacity/dash difference cannot lose.
        foreach (var zone in scene.Zones)
        {
            var b = zone.Rect;
            var fill = zone.Lane ? "url(#waterLaneHatch)" : BuildZoneColor;
            var col = zone.Lane ? WaterLaneColor : BuildZoneColor;
            var fillOpacity = zone.Lane ? "1" : "0.38";
            svg.Append($"<rect x='{N(X(b.X))}' y='{N(Z(b.Z))}' width='{N(b.Width * scale)}' height='{N(b.Height * scale)}' "
                + $"fill='{fill}' fill-opacity='{fillOpacity}' stroke='{col}' stroke-opacity='0.6' "
                + $"stroke-width='1' stroke-dasharray='{(zone.Lane ? "1 2" : "3 2")}'/>");
        }

        foreach (var piece in scene.Pieces)
        {
            var r = piece.Rect;
            var col = PieceColor(piece.Id);
            var room = piece.Role != PlanRoles.Piece;
            var op = piece.K == 0 ? (room ? 0.95 : 0.4) : (room ? 0.55 : 0.22);
            svg.Append($"<rect x='{N(X(r.X))}' y='{N(Z(r.Z))}' width='{N(r.Width * scale)}' height='{N(r.Height * scale)}' "
                + $"rx='1' fill='{col}' fill-opacity='{N(op)}' stroke='{col}' stroke-opacity='{(piece.K == 0 ? "1" : "0.4")}' stroke-width='0.8'/>");
        }

        // markers at their fanned cells: iron (grey pip), wool (colour disc), spawn (pale disc drawn last, on top)
        foreach (var marker in scene.Markers.Where(m => m.Kind == "iron")) DrawMarker(svg, marker, X, Z);
        foreach (var marker in scene.Markers.Where(m => m.Kind == "wool")) DrawMarker(svg, marker, X, Z);
        foreach (var marker in scene.Markers.Where(m => m.Kind == "spawn")) DrawMarker(svg, marker, X, Z);

        AppendLegend(svg, pad, boardHeight);
        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>The role/zone key, drawn as one swatch-plus-label row per <see cref="LegendRows"/> entry,
    /// starting at <paramref name="top"/> — the strip beneath the board itself.</summary>
    private static void AppendLegend(StringBuilder svg, int pad, int top)
    {
        var y = top;
        foreach (var (label, color) in LegendRows)
        {
            svg.Append($"<rect x='{pad}' y='{N(y + 3)}' width='{LegendSwatch}' height='{LegendSwatch}' fill='{color}'/>");
            svg.Append($"<text x='{pad + LegendSwatch + 5}' y='{N(y + 3 + LegendSwatch - 1.5)}' "
                + "font-family='monospace' font-size='11' fill='#e2e8f5'>" + System.Net.WebUtility.HtmlEncode(label) + "</text>");
            y += LegendRowHeight;
        }
    }

    private static void DrawMarker(StringBuilder svg, MarkerFan marker, Func<double, double> X, Func<double, double> Z)
    {
        double cx = X(marker.X), cy = Z(marker.Z);
        var op = N(marker.K == 0 ? 1.0 : 0.5);
        svg.Append(marker.Kind switch
        {
            "spawn" => $"<circle cx='{N(cx)}' cy='{N(cy)}' r='2.5' fill='#e2e8f5' fill-opacity='{op}'/>",
            "iron" => $"<rect x='{N(cx - 2)}' y='{N(cy - 2)}' width='4' height='4' fill='#94a3b8' fill-opacity='{op}'/>",
            "wool" => $"<circle cx='{N(cx)}' cy='{N(cy)}' r='2.6' fill='{WoolColor(marker.Color)}' stroke='#1e293b' stroke-opacity='0.5' stroke-width='0.6' fill-opacity='{op}'/>",
            _ => "",
        });
    }

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
