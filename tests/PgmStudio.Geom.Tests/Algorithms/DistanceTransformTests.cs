using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>The distance field is exact: every cell reads the straight-line distance to the nearest source,
/// not a chamfer's approximation of it.</summary>
public sealed class DistanceTransformTests
{
    [Test]
    public async Task Every_cell_reads_the_exact_distance_to_its_nearest_source()
    {
        const int width = 23, depth = 17;
        var random = new Random(7);
        var source = new bool[width * depth];
        for (var cell = 0; cell < source.Length; cell++) source[cell] = random.NextDouble() < 0.04;
        source[5 * width + 9] = true;

        var field = DistanceTransform.ToNearest(source, width, depth);
        for (var cell = 0; cell < source.Length; cell++)
        {
            var best = double.PositiveInfinity;
            for (var other = 0; other < source.Length; other++)
                if (source[other])
                    best = Math.Min(best, Math.Sqrt(Math.Pow(cell % width - other % width, 2) + Math.Pow(cell / width - other / width, 2)));
            await Assert.That(field[cell]).IsEqualTo(best).Within(1e-9);
        }
    }

    [Test]
    public async Task A_grid_with_no_source_is_infinitely_far_from_one()
    {
        var field = DistanceTransform.ToNearest(new bool[12], 4, 3);
        await Assert.That(field.All(double.IsPositiveInfinity)).IsTrue();
    }
}
