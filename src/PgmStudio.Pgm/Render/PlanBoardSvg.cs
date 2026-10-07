using System.Globalization;
using System.Text;
using PgmStudio.Pgm.Plan;
using static PgmStudio.Pgm.Render.PlanBoardScene;
using static PgmStudio.Pgm.Render.PlanBoardPalette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Render;

/// <summary>
/// Renders a plan as a self-contained SVG of the <b>full fanned board</b> — every piece/zone fanned to its
/// orbit images, in the four inks of <see cref="PlanBoardPalette"/> (the base unit at full strength, the fanned
/// images at half), zones drawn as dashed outlines, and the spawn/wool/iron markers placed at their fanned
/// cells. It carries no legend: a page showing boards draws <see cref="PlanBoardPalette.Key"/> once beside them.
/// Pure over a <see cref="PlanModel"/>, the geometry built once by <see cref="PlanBoardScene"/> and shared with
/// <see cref="PlanBoardPng"/> so the two encodings of one plan can never disagree with each other. The SVG paints
/// no ground of its own: the page frames it on <c>--board-bg</c>, and every ink reads its <c>--board-*</c> token,
/// falling back to the paper values where the SVG is shown outside the app. Each piece carries a
/// <c>role-*</c> class (<see cref="BoardRoles"/>, with <see cref="BoardRoles.Zone"/> for a zone), so a page
/// can tint one role by CSS without asking for another picture. This is the browse feed's card image and,
/// scaled up, its detail view.
/// </summary>
public static class PlanBoardSvg
{
    /// <summary><b>scale</b> is pixels per proxy cell.
    /// <para><b>pad</b> — Pixel margin around the board.</para></summary>
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

        foreach (var piece in scene.Pieces)
        {
            var r = piece.Rect;
            var role = RoleOf(piece.Role, piece.Id);
            var ink = InkOf(role);
            svg.Append($"<rect class='role-{role}' x='{N(X(r.X))}' y='{N(Z(r.Z))}' width='{N(r.Width * scale)}' height='{N(r.Height * scale)}' "
                + $"style='fill:{ink.FillCss};stroke:{ink.EdgeCss}' stroke-width='1'/>");
        }

        // A build zone and a water lane draw alike: the composer builds no lanes, so the board has one outline.
        foreach (var zone in scene.Zones)
        {
            var b = zone.Rect;
            svg.Append($"<rect class='role-{BoardRoles.Zone}' x='{N(X(b.X))}' y='{N(Z(b.Z))}' width='{N(b.Width * scale)}' height='{N(b.Height * scale)}' "
                + $"style='fill:{Zone.FillCss};stroke:{Zone.EdgeCss}' fill-opacity='0.07' stroke-width='1.4' stroke-dasharray='5 3'/>");
        }

        // markers at their fanned cells: iron (grey pip), wool (colour disc), spawn (disc drawn last, on top)
        foreach (var marker in scene.Markers.Where(m => m.Kind == "iron")) DrawMarker(svg, marker, X, Z);
        foreach (var marker in scene.Markers.Where(m => m.Kind == "wool")) DrawMarker(svg, marker, X, Z);
        foreach (var marker in scene.Markers.Where(m => m.Kind == "spawn")) DrawMarker(svg, marker, X, Z);

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void DrawMarker(StringBuilder svg, MarkerFan marker, Func<double, double> X, Func<double, double> Z)
    {
        double cx = X(marker.X), cy = Z(marker.Z);
        svg.Append(marker.Kind switch
        {
            "spawn" => $"<circle cx='{N(cx)}' cy='{N(cy)}' r='2.5' style='fill:{Spawn.EdgeCss}'/>",
            "iron" => $"<rect x='{N(cx - 2)}' y='{N(cy - 2)}' width='4' height='4' style='fill:var(--board-iron,{Hex(AxisRgb)})'/>",
            "wool" => $"<circle cx='{N(cx)}' cy='{N(cy)}' r='2.6' fill='{WoolColor(marker.Color)}' stroke='#1e293b' stroke-opacity='0.45' stroke-width='0.6'/>",
            _ => "",
        });
    }

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
