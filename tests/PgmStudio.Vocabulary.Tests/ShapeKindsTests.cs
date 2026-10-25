using PgmStudio.Vocabulary;

namespace PgmStudio.Vocabulary.Tests;

/// <summary>The words a shape names itself by: five drawn by outline and six that stand in the block grid, each of
/// the six read through the footprint of one of the five.</summary>
public sealed class ShapeKindsTests
{
    [Test]
    public async Task Every_kind_is_drawn_or_a_solid_and_never_both()
    {
        await Assert.That(ShapeKinds.All.Length).IsEqualTo(ShapeKinds.Drawn.Length + ShapeKinds.Solids.Length);
        await Assert.That(ShapeKinds.All.Distinct().Count()).IsEqualTo(ShapeKinds.All.Length);
    }

    [Test]
    public async Task A_solid_is_drawn_as_the_footprint_of_a_drawn_kind()
    {
        foreach (var solid in ShapeKinds.Solids)
            await Assert.That(ShapeKinds.Drawn).Contains(ShapeKinds.Footprint(solid));

        await Assert.That(ShapeKinds.Footprint(ShapeKinds.Box)).IsEqualTo(ShapeKinds.Rectangle);
        await Assert.That(ShapeKinds.Footprint(ShapeKinds.Wedge)).IsEqualTo(ShapeKinds.Rectangle);
        await Assert.That(ShapeKinds.Footprint(ShapeKinds.Sphere)).IsEqualTo(ShapeKinds.Circle);
        await Assert.That(ShapeKinds.Footprint(ShapeKinds.Arch)).IsEqualTo(ShapeKinds.Polyline);
        await Assert.That(ShapeKinds.Footprint(ShapeKinds.Lasso)).IsEqualTo(ShapeKinds.Lasso);
    }
}
