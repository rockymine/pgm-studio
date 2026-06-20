using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The presentation step that makes a generated layout editable: the board is recentred on the symmetry
/// origin, a square framing bbox is set, and dense lane hulls are thinned to a handful of draggable vertices.
/// </summary>
public sealed class SketchLayoutPrepTests
{
    [Test]
    public async Task ForEditor_recentres_on_the_origin_and_frames_every_shape()
    {
        // HLayout's board is 60×90 → its symmetry centre starts at (30, 45).
        var layout = SketchLayoutPrep.ForEditor(LaneSketchGenerator.HLayout().Layout);

        await Assert.That(layout.Setup!.Center!.Cx).IsEqualTo(0);
        await Assert.That(layout.Setup.Center.Cz).IsEqualTo(0);

        // square, origin-centred bbox
        var bbox = layout.Setup.Bbox!;
        await Assert.That(bbox.MaxX).IsEqualTo(-bbox.MinX);
        await Assert.That(bbox.MaxZ).IsEqualTo(-bbox.MinZ);
        await Assert.That(bbox.MaxX).IsEqualTo(bbox.MaxZ);

        // every recentred vertex sits inside the frame
        var half = bbox.MaxX;
        foreach (var shape in layout.Layout!.Shapes)
            foreach (var v in shape.Vertices!)
            {
                await Assert.That(Math.Abs(v[0]) <= half).IsTrue();
                await Assert.That(Math.Abs(v[1]) <= half).IsTrue();
            }
    }

    [Test]
    public async Task ForEditor_thins_dense_lane_polygons()
    {
        // Organic lanes are the dense case — jittered ribbon hulls of many vertices.
        var layout = LaneSketchGenerator.Build(new LaneLayoutOptions { Archetype = LaneArchetype.Organic, Seed = 3 }).Layout;
        var before = layout.Layout!.Shapes.Max(s => s.Vertices!.Length);

        SketchLayoutPrep.ForEditor(layout);
        var after = layout.Layout.Shapes.Max(s => s.Vertices!.Length);

        await Assert.That(before > 12).IsTrue();   // the generator really did emit dense hulls
        await Assert.That(after < before).IsTrue(); // simplified down
    }

    [Test]
    public async Task ForEditor_leaves_straight_lanes_and_regular_mids_uncurved()
    {
        // Default H is all rectangles + an octagon mid — nothing to round.
        var layout = SketchLayoutPrep.ForEditor(LaneSketchGenerator.HLayout().Layout);
        foreach (var s in layout.Layout!.Shapes)
            await Assert.That(s.Controls is null || s.Controls.Count == 0).IsTrue();
    }

    [Test]
    public async Task ForEditor_recovers_curved_lanes_as_bezier_controls()
    {
        // A curved crossbar is a gentle spline strip — its bends become Bézier handles on the lane shape.
        var layout = SketchLayoutPrep.ForEditor(
            LaneSketchGenerator.Build(new LaneLayoutOptions { Archetype = LaneArchetype.H, CurvedCrossbar = true }).Layout);
        var cross = layout.Layout!.Shapes.First(s => s.Id == "cross");
        await Assert.That(cross.Controls is { Count: > 0 }).IsTrue();
    }

    [Test]
    public async Task ForEditor_rounding_keeps_a_clean_curve_simple()
    {
        // Rounding a gentle (non-overshooting) curve must not introduce a self-intersection — the Bézier
        // sample stays simple, so polygon-clipping shows no phantom hole. (A source polygon that already
        // self-overlaps is the generator's concern; SketchLayoutPrep declines to round it via Smooth.)
        var layout = SketchLayoutPrep.ForEditor(
            LaneSketchGenerator.Build(new LaneLayoutOptions { Archetype = LaneArchetype.H, CurvedCrossbar = true }).Layout);
        foreach (var s in layout.Layout!.Shapes)
            await Assert.That(SelfIntersections(SampleRing(s))).IsEqualTo(0);
    }

    [Test]
    public async Task ForEditor_keeps_the_layout_rasterizable()
    {
        // Recentre + simplify must not break the round-trip into cells (the Finish path).
        var layout = SketchLayoutPrep.ForEditor(LaneSketchGenerator.HLayout().Layout);
        var cells = SketchRasterizer.Rasterize(layout.ToJson());
        await Assert.That(cells.Count).IsGreaterThan(0);
    }

    // ── helpers: sample a shape's (possibly Bézier) ring exactly as the renderer/rasterizer do ──────
    private static List<double[]> SampleRing(SketchShape s, int per = 16)
    {
        var v = s.Vertices!;
        var c = s.Controls;
        var n = v.Length;
        var pts = new List<double[]>();
        for (var i = 0; i < n; i++)
        {
            double[] p0 = v[i], p3 = v[(i + 1) % n];
            var cpOut = c?.GetValueOrDefault(i.ToString())?.Out;
            var cpIn = c?.GetValueOrDefault(((i + 1) % n).ToString())?.In;
            if (cpOut is not null || cpIn is not null)
            {
                double[] a = cpOut ?? p0, b = cpIn ?? p3;
                for (var k = 0; k < per; k++) pts.Add(Cubic(p0, a, b, p3, (double)k / per));
            }
            else
                for (var k = 0; k < per; k++) { var t = (double)k / per; pts.Add([p0[0] + (p3[0] - p0[0]) * t, p0[1] + (p3[1] - p0[1]) * t]); }
        }
        return pts;
    }

    private static double[] Cubic(double[] p0, double[] c1, double[] c2, double[] p3, double t)
    {
        var u = 1 - t;
        double w0 = u * u * u, w1 = 3 * u * u * t, w2 = 3 * u * t * t, w3 = t * t * t;
        return [w0 * p0[0] + w1 * c1[0] + w2 * c2[0] + w3 * p3[0], w0 * p0[1] + w1 * c1[1] + w2 * c2[1] + w3 * p3[1]];
    }

    private static int SelfIntersections(List<double[]> p)
    {
        int m = p.Count, hits = 0;
        for (var i = 0; i < m; i++)
            for (var j = i + 2; j < m; j++)
            {
                if (i == 0 && j == m - 1) continue;   // adjacent closing edge
                if (SegmentsCross(p[i], p[(i + 1) % m], p[j], p[(j + 1) % m])) hits++;
            }
        return hits;
    }

    private static bool SegmentsCross(double[] a, double[] b, double[] c, double[] d) =>
        Ccw(c, d, a) > 0 != Ccw(c, d, b) > 0 && Ccw(a, b, c) > 0 != Ccw(a, b, d) > 0;

    private static double Ccw(double[] p, double[] q, double[] r) =>
        (r[1] - p[1]) * (q[0] - p[0]) - (q[1] - p[1]) * (r[0] - p[0]);
}
