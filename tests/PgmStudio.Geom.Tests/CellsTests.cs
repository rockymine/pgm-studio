using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The rectilinear cell-set substrate: neighbour iteration, flood fill, connected components, enclosed-void
/// detection, reflex-corner counting, bay detection, bounding box, and min run width — the shared primitives the
/// shape classifier and lane read are built on.
/// </summary>
public sealed class CellsTests
{
    private static HashSet<(int, int)> Set(params (int, int)[] cells) => [.. cells];

    private static HashSet<(int, int)> Rect(int x0, int z0, int w, int h)
    {
        var s = new HashSet<(int, int)>();
        for (var x = x0; x < x0 + w; x++) for (var z = z0; z < z0 + h; z++) s.Add((x, z));
        return s;
    }

    [Test]
    public async Task N4_yields_the_four_orthogonal_neighbours()
    {
        var got = Cells.N4((3, 5)).ToHashSet();
        await Assert.That(got).IsEquivalentTo(new HashSet<(int, int)> { (4, 5), (2, 5), (3, 6), (3, 4) });
    }

    [Test]
    public async Task BoundingBox_spans_every_cell()
    {
        // both bounding corners are occupied cells, so the extent counts them: x 2..5 is 4 cells wide.
        var b = Cells.BoundingBox(Set((2, 3), (5, 1), (4, 7)));
        await Assert.That(b).IsEqualTo(new CellRect(2, 1, 4, 7));
        await Assert.That(b.MaxX).IsEqualTo(6).Because("MaxX is one past the far cell");
        await Assert.That(b.MaxZ).IsEqualTo(8);
    }

    [Test]
    public async Task Flood_returns_only_the_component_reachable_from_the_seed()
    {
        var within = Rect(0, 0, 2, 2);              // component A
        within.UnionWith(Rect(10, 10, 2, 2));       // component B, disjoint
        var got = Cells.Flood(new[] { (0, 0) }, within);
        await Assert.That(got).IsEquivalentTo(Rect(0, 0, 2, 2));
    }

    [Test]
    public async Task Components_counts_disjoint_groups()
    {
        var two = Rect(0, 0, 2, 2);
        two.UnionWith(Rect(5, 5, 3, 1));
        await Assert.That(Cells.Components(two)).IsEqualTo(2);
        await Assert.That(Cells.Components(Rect(0, 0, 4, 4))).IsEqualTo(1);
    }

    [Test]
    public async Task HasEnclosedVoid_is_true_for_a_ring_and_false_for_a_solid_block()
    {
        var ring = Rect(0, 0, 3, 3); ring.Remove((1, 1));   // 3×3 with the centre punched out
        await Assert.That(Cells.HasEnclosedVoid(ring)).IsTrue();
        await Assert.That(Cells.HasEnclosedVoid(Rect(0, 0, 3, 3))).IsFalse();
    }

    [Test]
    public async Task HasDiagonalPinch_flags_point_touches_but_not_solid_or_three_quarter_corners()
    {
        // two cells on a diagonal, the other two of the 2×2 window void → a point-to-point pinch
        await Assert.That(Cells.HasDiagonalPinch(Set((0, 0), (1, 1)))).IsTrue();
        await Assert.That(Cells.HasDiagonalPinch(Set((1, 0), (0, 1)))).IsTrue();
        // a ¾-solid inside corner (three of four filled): a third cell bridges the diagonal → clean
        await Assert.That(Cells.HasDiagonalPinch(Set((0, 0), (1, 1), (1, 0)))).IsFalse();
        // solid blocks, bars and an L never pinch
        await Assert.That(Cells.HasDiagonalPinch(Rect(0, 0, 3, 3))).IsFalse();
        await Assert.That(Cells.HasDiagonalPinch(Rect(0, 0, 5, 1))).IsFalse();
        await Assert.That(Cells.HasDiagonalPinch(Set((0, 0), (0, 1), (0, 2), (1, 2), (2, 2)))).IsFalse();
        // a ring encloses a void but its corners are all solid → no pinch
        var ring = Rect(0, 0, 3, 3); ring.Remove((1, 1));
        await Assert.That(Cells.HasDiagonalPinch(ring)).IsFalse();
    }

