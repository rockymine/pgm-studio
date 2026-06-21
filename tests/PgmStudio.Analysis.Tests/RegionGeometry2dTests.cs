using PgmStudio.Analysis.Region;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Footprint-geometry resolution from region dicts, with a focus on degenerate inputs that must not
/// crash the categorizer/IoU path (zero-area rectangles, empty compounds).
/// </summary>
public sealed class RegionGeometry2dTests
{
    private static Dict Pt(double x, double z) => new() { ["x"] = x, ["z"] = z };
    private static Dict Rect(double mnx, double mnz, double mxx, double mxz) => new()
    {
        ["type"] = "rectangle", ["min"] = Pt(mnx, mnz), ["max"] = Pt(mxx, mxz),
    };

    private static readonly (double, double, double, double) Bounds = (0, 0, 100, 100);

    [Test]
    public async Task Rectangle_coversExpectedCells()
    {
        var geom = RegionGeometry2d.ToGeometry(Rect(0, 0, 10, 10), Bounds, new Dict());
        await Assert.That(geom).IsNotNull();
        await Assert.That(geom!.CoversCell(5, 5)).IsTrue();   // centre 5.5,5.5 inside
        await Assert.That(geom.CoversCell(20, 20)).IsFalse();
    }

    [Test]
    public async Task DegenerateRectangle_zeroWidth_doesNotThrow_andCoversNothing()
    {
        // A zero-width rectangle (min.x == max.x): the NTS envelope collapses to a line, which must
        // not crash the resolver. Its cell-centre footprint covers nothing.
        var geom = RegionGeometry2d.ToGeometry(Rect(5, 0, 5, 10), Bounds, new Dict());
        await Assert.That(geom is null || geom.IsEmpty || !geom.CoversCell(5, 5)).IsTrue();
    }

    [Test]
    public async Task DegenerateRectangle_singlePoint_doesNotThrow()
    {
        // min == max collapses the envelope to a point.
        var geom = RegionGeometry2d.ToGeometry(Rect(5, 5, 5, 5), Bounds, new Dict());
        await Assert.That(geom is null || geom.IsEmpty).IsTrue();
    }

    [Test]
    public async Task DegenerateRectangle_insideUnion_doesNotThrow()
    {
        // A degenerate child inside a compound must not break the union of the valid sibling.
        var registry = new Dict
        {
            ["good"] = Rect(0, 0, 10, 10),
            ["flat"] = Rect(5, 0, 5, 10),
        };
        var union = new Dict { ["type"] = "union", ["children"] = new List<object?> { "good", "flat" } };
        var geom = RegionGeometry2d.ToGeometry(union, Bounds, registry);
        await Assert.That(geom).IsNotNull();
        await Assert.That(geom!.CoversCell(5, 5)).IsTrue();
    }
}
