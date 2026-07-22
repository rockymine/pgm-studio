using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Render;

/// <summary>
/// Renders a plan as a self-contained SVG of the <b>full fanned board</b> — every piece/zone fanned to its
/// orbit images, coloured by role (the base unit at full strength, the fanned images faint), zones drawn as
/// dashed build bands, and the spawn/wool/iron markers placed at their fanned cells. Pure over a
/// <see cref="PlanModel"/>; the fan routes through <see cref="Symmetry"/>. This is the browse feed's card
/// image and, scaled up, its detail view — one renderer, server-side, shipped as an SVG string.
/// </summary>
public static class PlanBoardSvg
{
    /// <param name="scale">Pixels per proxy cell.</param>
    /// <param name="pad">Pixel margin around the board.</param>
    public static string Render(PlanModel plan, int scale = 9, int pad = 10)
    {
        int order = Symmetry.Order(plan.Globals.Symmetry);
        string[] axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);

        // base-unit rect per piece id — a marker's `At` is an offset from its host piece's origin, so its
        // absolute cell is piece.Rect + At (the markerCell convention the editor canvas uses).
        var pieceRect = new Dictionary<string, int[]>();
        foreach (var p in plan.Pieces) pieceRect[p.Id] = p.Rect;

        // solid pieces (annotations produce no terrain) and zones, each fanned to every orbit image
        var pieces = new List<(int[] Rect, string Id, string Role, int K)>();
        foreach (var p in plan.Pieces)
        {
            if (PlanRoles.IsAnnotation(p.Role)) continue;
            for (var k = 0; k < order; k++) pieces.Add((Fan(p.Rect, axes, k), p.Id, p.Role, k));
        }
        var zones = new List<int[]>();
        foreach (var z in plan.Zones)
            for (var k = 0; k < order; k++) zones.Add(Fan(z.Rect, axes, k));

        var rects = pieces.Select(f => f.Rect).Concat(zones).ToList();
        if (rects.Count == 0)
            return $"<svg viewBox='0 0 {2 * pad} {2 * pad}' width='{2 * pad}' height='{2 * pad}' xmlns='http://www.w3.org/2000/svg'></svg>";

        int minX = rects.Min(r => r[0]), minZ = rects.Min(r => r[1]);
        int maxX = rects.Max(r => r[0] + r[2]), maxZ = rects.Max(r => r[1] + r[3]);
        int w = maxX - minX, h = maxZ - minZ;
        int vw = w * scale + 2 * pad, vh = h * scale + 2 * pad;

        double X(double cx) => (cx - minX) * scale + pad;
        double Z(double cz) => (cz - minZ) * scale + pad;

        var svg = new StringBuilder();
        svg.Append($"<svg viewBox='0 0 {vw} {vh}' width='{vw}' height='{vh}' xmlns='http://www.w3.org/2000/svg' role='img'>");

        foreach (var b in zones)
            svg.Append($"<rect x='{N(X(b[0]))}' y='{N(Z(b[1]))}' width='{N(b[2] * scale)}' height='{N(b[3] * scale)}' "
                + "fill='#38bdf8' fill-opacity='0.18' stroke='#38bdf8' stroke-opacity='0.5' stroke-width='1' stroke-dasharray='3 2'/>");

        foreach (var (r, id, role, k) in pieces)
        {
            var col = PieceColor(id);
            var room = role != PlanRoles.Piece;
            var op = k == 0 ? (room ? 0.95 : 0.4) : (room ? 0.55 : 0.22);
            svg.Append($"<rect x='{N(X(r[0]))}' y='{N(Z(r[1]))}' width='{N(r[2] * scale)}' height='{N(r[3] * scale)}' "
                + $"rx='1' fill='{col}' fill-opacity='{N(op)}' stroke='{col}' stroke-opacity='{(k == 0 ? "1" : "0.4")}' stroke-width='0.8'/>");
        }

