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
    internal readonly record struct MarkerFan(string Id, double X, double Z, string Kind, string? Color, int K);

    /// <summary><see cref="ZoneHoles"/> are the cells a zone declares no-build — void whatever rect covers
    /// them, which a picture may ignore and a walk may not.</summary>
    internal sealed record Scene(List<PieceFan> Pieces, List<ZoneFan> Zones, List<MarkerFan> Markers,
        HashSet<(int X, int Z)> ZoneHoles, int MinX, int MinZ, int Width, int Height);

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
        var zoneHoles = new HashSet<(int X, int Z)>();
        foreach (var z in plan.Zones)
            for (var k = 0; k < order; k++)
            {
                zones.Add(new ZoneFan(Fan(z.Rect, axes, k), z.IsWaterLane));
                foreach (var hole in z.Holes)
                {
                    var r = Fan(hole, axes, k);
                    for (var x = r.X; x < r.X + r.Width; x++)
                        for (var cz = r.Z; cz < r.Z + r.Height; cz++) zoneHoles.Add((x, cz));
                }
            }

        var rects = pieces.Select(f => f.Rect).Concat(zones.Select(z => z.Rect)).ToList();
        if (rects.Count == 0) return null;

        int minX = rects.Min(r => r.X), minZ = rects.Min(r => r.Z);
        int maxX = rects.Max(r => r.X + r.Width), maxZ = rects.Max(r => r.Z + r.Height);

        var markers = new List<MarkerFan>();
        foreach (var m in plan.Placements.Iron) FanMarker(markers, pieceRect, m, axes, order, plan.Globals.Cell, "iron", null);
        foreach (var m in plan.Placements.Wools) FanMarker(markers, pieceRect, m, axes, order, plan.Globals.Cell, "wool", m.Color);
        foreach (var m in plan.Placements.Spawns) FanMarker(markers, pieceRect, m, axes, order, plan.Globals.Cell, "spawn", null);
        foreach (var m in plan.Placements.Destroyables) FanMarker(markers, pieceRect, m, axes, order, plan.Globals.Cell, "destroyable", null);
        foreach (var m in plan.Placements.Cores) FanMarker(markers, pieceRect, m, axes, order, plan.Globals.Cell, "core", null);

        return new Scene(pieces, zones, markers, zoneHoles, minX, minZ, maxX - minX, maxZ - minZ);
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
        IPlanMarker marker, string[] axes, int order, int cell, string kind, string? color)
    {
        var at = marker.At;
        if (at.Length < 2) return;
        // absolute base-unit cell = the host piece's origin plus the marker's block offset in cells, so a
        // centred marker lands dead centre. A destroyable or a core may name no piece at all (B128), in which
        // case `At` is already absolute about the symmetry centre.
        double bx, bz;
        if (marker.Piece.Length == 0) { bx = at[0] / (double)cell; bz = at[1] / (double)cell; }
        else if (pieceRect.TryGetValue(marker.Piece, out var rect)) (bx, bz) = PlanMarkers.Cell(rect, at, cell);
        else return;

        for (var k = 0; k < order; k++)
        {
            var (px, pz) = k == 0 ? (bx, bz) : Symmetry.Apply(bx, bz, axes[k - 1], 0, 0);
            markers.Add(new MarkerFan(marker.Id, px, pz, kind, color, k));
        }
    }

}
