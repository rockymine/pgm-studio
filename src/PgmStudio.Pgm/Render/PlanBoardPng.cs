using PgmStudio.Geom.Render;
using PgmStudio.Pgm.Plan;
using static PgmStudio.Pgm.Render.PlanBoardScene;

namespace PgmStudio.Pgm.Render;

/// <summary>
/// Renders a plan to a PNG — the same fanned board <see cref="PlanBoardSvg"/> draws, off the same
/// <see cref="PlanBoardScene"/>, so the two never draw two different plans. A vector card scales losslessly
/// in a browser and an agent's image reader cannot open one; a raster is the one an agent asking for the
/// "plan" stage image actually gets back, whether from the API (<c>GET /plans/{id}/png</c>) or from a
/// generator writing its own <c>stages/plan.png</c> straight off the <see cref="PlanModel"/> it just built,
/// with no HTTP round trip.
/// </summary>
public static class PlanBoardPng
{
    private const int Background = 0x11141a;

    /// <param name="scale">Pixels per proxy cell. Raster has no lossless zoom, so this defaults higher than
    /// the SVG's own default — legible at the fixed size an image reader actually opens it at.</param>
    /// <param name="pad">Pixel margin around the board.</param>
    public static byte[] Render(PlanModel plan, int scale = 14, int pad = 16)
    {
        var scene = PlanBoardScene.Build(plan);
        int width = scene is null ? 2 * pad : scene.Width * scale + 2 * pad;
        int height = scene is null ? 2 * pad : scene.Height * scale + 2 * pad;

        var pixels = new byte[width * height * 3];
        for (var row = 0; row < height; row++)
            for (var col = 0; col < width; col++)
                Raster.Set(pixels, width, col, row, Background);
        if (scene is null) return PngWriter.Encode(width, height, pixels);

        int X(double cx) => (int)Math.Round((cx - scene.MinX) * scale) + pad;
        int Z(double cz) => (int)Math.Round((cz - scene.MinZ) * scale) + pad;

        foreach (var zone in scene.Zones)
        {
            var rgb = zone.Lane ? 0x2563eb : 0x38bdf8;
            FillRect(pixels, width, height, X(zone.Rect.X), Z(zone.Rect.Z), zone.Rect.Width * scale, zone.Rect.Height * scale,
                rgb, zone.Lane ? 0.32 : 0.18);
        }

        foreach (var piece in scene.Pieces)
        {
            var rgb = PieceRgb(piece.Id);
            var room = piece.Role != PlanRoles.Piece;
            var op = piece.K == 0 ? (room ? 0.95 : 0.4) : (room ? 0.55 : 0.22);
            var px = X(piece.Rect.X); var pz = Z(piece.Rect.Z);
            var pw = piece.Rect.Width * scale; var ph = piece.Rect.Height * scale;
            FillRect(pixels, width, height, px, pz, pw, ph, rgb, op);
            StrokeRect(pixels, width, height, px, pz, pw, ph, rgb, piece.K == 0 ? 1.0 : 0.4);
        }

        // markers at their fanned cells: iron (grey pip), wool (colour disc), spawn (pale disc drawn last, on top)
        foreach (var marker in scene.Markers.Where(m => m.Kind == "iron")) DrawMarker(pixels, width, height, marker, X, Z);
        foreach (var marker in scene.Markers.Where(m => m.Kind == "wool")) DrawMarker(pixels, width, height, marker, X, Z);
        foreach (var marker in scene.Markers.Where(m => m.Kind == "spawn")) DrawMarker(pixels, width, height, marker, X, Z);

        return PngWriter.Encode(width, height, pixels);
    }

    private static void DrawMarker(byte[] pixels, int width, int height, MarkerFan marker, Func<double, int> X, Func<double, int> Z)
    {
        var cx = X(marker.X); var cy = Z(marker.Z);
        var op = marker.K == 0 ? 1.0 : 0.5;
        switch (marker.Kind)
        {
            case "spawn": FillCircle(pixels, width, height, cx, cy, 3, 0xe2e8f5, op); break;
            case "iron": FillRect(pixels, width, height, cx - 3, cy - 3, 6, 6, 0x94a3b8, op); break;
            case "wool":
                FillCircle(pixels, width, height, cx, cy, 4, WoolRgb(marker.Color), op);
                StrokeCircle(pixels, width, height, cx, cy, 4, 0x1e293b, op * 0.5);
                break;
        }
    }

    private static void FillRect(byte[] pixels, int width, int height, int x, int z, int w, int h, int rgb, double opacity)
    {
        for (var row = Math.Max(0, z); row < Math.Min(height, z + h); row++)
            for (var col = Math.Max(0, x); col < Math.Min(width, x + w); col++)
                Raster.Over(pixels, width, col, row, rgb, opacity);
    }

    private static void StrokeRect(byte[] pixels, int width, int height, int x, int z, int w, int h, int rgb, double opacity)
    {
        for (var col = Math.Max(0, x); col < Math.Min(width, x + w); col++)
        {
            if (z >= 0 && z < height) Raster.Over(pixels, width, col, z, rgb, opacity);
            if (z + h - 1 >= 0 && z + h - 1 < height) Raster.Over(pixels, width, col, z + h - 1, rgb, opacity);
        }
        for (var row = Math.Max(0, z); row < Math.Min(height, z + h); row++)
        {
            if (x >= 0 && x < width) Raster.Over(pixels, width, x, row, rgb, opacity);
            if (x + w - 1 >= 0 && x + w - 1 < width) Raster.Over(pixels, width, x + w - 1, row, rgb, opacity);
        }
    }

    private static void FillCircle(byte[] pixels, int width, int height, int cx, int cy, int radius, int rgb, double opacity)
    {
        for (var row = Math.Max(0, cy - radius); row <= Math.Min(height - 1, cy + radius); row++)
            for (var col = Math.Max(0, cx - radius); col <= Math.Min(width - 1, cx + radius); col++)
                if ((col - cx) * (col - cx) + (row - cy) * (row - cy) <= radius * radius)
                    Raster.Over(pixels, width, col, row, rgb, opacity);
    }

    private static void StrokeCircle(byte[] pixels, int width, int height, int cx, int cy, int radius, int rgb, double opacity)
    {
        for (var row = Math.Max(0, cy - radius); row <= Math.Min(height - 1, cy + radius); row++)
            for (var col = Math.Max(0, cx - radius); col <= Math.Min(width - 1, cx + radius); col++)
            {
                var d2 = (col - cx) * (col - cx) + (row - cy) * (row - cy);
                if (d2 <= radius * radius && d2 >= (radius - 1) * (radius - 1))
                    Raster.Over(pixels, width, col, row, rgb, opacity);
            }
    }
}
