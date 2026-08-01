using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Exact union of grid-aligned rectangles into disjoint boundary rings. A single rect keeps its four corners;
/// a set of bars sharing full-length borders unions into the minimal corner outline (an H, an L); interior
/// edges cancel so no collinear seam survives; disjoint rectangles each surface as their own outer ring.
/// An outline states its patch with the voids it encircles filled in, so <c>EnclosedVoids</c> reports those
/// separately as rectangles — trimmed to what stays open once other ground is taken into account, an L coming
/// back as two — and <c>Difference</c> outlines a union with a second set of rectangles taken back out.
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
        var rings = RectilinearUnion.Outlines(Frame);
        await Assert.That(rings.Count).IsEqualTo(1);
        await Assert.That(Norm(rings[0])).IsEqualTo(Norm([[0, 0], [30, 0], [30, 30], [0, 30]]));
    }

    // A square frame of four bars around a 10×10 void at (10,10)–(20,20).
    private static readonly (int, int, int, int)[] Frame =
        [(0, 0, 30, 10), (0, 20, 30, 30), (0, 10, 10, 20), (20, 10, 30, 20)];

    [Test]
    public async Task The_void_a_ring_encircles_comes_back_as_a_rectangle()
    {
        await Assert.That(RectilinearUnion.EnclosedVoids(Frame))
            .IsEquivalentTo(new[] { (10, 10, 20, 20) });
    }

    [Test]
    public async Task A_union_that_encloses_nothing_has_no_voids()
    {
        await Assert.That(RectilinearUnion.EnclosedVoids([(0, 0, 30, 30)]).Count).IsEqualTo(0);
        // a U opens to the north — its bay reaches the outside, so it is not enclosed
        await Assert.That(RectilinearUnion.EnclosedVoids([(0, 0, 30, 10), (0, 10, 10, 30), (20, 10, 30, 30)]).Count).IsEqualTo(0);
        await Assert.That(RectilinearUnion.EnclosedVoids([]).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_void_something_else_lays_ground_into_is_not_a_void()
    {
        // the filler covers the whole enclosed square — nothing stays open
        await Assert.That(RectilinearUnion.EnclosedVoids(Frame, [(10, 10, 20, 20)]).Count).IsEqualTo(0);
        // one outside the void changes nothing
        await Assert.That(RectilinearUnion.EnclosedVoids(Frame, [(100, 100, 120, 120)]))
            .IsEquivalentTo(new[] { (10, 10, 20, 20) });
        // one taking the void's left half leaves the right half
        await Assert.That(RectilinearUnion.EnclosedVoids(Frame, [(10, 10, 15, 20)]))
            .IsEquivalentTo(new[] { (15, 10, 20, 20) });
    }

    [Test]
    public async Task A_void_shaped_as_an_L_comes_back_as_rectangles_that_tile_it()
    {
        // a filler in the void's top-right corner bends what is left into an L, which no single rectangle
        // states — the pieces it comes back as are whichever tile it, so the property is the tiling
        var voids = RectilinearUnion.EnclosedVoids(Frame, [(15, 10, 20, 15)]);
        await Assert.That(voids.Count).IsEqualTo(2);
        var cells = voids.SelectMany(Cells).ToList();
        await Assert.That(cells.Count).IsEqualTo(cells.Distinct().Count());        // disjoint
        await Assert.That(cells.ToHashSet().SetEquals(
            Cells((10, 10, 20, 20)).Except(Cells((15, 10, 20, 15))))).IsTrue();    // and exactly the L
    }

    private static IEnumerable<(int X, int Z)> Cells((int MinX, int MinZ, int MaxX, int MaxZ) rect)
    {
        for (var x = rect.MinX; x < rect.MaxX; x++)
            for (var z = rect.MinZ; z < rect.MaxZ; z++) yield return (x, z);
    }

    [Test]
    public async Task Every_enclosed_void_of_a_multi_patch_union_is_found()
    {
        var rects = Frame.Concat(Frame.Select(r => (r.Item1 + 100, r.Item2 + 100, r.Item3 + 100, r.Item4 + 100))).ToList();
        await Assert.That(RectilinearUnion.EnclosedVoids(rects).ToHashSet()
            .SetEquals(new[] { (10, 10, 20, 20), (110, 110, 120, 120) })).IsTrue();
    }

    [Test]
    public async Task A_difference_outlines_only_what_survives_the_subtraction()
    {
        // fully covered → nothing
        await Assert.That(RectilinearUnion.Difference([(0, 0, 10, 10)], [(0, 0, 10, 10)]).Count).IsEqualTo(0);
        // nothing taken → the plain outline
        await Assert.That(Norm(RectilinearUnion.Difference([(0, 0, 10, 10)], []).Single()))
            .IsEqualTo(Norm([[0, 0], [10, 0], [10, 10], [0, 10]]));
        // a bar across the middle splits it into two patches, one ring each
        var split = RectilinearUnion.Difference([(0, 0, 10, 30)], [(0, 10, 10, 20)]);
        await Assert.That(split.Count).IsEqualTo(2);
        await Assert.That(split.Select(Norm).ToHashSet().SetEquals(
            new[] { Norm([[0, 0], [10, 0], [10, 10], [0, 10]]), Norm([[0, 20], [10, 20], [10, 30], [0, 30]]) }))
            .IsTrue();
    }
}
