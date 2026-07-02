using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Exact union of grid-aligned rectangles into a boundary ring. A single rect keeps its four corners; a set
/// of bars sharing full-length borders unions into the minimal corner outline (an H, an L); interior edges
/// cancel so no collinear seam survives.
/// </summary>
public sealed class RectilinearUnionTests
{
    // A ring is compared as a cyclic vertex sequence, winding- and start-independent.
    private static string Norm(IReadOnlyList<double[]> ring)
    {
        var pts = ring.Select(v => ((int)v[0], (int)v[1])).ToList();
        if (pts.Count == 0) return "";
        // signed area to pick a canonical winding (CCW)
        double a = 0;
        for (int i = 0, n = pts.Count; i < n; i++) { var p = pts[i]; var q = pts[(i + 1) % n]; a += p.Item1 * q.Item2 - q.Item1 * p.Item2; }
        if (a < 0) pts.Reverse();
        var start = pts.IndexOf(pts.Min());
        var seq = Enumerable.Range(0, pts.Count).Select(i => pts[(start + i) % pts.Count]);
        return string.Join(";", seq.Select(p => $"{p.Item1},{p.Item2}"));
    }

    [Test]
    public async Task A_single_rectangle_keeps_its_four_corners()
    {
        var ring = RectilinearUnion.Outline([(5, 5, 15, 15)]);
        await Assert.That(ring.Count).IsEqualTo(4);
        await Assert.That(Norm(ring)).IsEqualTo(Norm([[5, 5], [15, 5], [15, 15], [5, 15]]));
    }

    [Test]
    public async Task Three_bars_union_into_a_twelve_vertex_H()
    {
        var ring = RectilinearUnion.Outline([(5, 25, 15, 55), (-5, 35, 5, 45), (-15, 20, -5, 65)]);
        await Assert.That(ring.Count).IsEqualTo(12);
        // the seed's "h" polygon
        List<double[]> h =
        [
            [5, 25], [15, 25], [15, 55], [5, 55], [5, 45], [-5, 45],
            [-5, 65], [-15, 65], [-15, 20], [-5, 20], [-5, 35], [5, 35],
        ];
        await Assert.That(Norm(ring)).IsEqualTo(Norm(h));
    }

    [Test]
    public async Task Two_rects_union_into_a_six_vertex_L()
    {
        var ring = RectilinearUnion.Outline([(25, 35, 45, 45), (35, 25, 45, 35)]);
        await Assert.That(ring.Count).IsEqualTo(6);
        List<double[]> l = [[25, 45], [45, 45], [45, 25], [35, 25], [35, 35], [25, 35]];
        await Assert.That(Norm(ring)).IsEqualTo(Norm(l));
    }

    [Test]
    public async Task Two_overlapping_rects_union_without_an_interior_seam()
    {
        // an overlapping cross of two rects → a plus sign with 12 corners, no stray collinear points
        var ring = RectilinearUnion.Outline([(0, 10, 30, 20), (10, 0, 20, 30)]);
        await Assert.That(ring.Count).IsEqualTo(12);
    }

    [Test]
    public async Task Disjoint_rects_return_one_outer_ring()
    {
        // two far-apart rects: the outer ring is the larger one
        var ring = RectilinearUnion.Outline([(0, 0, 4, 4), (100, 100, 120, 120)]);
        await Assert.That(ring.Count).IsEqualTo(4);
        await Assert.That(Norm(ring)).IsEqualTo(Norm([[100, 100], [120, 100], [120, 120], [100, 120]]));
    }
}