    [Test]
    public async Task ReflexCorners_counts_concave_turns()
    {
        // a straight bar and a solid rectangle are convex everywhere → 0
        await Assert.That(Cells.ReflexCorners(Rect(0, 0, 5, 1))).IsEqualTo(0);
        await Assert.That(Cells.ReflexCorners(Rect(0, 0, 4, 4))).IsEqualTo(0);
        // an L has exactly one concave (inner) corner
        var l = Set((0, 0), (0, 1), (0, 2), (1, 2), (2, 2));
        await Assert.That(Cells.ReflexCorners(l)).IsEqualTo(1);
    }

    [Test]
    public async Task HasFold_is_true_when_a_line_crosses_two_runs_and_false_for_staircases()
    {
        // a U: two arms + a floor — the rows through the notch cross two runs
        var u = Set((0, 0), (2, 0), (0, 1), (2, 1), (0, 2), (1, 2), (2, 2));
        await Assert.That(Cells.HasFold(u)).IsTrue();
        // solid blocks and straight bars have single runs everywhere
        await Assert.That(Cells.HasFold(Rect(0, 0, 3, 3))).IsFalse();
        await Assert.That(Cells.HasFold(Rect(0, 0, 5, 1))).IsFalse();
        // a Z staircase is orthogonally convex — every row and column is one run
        var z = Set((0, 0), (1, 0), (1, 1), (2, 1), (3, 1), (3, 2));
        await Assert.That(Cells.HasFold(z)).IsFalse();
        // the same staircase folded back at its end crosses two runs on the top row
        var hook = Set((0, 0), (1, 0), (1, 1), (2, 1), (3, 1), (3, 0));
        await Assert.That(Cells.HasFold(hook)).IsTrue();
    }

    [Test]
    public async Task MinRunWidth_is_the_clamped_cross_section()
    {
        // a 3×3 block seeded at its centre: both runs are 3 → 3
        await Assert.That(Cells.MinRunWidth(Rect(0, 0, 3, 3), new[] { (1, 1) })).IsEqualTo(3);
        // a 1-wide corridor clamps up to the floor of 2
        await Assert.That(Cells.MinRunWidth(Rect(0, 0, 1, 5), new[] { (0, 2) })).IsEqualTo(2);
        // an 8×8 block clamps down to the ceiling of 6
        await Assert.That(Cells.MinRunWidth(Rect(0, 0, 8, 8), new[] { (4, 4) })).IsEqualTo(6);
    }

    [Test]
    public async Task SnapToWalkable_returns_the_cell_itself_when_already_walkable()
    {
        var within = Rect(0, 0, 3, 3);
        await Assert.That(Cells.SnapToWalkable((1, 1), within, radius: 2)).IsEqualTo((1, 1));
    }

    [Test]
    public async Task SnapToWalkable_finds_the_nearest_ring_cell_by_the_documented_tie_break()
    {
        // both (0, -1) and (-1, 0) sit at Chebyshev ring 1 from the origin. The tie-break is increasing Z
        // then increasing X, and (0, -1) has the lesser Z, so it wins even though (-1, 0) has the lesser X.
        var within = Set((0, -1), (-1, 0));
        await Assert.That(Cells.SnapToWalkable((0, 0), within, radius: 2)).IsEqualTo((0, -1));
    }

    [Test]
    public async Task SnapToWalkable_prefers_a_diagonal_corner_over_a_farther_cardinal_cell_by_chebyshev_ring()
    {
        // (1, 1) is at Chebyshev ring 1 (a diagonal corner); (2, 0) is at ring 2. The square ring reaches
        // the corner first, unlike a Manhattan ring which would have ranked them the other way (both at
        // Manhattan distance 2).
        var within = Set((1, 1), (2, 0));
        await Assert.That(Cells.SnapToWalkable((0, 0), within, radius: 2)).IsEqualTo((1, 1));
    }

    [Test]
    public async Task SnapToWalkable_is_null_past_the_radius()
    {
        var within = Set((5, 5));
        await Assert.That(Cells.SnapToWalkable((0, 0), within, radius: 2)).IsNull();
    }
}
