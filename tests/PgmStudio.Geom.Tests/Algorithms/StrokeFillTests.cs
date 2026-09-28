using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>The ground a stroke paves. What matters here is that a wandering stroke paves round its wandering
/// line — the band leaves the straight corridor its points alone would give, and still reaches both ends.</summary>
public sealed class StrokeFillTests
{
    private static readonly double[][] StraightLine = [[0, 0], [40, 0]];

    [Test]
    public async Task A_wandering_stroke_paves_off_the_straight_band_and_still_reaches_both_ends()
    {
        var straight = StrokeFill.Cells(StraightLine, 1, StrokeStyle.Solid, 1, seed: 7).ToHashSet();
        var wandering = StrokeFill.Cells(StraightLine, 1, StrokeStyle.Solid, 1, seed: 7, wander: 4, wanderLength: 16).ToHashSet();

        await Assert.That(straight.All(cell => Math.Abs(cell.Z) <= 1)).IsTrue();
        await Assert.That(wandering.Any(cell => Math.Abs(cell.Z) >= 3)).IsTrue();
        await Assert.That(wandering).Contains(new StrokeCell(0, 0));
        await Assert.That(wandering).Contains(new StrokeCell(40, 0));
    }
}
