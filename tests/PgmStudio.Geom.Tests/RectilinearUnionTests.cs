using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Exact union of grid-aligned rectangles into disjoint boundary rings. A single rect keeps its four corners;
/// a set of bars sharing full-length borders unions into the minimal corner outline (an H, an L); interior
/// edges cancel so no collinear seam survives; disjoint rectangles each surface as their own outer ring.
/// </summary>
public sealed class RectilinearUnionTests
{
    // The single connected outline — throws if the rects do not union to exactly one patch.
    private static List<double[]> Outline(IReadOnlyList<(int, int, int, int)> rects) =>
        RectilinearUnion.Outlines(rects).Single();

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
        var ring = Outline([(5, 5, 15, 15)]);
        await Assert.That(ring.Count).IsEqualTo(4);
        await Assert.That(Norm(ring)).IsEqualTo(Norm([[5, 5], [15, 5], [15, 15], [5, 15]]));
    }

    [Test]
    public async Task Three_bars_union_into_a_twelve_vertex_H()
    {
        var ring = Outline([(5, 25, 15, 55), (-5, 35, 5, 45), (-15, 20, -5, 65)]);
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
        var ring = Outline([(25, 35, 45, 45), (35, 25, 45, 35)]);
        await Assert.That(ring.Count).IsEqualTo(6);
        List<double[]> l = [[25, 45], [45, 45], [45, 25], [35, 25], [35, 35], [25, 35]];
        await Assert.That(Norm(ring)).IsEqualTo(Norm(l));
    }

    [Test]
    public async Task Two_overlapping_rects_union_without_an_interior_seam()
    {
        // an overlapping cross of two rects → a plus sign with 12 corners, no stray collinear points
        var ring = Outline([(0, 10, 30, 20), (10, 0, 20, 30)]);
        await Assert.That(ring.Count).IsEqualTo(12);
    }

    [Test]
    public async Task Disjoint_rects_each_return_their_own_outer_ring()
    {
        // two far-apart rects: two disjoint patches, largest first, neither dropped
        var rings = RectilinearUnion.Outlines([(0, 0, 4, 4), (100, 100, 120, 120)]);
        await Assert.That(rings.Count).IsEqualTo(2);
        await Assert.That(Norm(rings[0])).IsEqualTo(Norm([[100, 100], [120, 100], [120, 120], [100, 120]]));
        await Assert.That(Norm(rings[1])).IsEqualTo(Norm([[0, 0], [4, 0], [4, 4], [0, 4]]));
        // as a set, both patches are present
        await Assert.That(rings.Select(Norm).ToHashSet().SetEquals(
            new[] { Norm([[0, 0], [4, 0], [4, 4], [0, 4]]), Norm([[100, 100], [120, 100], [120, 120], [100, 120]]) }))
            .IsTrue();
    }

    [Test]
    public async Task Empty_input_returns_no_rings()
    {
        await Assert.That(RectilinearUnion.Outlines([]).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_ring_around_an_enclosed_void_carries_only_the_outer_boundary()
    {
        // a square frame (four bars) encloses a hole — one outer ring, the hole is not carried
        var rings = RectilinearUnion.Outlines(
        [
            (0, 0, 30, 10), (0, 20, 30, 30), (0, 10, 10, 20), (20, 10, 30, 20),
        ]);
        await Assert.That(rings.Count).IsEqualTo(1);
        await Assert.That(Norm(rings[0])).IsEqualTo(Norm([[0, 0], [30, 0], [30, 30], [0, 30]]));
    }
}
