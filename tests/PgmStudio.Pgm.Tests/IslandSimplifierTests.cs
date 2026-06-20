using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="IslandSimplifier"/> simplifies an island outline into an editable <see cref="SketchLayout"/>:
/// a Douglas-Peucker simplified exterior "add" polygon (right-angle outlines collapse to their corners) plus
/// a "subtract" shape per interior hole. Simplification only — no lane cutting.
/// </summary>
public sealed class IslandSimplifierTests
{
    [Test]
    public async Task A_square_simplifies_to_one_add_polygon()
    {
        List<double[]> ext = [[0, 0], [20, 0], [20, 20], [0, 20]];
        var r = IslandSimplifier.Simplify(ext);
        await Assert.That(r.Holes).IsEqualTo(0);
        await Assert.That(r.ExteriorVertices).IsEqualTo(4);
        var shapes = r.Layout.Layout!.Shapes;
        await Assert.That(shapes.Count).IsEqualTo(1);
        await Assert.That(shapes[0].Operation).IsEqualTo("add");
    }

    [Test]
    public async Task A_right_angle_outline_collapses_to_its_corners()
    {
        // a square traced with extra collinear points along each side → Douglas-Peucker keeps only the 4 corners
        List<double[]> ext = [[0, 0], [5, 0], [10, 0], [10, 5], [10, 10], [5, 10], [0, 10], [0, 5]];
        var r = IslandSimplifier.Simplify(ext);
        await Assert.That(r.ExteriorVertices).IsEqualTo(4);
    }

    [Test]
    public async Task A_hole_becomes_a_subtract_shape()
    {
        List<double[]> ext = [[0, 0], [40, 0], [40, 40], [0, 40]];
        List<double[]> hole = [[15, 15], [25, 15], [25, 25], [15, 25]];   // area 100 ≥ minHoleArea
        var r = IslandSimplifier.Simplify(ext, [hole]);
        await Assert.That(r.Holes).IsEqualTo(1);
        var shapes = r.Layout.Layout!.Shapes;
        await Assert.That(shapes.Count).IsEqualTo(2);
        await Assert.That(shapes[0].Operation).IsEqualTo("add");
        await Assert.That(shapes[1].Operation).IsEqualTo("subtract");
    }

    [Test]
    public async Task A_degenerate_outline_yields_nothing()
    {
        List<double[]> ext = [[0, 0], [1, 1]];
        var r = IslandSimplifier.Simplify(ext);
        await Assert.That(r.Layout.Layout!.Shapes.Count).IsEqualTo(0);
    }
}
