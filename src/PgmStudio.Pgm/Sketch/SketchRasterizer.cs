using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Rasterizes a sketch layout (the <c>sketch_layout_json</c> blob) into the solid block cells of the
/// finished world (docs/contracts/sketch-tool-improvements.md §3). Pure: no DOM, no DB. <see cref="Rasterize"/>
/// yields the (x,z) footprint; <see cref="RasterizeColumns"/> also carries each cell's vertical span
/// <c>[YFloor, YTop]</c> — a uniform <c>base_height</c>, or, for a polygon/lasso whose <c>anchor_heights</c>
/// line up with its vertices, a per-vertex surface TIN-interpolated across the footprint
/// (<see cref="Triangulation"/>). Mirrors the JS geometry it must agree with (circle = 64-gon,
/// Bézier = 16 samples/edge); per-island mirror copies follow the saved island <c>shapeIds</c>.
/// </summary>
public static class SketchRasterizer
{
    private const int CirclePoints  = 64;   // matches JS geometry/shape.js CIRCLE_POINTS
    private const int BezierSamples  = 16;   // matches JS geometry/shape.js BEZIER_SAMPLES

    /// <summary>The finished world's solid (x,z) footprint (primary + opted-in island mirror copies).</summary>
    public static List<(int X, int Z)> Rasterize(string layoutJson)
        => RasterizeColumns(layoutJson).Select(c => (c.X, c.Z)).ToList();

