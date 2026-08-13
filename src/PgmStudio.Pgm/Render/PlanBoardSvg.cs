using System.Globalization;
using System.Text;
using PgmStudio.Pgm.Plan;
using static PgmStudio.Pgm.Render.PlanBoardScene;

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
    /// <param name="scale">Pixels per proxy cell.</param>
    /// <param name="pad">Pixel margin around the board.</param>
    public static string Render(PlanModel plan, int scale = 9, int pad = 10)
    {
        var scene = PlanBoardScene.Build(plan);
        if (scene is null)
            return $"<svg viewBox='0 0 {2 * pad} {2 * pad}' width='{2 * pad}' height='{2 * pad}' xmlns='http://www.w3.org/2000/svg'></svg>";

        int vw = scene.Width * scale + 2 * pad, vh = scene.Height * scale + 2 * pad;
        double X(double cx) => (cx - scene.MinX) * scale + pad;
        double Z(double cz) => (cz - scene.MinZ) * scale + pad;

        var svg = new StringBuilder();
        svg.Append($"<svg viewBox='0 0 {vw} {vh}' width='{vw}' height='{vh}' xmlns='http://www.w3.org/2000/svg' role='img'>");

        // A water lane is drawn deeper and denser than a build zone: both are gaps players cross, and the
        // only thing separating them on a still image is which one is open yet.
        foreach (var zone in scene.Zones)
        {
            var b = zone.Rect;
            var col = zone.Lane ? "#2563eb" : "#38bdf8";
            svg.Append($"<rect x='{N(X(b.X))}' y='{N(Z(b.Z))}' width='{N(b.Width * scale)}' height='{N(b.Height * scale)}' "
                + $"fill='{col}' fill-opacity='{(zone.Lane ? "0.32" : "0.18")}' stroke='{col}' stroke-opacity='0.5' "
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

        svg.Append("</svg>");
        return svg.ToString();
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
