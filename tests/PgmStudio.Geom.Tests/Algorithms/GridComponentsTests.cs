using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Grid connected components: orthogonal vs diagonal connectivity split a diagonal pair differently, a join
/// predicate can hold neighbours apart, components come back in raster seed order, and the empty set yields none.
/// </summary>
public sealed class GridComponentsTests
{
    [Test]
    public async Task A_diagonal_pair_is_two_components_at_4_and_one_at_8()
    {
        (int, int)[] cells = [(0, 0), (1, 1)];
        await Assert.That(GridComponents.Label(cells, connectivity: 4).Count).IsEqualTo(2);
        await Assert.That(GridComponents.Label(cells, connectivity: 8).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Two_disjoint_blobs_are_two_components()
    {
        (int, int)[] cells = [(0, 0), (1, 0), (0, 1), (10, 10), (11, 10)];
        var comps = GridComponents.Label(cells, connectivity: 4);
        await Assert.That(comps.Count).IsEqualTo(2);
        await Assert.That(comps.Sum(c => c.Count)).IsEqualTo(5);
    }

    [Test]
    public async Task A_join_predicate_can_keep_adjacent_cells_apart()
    {
        (int, int)[] cells = [(0, 0), (1, 0)];
        var comps = GridComponents.Label(cells, connectivity: 4, canJoin: (_, _) => false);
        await Assert.That(comps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Components_are_returned_in_raster_seed_order()
    {
        // Seeds sort by x then z, so the component seeded at (0,0) comes before the one at (5,5).
        (int, int)[] cells = [(5, 5), (0, 0)];
        var comps = GridComponents.Label(cells, connectivity: 4);
        await Assert.That(comps[0][0]).IsEqualTo((0, 0));
        await Assert.That(comps[1][0]).IsEqualTo((5, 5));
    }

    [Test]
    public async Task The_empty_set_yields_no_components()
    {
        await Assert.That(GridComponents.Label([], connectivity: 8).Count).IsEqualTo(0);
    }
}
