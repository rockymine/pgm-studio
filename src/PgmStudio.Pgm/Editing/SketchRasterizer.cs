using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Editing;

/// <summary>
/// Rasterizes a sketch layout (the <c>sketch_layout_json</c> blob) into the set of solid (x,z) block
/// cells of the finished world — the server half of "finish a sketch" (docs/contracts/sketch-authoring.md
/// §4). Pure: no DOM, no DB. Mirrors the JS geometry it must agree with — shapes → closed rings
/// (circle = 64-gon, Bézier = 16 samples/edge), the 4-step add/subtract/override set algebra applied to
/// each shape's rasterized cell set, and per-island mirror copies via the saved island <c>shapeIds</c>
/// (so no server-side boolean is needed — the JS already assigned shapes to islands). Cells are then fed
/// to <c>IslandDetector.Detect</c> + the artifact writers.
/// </summary>
public static class SketchRasterizer
{
    private const int CirclePoints  = 64;   // matches JS geometry/shape.js CIRCLE_POINTS
    private const int BezierSamples  = 16;   // matches JS geometry/shape.js BEZIER_SAMPLES

    // ── wire model (the JS-origin blob; camelCase + the `in`/`override` keywords aliased) ──────────
    private sealed class State
    {
        [JsonPropertyName("setup")]  public Setup? Setup { get; set; }
        [JsonPropertyName("layout")] public Layout? Layout { get; set; }
    }
    private sealed class Setup
    {
        [JsonPropertyName("mirror_mode")] public string MirrorMode { get; set; } = "rot_180";
        [JsonPropertyName("center")]      public Center? Center { get; set; }
    }
    private sealed class Center
    {
        [JsonPropertyName("cx")] public double Cx { get; set; }
        [JsonPropertyName("cz")] public double Cz { get; set; }
    }
    private sealed class Layout
    {
        [JsonPropertyName("shapes")]  public List<Shape> Shapes { get; set; } = [];
        [JsonPropertyName("islands")] public List<IslandMeta> Islands { get; set; } = [];
    }
    private sealed class IslandMeta
    {
        [JsonPropertyName("mirrors")]  public bool Mirrors { get; set; } = true;
        [JsonPropertyName("shapeIds")] public List<string> ShapeIds { get; set; } = [];
    }
    private sealed class Ctrl
    {
        [JsonPropertyName("in")]  public double[]? In { get; set; }
        [JsonPropertyName("out")] public double[]? Out { get; set; }
    }
    private sealed class Shape
    {
        [JsonPropertyName("id")]        public string Id { get; set; } = "";
        [JsonPropertyName("type")]      public string Type { get; set; } = "";
        [JsonPropertyName("operation")] public string Operation { get; set; } = "add";
        [JsonPropertyName("override")]  public bool Override { get; set; }
        [JsonPropertyName("min_x")] public double? MinX { get; set; }
        [JsonPropertyName("min_z")] public double? MinZ { get; set; }
        [JsonPropertyName("max_x")] public double? MaxX { get; set; }
        [JsonPropertyName("max_z")] public double? MaxZ { get; set; }
        [JsonPropertyName("center_x")] public double? CenterX { get; set; }
        [JsonPropertyName("center_z")] public double? CenterZ { get; set; }
        [JsonPropertyName("radius")]   public double? Radius { get; set; }
        [JsonPropertyName("vertices")] public double[][]? Vertices { get; set; }
        [JsonPropertyName("controls")] public Dictionary<string, Ctrl>? Controls { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Rasterize the stored layout JSON to the finished world's solid (x,z) cells (primary +
    /// the mirror copies of the islands that opt in). Empty when the layout has no add shapes.</summary>
    public static List<(int X, int Z)> Rasterize(string layoutJson)
    {
        var state = JsonSerializer.Deserialize<State>(layoutJson, Json);
        var shapes = state?.Layout?.Shapes ?? [];
        if (shapes.Count == 0) return [];

        var cells = RasterGroup(shapes);                 // primary
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = MirrorAxes(state?.Setup?.MirrorMode ?? "rot_180");
        var metas = state?.Layout?.Islands ?? [];

        if (metas.Count == 0)
        {
            // No island metadata (e.g. a hand-authored layout): mirror the whole primary footprint.
            foreach (var axis in axes)
                cells.UnionWith(RasterGroup(shapes).Select(c => MirrorCell(c, axis, cx, cz)));
        }
        else
        {
            var byId = shapes.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var meta in metas.Where(m => m.Mirrors))
            {
                var islandShapes = meta.ShapeIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                foreach (var axis in axes)
                {
                    // Mirror each shape's geometry, then rasterize (transform the polygon, not the
                    // rasterized cells — a per-cell mirror leaves gaps on rotations).
                    var mirrored = islandShapes.Select(s => MirrorShape(s, axis, cx, cz));
                    cells.UnionWith(RasterGroup(mirrored));
                }
            }
        }
        return [.. cells];
    }

    // ── 4-step set algebra over a shape group ─────────────────────────────────────────────────────
    private static HashSet<(int, int)> RasterGroup(IEnumerable<Shape> shapes)
    {
        HashSet<(int, int)> add = [], sub = [], oadd = [], osub = [];
        foreach (var s in shapes)
        {
            var target = (s.Operation == "subtract", s.Override) switch
            {
                (false, false) => add,
                (true,  false) => sub,
                (false, true)  => oadd,
                (true,  true)  => osub,
            };
            target.UnionWith(RasterShape(s));
        }
        // ((adds − subs) ∪ override-adds) − override-subs
        var result = new HashSet<(int, int)>(add);
        result.ExceptWith(sub);
        result.UnionWith(oadd);
        result.ExceptWith(osub);
        return result;
    }

    // ── single shape → cells (rasterize its ring by block-centre sampling) ─────────────────────────
    private static IEnumerable<(int, int)> RasterShape(Shape s) => RasterRing(RingOf(s));

    private static List<double[]> RingOf(Shape s) => s.Type switch
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

    private static List<double[]> PolygonRing(double[][]? verts, Dictionary<string, Ctrl>? controls)
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
    private static string[] MirrorAxes(string mode) => mode == "rot_90" ? ["rot_90", "rot_180", "rot_270"] : [mode];

    // Mirror a block cell's centre, then floor back to a cell (used only for the no-metadata fallback).
    private static (int, int) MirrorCell((int X, int Z) c, string axis, double cx, double cz)
    {
        var (x, z) = MirrorPoint(c.X + 0.5, c.Z + 0.5, axis, cx, cz);
        return ((int)Math.Floor(x), (int)Math.Floor(z));
    }

    private static Shape MirrorShape(Shape s, string axis, double cx, double cz)
    {
        // Transform the shape's ring points into a polygon shape carrying the same operation/override,
        // so the 4-step still composes the mirror copy correctly.
        var ring = RingOf(s).Select(p => { var (x, z) = MirrorPoint(p[0], p[1], axis, cx, cz); return new[] { x, z }; }).ToArray();
        return new Shape { Id = s.Id, Type = "polygon", Operation = s.Operation, Override = s.Override, Vertices = ring };
    }

    // The one canonical concrete-axis transform — every orbit axis (incl. the diagonals mirror_d1/d2 and
    // the rot_270 image that MirrorAxes fans rot_90 out to) stays consistent with the generator + JS canvas.
    private static (double, double) MirrorPoint(double x, double z, string axis, double cx, double cz)
        => Symmetry.Apply(x, z, axis, cx, cz);
}
