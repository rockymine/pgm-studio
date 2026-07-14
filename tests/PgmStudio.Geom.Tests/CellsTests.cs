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
    public async Task BoundingBox_is_the_inclusive_extent()
    {
        var (mnx, mnz, mxx, mxz) = Cells.BoundingBox(Set((2, 3), (5, 1), (4, 7)));
        await Assert.That(mnx).IsEqualTo(2);
        await Assert.That(mnz).IsEqualTo(1);
        await Assert.That(mxx).IsEqualTo(5);
        await Assert.That(mxz).IsEqualTo(7);
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
    public async Task HasBay_is_true_for_a_single_edge_notch_and_false_for_a_solid_block()
    {
        // a U: two arms + a floor, the notch open only on the top edge
        var u = Set((0, 0), (2, 0), (0, 1), (2, 1), (0, 2), (1, 2), (2, 2));
        await Assert.That(Cells.HasBay(u)).IsTrue();
        await Assert.That(Cells.HasBay(Rect(0, 0, 3, 3))).IsFalse();
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
    public async Task ShortestPath_in_open_space_is_the_manhattan_distance()
    {
        // a diagonal target: rectilinear (no diagonal shortcut) → dx + dz steps, not the Euclidean 5
        await Assert.That(Cells.PathLength((0, 0), (3, 4), Rect(0, 0, 8, 8))).IsEqualTo(7);
    }

    [Test]
    public async Task ShortestPath_routes_around_an_obstacle_hugging_its_border()
    {
        // a U of walkable cells: a wall of removed cells splits the two arms, so the path must go down one arm,
        // across the base, and up the other — longer than the straight 2-step gap between the arm tops.
        var u = Rect(0, 0, 5, 5);
        for (var z = 1; z <= 4; z++) { u.Remove((2, z)); }      // a wall from the top, base open at z=0
        var len = Cells.PathLength((1, 4), (3, 4), u);
        await Assert.That(len).IsNotNull();
        await Assert.That(len!.Value).IsGreaterThan(2);         // not the straight-line 2 — it detours the wall
        await Assert.That(len!.Value).IsEqualTo(10);            // down 4, across 2, up 4
    }

    [Test]
    public async Task ShortestPath_is_null_across_a_disconnected_gap()
    {
        var split = Rect(0, 0, 2, 2);
        split.UnionWith(Rect(0, 5, 2, 2));                      // two blocks with a void between
        await Assert.That(Cells.PathLength((0, 0), (0, 5), split)).IsNull();
        await Assert.That(Cells.ShortestPath((0, 0), (0, 5), split)).IsNull();
    }

    [Test]
    public async Task ShortestPath_returns_the_cell_sequence_including_both_ends()
    {
        var path = Cells.ShortestPath((0, 0), (0, 3), Rect(0, 0, 1, 4));
        await Assert.That(path).IsNotNull();
        await Assert.That(path![0]).IsEqualTo((0, 0));
        await Assert.That(path[^1]).IsEqualTo((0, 3));
        await Assert.That(path.Count).IsEqualTo(4);
    }
}
