namespace PgmStudio.Geom.Tests;

/// <summary>A wall laid along a grid line fills the two columns either side of it, exactly as a box two blocks
/// thick does, one at any other angle fills a band the same width, square at both ends, and one that bends turns
/// without a notch at the bend.</summary>
public sealed class WallLineTests
{
    [Test]
    public async Task A_line_on_a_grid_line_fills_the_box_two_blocks_thick()
    {
        var cells = WallLine.Cells([(11, 0), (11, 20)]).ToHashSet();
        var box = Enumerable.Range(10, 2).SelectMany(x => Enumerable.Range(0, 20).Select(z => (x, z))).ToHashSet();

        await Assert.That(cells.SetEquals(box)).IsTrue();
        await Assert.That(WallLine.Bounds([(11, 0), (11, 20)])).IsEqualTo(new BlockRect(10, 0, 12, 20));
    }

    [Test]
    public async Task A_diagonal_line_is_as_thick_across_as_a_straight_one()
    {
        var cells = WallLine.Cells([(0, 0), (20, 20)]).ToList();

        // Every column of the band lies within one block of the line, and none past its ends.
        foreach (var (x, z) in cells)
        {
            var across = Math.Abs(x + 0.5 - (z + 0.5)) / Math.Sqrt(2);
            await Assert.That(across).IsLessThanOrEqualTo(1);
            await Assert.That(x + z + 1).IsGreaterThan(0).And.IsLessThan(40);
        }
        await Assert.That(cells).Contains((10, 10));
        await Assert.That(cells).Contains((10, 9));
        await Assert.That(cells).Contains((9, 10));
    }

    [Test]
    public async Task A_wall_bent_square_fills_the_outside_of_its_bend()
    {
        var cells = WallLine.Cells([(0, 0), (10, 0), (10, 10)]).ToList();
        var legs = WallLine.Cells([(0, 0), (10, 0)]).Concat(WallLine.Cells([(10, 0), (10, 10)])).ToHashSet();

        await Assert.That(cells).Contains((10, -1)).Because("the outer corner of the bend");
        await Assert.That(cells.Count).IsEqualTo(cells.Distinct().Count());
        await Assert.That(legs.IsSubsetOf(cells)).IsTrue();
        await Assert.That(cells.Count - legs.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Corners_at_one_point_are_no_wall()
    {
        await Assert.That(WallLine.Cells([(4, 4), (4, 4)]).Any()).IsFalse();
    }
}