        // markers at their fanned cells: iron (grey pip), wool (colour disc), spawn (pale disc drawn last, on top)
        foreach (var m in plan.Placements.Iron) Marker(svg, pieceRect, m.Piece, m.At, axes, order, X, Z, "iron", null);
        foreach (var m in plan.Placements.Wools) Marker(svg, pieceRect, m.Piece, m.At, axes, order, X, Z, "wool", m.Color);
        foreach (var m in plan.Placements.Spawns) Marker(svg, pieceRect, m.Piece, m.At, axes, order, X, Z, "spawn", null);

        svg.Append("</svg>");
        return svg.ToString();
    }

    // a rect's k-th orbit image (identity at k=0; else the axis-aligned symmetry op about the axis line)
    private static int[] Fan(int[] r, string[] axes, int k)
    {
        if (k == 0) return r;
        (double x, double z)[] corners = [(r[0], r[1]), (r[0], r[1] + r[3]), (r[0] + r[2], r[1]), (r[0] + r[2], r[1] + r[3])];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
        var x1 = (int)Math.Round(pts.Min(p => p.X));
        var z1 = (int)Math.Round(pts.Min(p => p.Z));
        return [x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1];
    }

    private static void Marker(StringBuilder svg, IReadOnlyDictionary<string, int[]> pieceRect, string pieceId,
        double[] at, string[] axes, int order, Func<double, double> X, Func<double, double> Z, string kind, string? color)
    {
        if (at.Length < 2 || !pieceRect.TryGetValue(pieceId, out var rect)) return;
        // absolute base-unit cell = host piece origin + the marker's offset (markerCell); drawn at that grid
        // point directly — no half-cell nudge — so a centred marker (At = [1,1] on a 2×2 piece) lands dead centre.
        double bx = rect[0] + at[0], bz = rect[1] + at[1];
        for (var k = 0; k < order; k++)
        {
            var (px, pz) = k == 0 ? (bx, bz) : PtFan(bx, bz, axes[k - 1]);
            double cx = X(px), cy = Z(pz);
            var op = N(k == 0 ? 1.0 : 0.5);
            svg.Append(kind switch
            {
                "spawn" => $"<circle cx='{N(cx)}' cy='{N(cy)}' r='2.5' fill='#e2e8f5' fill-opacity='{op}'/>",
                "iron" => $"<rect x='{N(cx - 2)}' y='{N(cy - 2)}' width='4' height='4' fill='#94a3b8' fill-opacity='{op}'/>",
                "wool" => $"<circle cx='{N(cx)}' cy='{N(cy)}' r='2.6' fill='{WoolColor(color)}' stroke='#1e293b' stroke-opacity='0.5' stroke-width='0.6' fill-opacity='{op}'/>",
                _ => "",
            });
        }
    }

    private static (double X, double Z) PtFan(double x, double z, string axis)
    {
        var p = Symmetry.Apply(x, z, axis, 0, 0);
        return (p.X, p.Z);
    }

    private static string PieceColor(string id) =>
        id.StartsWith("hub") ? "#a78bfa"
        : id.StartsWith("spawn") ? "#34d399"
        : id.StartsWith("wool") ? "#fbbf24"
        : id.StartsWith("frontline") ? "#fb923c"
        : "#64748b";

    // Common wool dye names → a swatch; anything else falls back to amber.
    private static string WoolColor(string? color) => (color ?? "").ToLowerInvariant() switch
    {
        "red" => "#ef4444", "blue" => "#3b82f6", "green" or "lime" => "#22c55e",
        "yellow" => "#eab308", "orange" => "#f97316", "purple" or "magenta" => "#a855f7",
        "cyan" or "aqua" => "#06b6d4", "pink" => "#ec4899", "white" => "#f8fafc",
        "black" => "#334155", "brown" => "#92400e",
        _ => "#fbbf24",
    };

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
