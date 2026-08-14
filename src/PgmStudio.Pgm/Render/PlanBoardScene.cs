using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Render;

/// <summary>
/// The fanned board geometry <see cref="PlanBoardSvg"/> and <see cref="PlanBoardPng"/> both draw — every
/// piece/zone/marker expanded to its full symmetry orbit, in cell space. Built once so the vector and raster
/// renders can never draw two different boards: a plan has one true geometry, and pixel placement (scale, pad,
/// stroke) is the only thing either renderer is free to decide for itself.
/// </summary>
internal static class PlanBoardScene
{
    internal readonly record struct PieceFan(CellRect Rect, string Id, string Role, int K);
    internal readonly record struct ZoneFan(CellRect Rect, bool Lane);
    internal readonly record struct MarkerFan(double X, double Z, string Kind, string? Color, int K);

    internal sealed record Scene(List<PieceFan> Pieces, List<ZoneFan> Zones, List<MarkerFan> Markers,
        int MinX, int MinZ, int Width, int Height);

    /// <summary>Null when the plan has no drawable geometry at all — both renderers fall back to an empty
    /// canvas in that case.</summary>
    public static Scene? Build(PlanModel plan)
    {
        int order = Symmetry.Order(plan.Globals.Symmetry);
        string[] axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);

        // base-unit rect per piece id — a marker's `At` is an offset from its host piece's origin, so its
        // absolute cell is piece.Rect + At (the markerCell convention the world canvas uses).
        var pieceRect = new Dictionary<string, CellRect>();
        foreach (var p in plan.Pieces) pieceRect[p.Id] = p.Rect;

        // solid pieces (annotations produce no terrain) and zones, each fanned to every orbit image
        var pieces = new List<PieceFan>();
        foreach (var p in plan.Pieces)
        {
            if (PlanRoles.IsAnnotation(p.Role)) continue;
            for (var k = 0; k < order; k++) pieces.Add(new PieceFan(Fan(p.Rect, axes, k), p.Id, p.Role, k));
        }
        var zones = new List<ZoneFan>();
        foreach (var z in plan.Zones)
            for (var k = 0; k < order; k++) zones.Add(new ZoneFan(Fan(z.Rect, axes, k), z.IsWaterLane));

        var rects = pieces.Select(f => f.Rect).Concat(zones.Select(z => z.Rect)).ToList();
        if (rects.Count == 0) return null;

        int minX = rects.Min(r => r.X), minZ = rects.Min(r => r.Z);
        int maxX = rects.Max(r => r.X + r.Width), maxZ = rects.Max(r => r.Z + r.Height);

        var markers = new List<MarkerFan>();
        foreach (var m in plan.Placements.Iron) FanMarker(markers, pieceRect, m.Piece, m.At, axes, order, "iron", null);
        foreach (var m in plan.Placements.Wools) FanMarker(markers, pieceRect, m.Piece, m.At, axes, order, "wool", m.Color);
        foreach (var m in plan.Placements.Spawns) FanMarker(markers, pieceRect, m.Piece, m.At, axes, order, "spawn", null);

        return new Scene(pieces, zones, markers, minX, minZ, maxX - minX, maxZ - minZ);
    }

    // a rect's k-th orbit image (identity at k=0; else the axis-aligned symmetry op about the axis line)
    private static CellRect Fan(CellRect r, string[] axes, int k)
    {
        if (k == 0) return r;
        (double x, double z)[] corners = [(r.X, r.Z), (r.X, r.Z + r.Height), (r.X + r.Width, r.Z), (r.X + r.Width, r.Z + r.Height)];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
        var x1 = (int)Math.Round(pts.Min(p => p.X));
        var z1 = (int)Math.Round(pts.Min(p => p.Z));
        return new(x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1);
    }

    private static void FanMarker(List<MarkerFan> markers, IReadOnlyDictionary<string, CellRect> pieceRect,
        string pieceId, double[] at, string[] axes, int order, string kind, string? color)
    {
        if (at.Length < 2 || !pieceRect.TryGetValue(pieceId, out var rect)) return;
        // absolute base-unit cell = host piece origin + the marker's offset (markerCell); no half-cell nudge,
        // so a centred marker (At = [1,1] on a 2×2 piece) lands dead centre.
        double bx = rect.X + at[0], bz = rect.Z + at[1];
        for (var k = 0; k < order; k++)
        {
            var (px, pz) = k == 0 ? (bx, bz) : Symmetry.Apply(bx, bz, axes[k - 1], 0, 0);
            markers.Add(new MarkerFan(px, pz, kind, color, k));
        }
    }

}