    /// <summary>As <see cref="Rasterize"/>, but each cell also carries its column span <c>[YFloor, YTop]</c>.
    /// Height never affects membership — the footprint is identical to <see cref="Rasterize"/>.</summary>
    public static List<(int X, int Z, int YFloor, int YTop)> RasterizeColumns(string layoutJson)
    {
        var state = SketchLayout.Parse(layoutJson);
        var shapes = state?.Layout?.Shapes ?? [];
        if (shapes.Count == 0) return [];

        var cells = RasterGroup(shapes);                 // primary
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");
        var metas = state?.Layout?.Islands ?? [];

        if (metas.Count == 0)
        {
            // No island metadata (e.g. a hand-authored layout): mirror the whole primary footprint. Height is
            // reflection/rotation-invariant, so each mirrored cell keeps its column.
            var primary = new Dictionary<(int, int), (int Top, int Floor)>(cells);
            foreach (var axis in axes)
            {
                var mirrored = new Dictionary<(int, int), (int Top, int Floor)>();
                foreach (var (k, v) in primary) mirrored[MirrorCell(k, axis, cx, cz)] = v;
                Merge(cells, mirrored);
            }
        }
        else
        {
            var byId = shapes.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var meta in metas.Where(m => m.Mirrors))
            {
                var islandShapes = meta.ShapeIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                foreach (var axis in axes)
                {
                    // Mirror each shape (transform geometry + carry heights), then rasterize.
                    var mirrored = islandShapes.Select(s => MirrorShape(s, axis, cx, cz));
                    Merge(cells, RasterGroup(mirrored));
                }
            }
        }
        return cells.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value.Floor, kv.Value.Top)).ToList();
    }

    // ── 4-step set algebra over a shape group, carrying each cell's column ─────────────────────────
    private static Dictionary<(int, int), (int Top, int Floor)> RasterGroup(IEnumerable<SketchShape> shapes)
    {
        Dictionary<(int, int), (int Top, int Floor)> add = [], oadd = [];
        HashSet<(int, int)> sub = [], osub = [];
        foreach (var s in shapes)
        {
            if (s.Operation == "subtract")
            {
                var set = s.Override ? osub : sub;
                foreach (var c in RasterShape(s)) set.Add((c.X, c.Z));
            }
            else
            {
                var dict = s.Override ? oadd : add;
                foreach (var c in RasterShape(s)) MergeCell(dict, (c.X, c.Z), (c.Top, c.Floor));
            }
        }
        // ((adds − subs) ∪ override-adds) − override-subs; height = the tallest add on each cell.
        var result = new Dictionary<(int, int), (int Top, int Floor)>(add);
        foreach (var k in sub) result.Remove(k);
        foreach (var (k, v) in oadd) result[k] = v;        // override-add overwrites the column
        foreach (var k in osub) result.Remove(k);
        return result;
    }

    // Taller surface wins where add shapes overlap (carrying that surface's floor).
    private static void MergeCell(Dictionary<(int, int), (int Top, int Floor)> d, (int, int) k, (int Top, int Floor) v)
    {
        if (d.TryGetValue(k, out var ex)) { if (v.Top > ex.Top) d[k] = v; }
        else d[k] = v;
    }

    private static void Merge(Dictionary<(int, int), (int Top, int Floor)> dst, Dictionary<(int, int), (int Top, int Floor)> src)
    {
        foreach (var (k, v) in src) MergeCell(dst, k, v);
    }

    // ── single shape → cells with column (rasterize its ring by block-centre sampling) ────────────
    private static IEnumerable<(int X, int Z, int Top, int Floor)> RasterShape(SketchShape s)
    {
        var ring = RingOf(s);
        if (ring.Count < 3) yield break;
        int floor = (int)Math.Round(s.Floor ?? 0);
        var height = HeightFn(s);
        foreach (var (x, z) in RasterRing(ring))
            yield return (x, z, (int)Math.Round(height(x + 0.5, z + 0.5)), floor);
    }

    // The surface-height sampler for a shape: a per-vertex TIN (polygon/lasso with matching anchor_heights),
    // else the uniform base_height (or 0). The TIN is over the straight vertex polygon — points in a Bézier
    // fringe fall back to the nearest vertex inside Interpolate.
    private static Func<double, double, double> HeightFn(SketchShape s)
    {
        if ((s.Type == "polygon" || s.Type == "lasso") && s.Vertices is { Length: >= 3 } verts
            && s.AnchorHeights is { } ah && ah.Length == verts.Length)
        {
            var poly = verts.Select(v => new[] { v[0], v[1] }).ToList();
            var tris = Triangulation.EarClip(poly);
            return (x, z) => Triangulation.Interpolate(poly, ah, tris, x, z);
        }
        double bh = s.BaseHeight ?? 0;
        return (_, _) => bh;
    }

    private static List<double[]> RingOf(SketchShape s) => s.Type switch
    {
        "rectangle" => [[s.MinX ?? 0, s.MinZ ?? 0], [s.MaxX ?? 0, s.MinZ ?? 0], [s.MaxX ?? 0, s.MaxZ ?? 0], [s.MinX ?? 0, s.MaxZ ?? 0]],
        "circle"    => CircleRing(s.CenterX ?? 0, s.CenterZ ?? 0, s.Radius ?? 0),
        "polygon" or "lasso" => PolygonRing(s.Vertices, s.Controls),
        _ => [],
    };

    private static List<double[]> CircleRing(double cx, double cz, double r)
    {
        var pts = new List<double[]>(CirclePoints);
        for (var i = 0; i < CirclePoints; i++)
        {
            var a = 2 * Math.PI * i / CirclePoints;
            pts.Add([Math.Round(cx + r * Math.Cos(a)), Math.Round(cz + r * Math.Sin(a))]);
        }
        return pts;
    }

    private static List<double[]> PolygonRing(double[][]? verts, Dictionary<string, SketchControl>? controls)
    {
        if (verts is null || verts.Length < 3) return [];
        if (controls is null || controls.Count == 0) return [.. verts.Select(v => new[] { v[0], v[1] })];

        var ring = new List<double[]>();
        var n = verts.Length;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var p0 = verts[i];
            var p3 = verts[j];
            var cpOut = controls.GetValueOrDefault(i.ToString())?.Out;
            var cpIn  = controls.GetValueOrDefault(j.ToString())?.In;
            if (cpOut is not null || cpIn is not null) ring.AddRange(SampleBezier(p0, cpOut ?? p0, cpIn ?? p3, p3));
            else ring.Add([p0[0], p0[1]]);
        }
        return ring;
    }

    private static IEnumerable<double[]> SampleBezier(double[] p0, double[] c1, double[] c2, double[] p3)
    {
        for (var k = 0; k < BezierSamples; k++)
        {
            double t = (double)k / BezierSamples, u = 1 - t;
            yield return [
                u*u*u*p0[0] + 3*u*u*t*c1[0] + 3*u*t*t*c2[0] + t*t*t*p3[0],
                u*u*u*p0[1] + 3*u*u*t*c1[1] + 3*u*t*t*c2[1] + t*t*t*p3[1],
            ];
        }
    }

    private static IEnumerable<(int, int)> RasterRing(List<double[]> ring)
    {
        if (ring.Count < 3) yield break;
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var p in ring)
        {
            minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]);
            minZ = Math.Min(minZ, p[1]); maxZ = Math.Max(maxZ, p[1]);
        }
        for (var x = (int)Math.Floor(minX); x < (int)Math.Ceiling(maxX); x++)
            for (var z = (int)Math.Floor(minZ); z < (int)Math.Ceiling(maxZ); z++)
                if (Polygon.PointInRing(x + 0.5, z + 0.5, ring)) yield return (x, z);
    }

    // ── symmetry ──────────────────────────────────────────────────────────────────────────────────
    // Mirror a block cell's centre, then floor back to a cell (used only for the no-metadata fallback).
    private static (int, int) MirrorCell((int X, int Z) c, string axis, double cx, double cz)
    {
        var (x, z) = MirrorPoint(c.X + 0.5, c.Z + 0.5, axis, cx, cz);
        return ((int)Math.Floor(x), (int)Math.Floor(z));
    }

    private static SketchShape MirrorShape(SketchShape s, string axis, double cx, double cz)
    {
        // Polygon/lasso: transform vertices + Bézier handles in place, keeping anchor_heights index-aligned
        // (height is invariant) so a per-vertex surface mirrors correctly.
        if ((s.Type == "polygon" || s.Type == "lasso") && s.Vertices is { } verts)
        {
            var nv = verts.Select(v => { var (x, z) = MirrorPoint(v[0], v[1], axis, cx, cz); return new[] { x, z }; }).ToArray();
            Dictionary<string, SketchControl>? nc = null;
            if (s.Controls is { } ctrls)
            {
                nc = [];
                foreach (var (k, c) in ctrls)
                {
                    var nco = new SketchControl();
                    if (c.In  is { } i) { var (x, z) = MirrorPoint(i[0], i[1], axis, cx, cz); nco.In  = [x, z]; }
                    if (c.Out is { } o) { var (x, z) = MirrorPoint(o[0], o[1], axis, cx, cz); nco.Out = [x, z]; }
                    nc[k] = nco;
                }
            }
            return new SketchShape
            {
                Id = s.Id, Type = s.Type, Operation = s.Operation, Override = s.Override,
                Vertices = nv, Controls = nc, AnchorHeights = s.AnchorHeights, BaseHeight = s.BaseHeight, Floor = s.Floor,
            };
        }
        // Rectangle/circle: flatten the transformed footprint to a polygon (uniform height carried).
        var ring = RingOf(s).Select(p => { var (x, z) = MirrorPoint(p[0], p[1], axis, cx, cz); return new[] { x, z }; }).ToArray();
        return new SketchShape
        {
            Id = s.Id, Type = "polygon", Operation = s.Operation, Override = s.Override,
            Vertices = ring, BaseHeight = s.BaseHeight, Floor = s.Floor,
        };
    }

    // The one canonical concrete-axis transform — every orbit axis stays consistent with the generator + JS.
    private static (double, double) MirrorPoint(double x, double z, string axis, double cx, double cz)
        => Symmetry.Apply(x, z, axis, cx, cz);
}
