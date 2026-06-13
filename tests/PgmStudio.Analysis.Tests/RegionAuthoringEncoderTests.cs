using PgmStudio.Analysis;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Synthetic checks for the authoring split. Full byte-for-byte corpus parity vs Python's
/// <c>encode_region_authoring</c> is covered by the RoundTrip <c>--authoring</c> harness (350/350).
/// </summary>
public class RegionAuthoringEncoderTests
{
    private static Dict Bounds(double mnx, double mnz, double mxx, double mxz) => new()
    {
        ["min"] = new Dict { ["x"] = mnx, ["z"] = mnz },
        ["max"] = new Dict { ["x"] = mxx, ["z"] = mxz },
    };

    [Test]
    public async Task Splits_PrimitivesFromComposed_WithMembersAndWiring()
    {
        var regions = new Dict
        {
            ["pad"] = new Dict { ["type"] = "rectangle", ["bounds_2d"] = Bounds(0, 0, 10, 10) },
            ["zone"] = new Dict { ["type"] = "union", ["children"] = new List<object?> { "pad" }, ["bounds_2d"] = Bounds(0, 0, 10, 10) },
        };
        var cats = new Dictionary<string, string> { ["pad"] = "build", ["zone"] = "build" };
        var rules = new List<object?> { new Dict { ["region"] = "zone", ["block"] = "never", ["id"] = "rule_0" } };

        var split = RegionAuthoringEncoder.EncodeAuthoring(regions, cats, rules, (0, 0, 10, 10));
        var primitives = (List<object?>)split["primitives"]!;
        var composed = (List<object?>)split["composed"]!;

        await Assert.That(primitives.Count).IsEqualTo(1);
        await Assert.That(composed.Count).IsEqualTo(1);

        var pad = (Dict)primitives[0]!;
        await Assert.That(pad["type"]).IsEqualTo("rectangle");
        await Assert.That(pad["category"]).IsEqualTo("build");

        var zone = (Dict)composed[0]!;
        await Assert.That(zone["type"]).IsEqualTo("union");
        await Assert.That(((List<object?>)zone["member_ids"]!).Cast<string>()).Contains("pad");
        var wiring = (List<object?>)zone["wiring"]!;
        await Assert.That(wiring.Count).IsEqualTo(1);
        await Assert.That((string)((Dict)wiring[0]!)["event"]!).IsEqualTo("block");
        await Assert.That((string)((Dict)wiring[0]!)["value"]!).IsEqualTo("never");
    }

    [Test]
    public async Task PolygonTypes_GetPolygon2d_WhenBoundsProvided()
    {
        // A union of one rectangle → a polygon (union is a PolygonType).
        var regions = new Dict
        {
            ["r"] = new Dict { ["type"] = "rectangle", ["bounds_2d"] = Bounds(0, 0, 4, 4) },
            ["u"] = new Dict { ["type"] = "union", ["children"] = new List<object?> { "r" }, ["bounds_2d"] = Bounds(0, 0, 4, 4) },
        };
        var split = RegionAuthoringEncoder.EncodeAuthoring(regions, new Dictionary<string, string>(), null, (0, 0, 4, 4));
        var u = (Dict)((List<object?>)split["composed"]!)[0]!;
        await Assert.That(u.ContainsKey("polygon_2d")).IsTrue();

        // A rectangle is a primitive (not a PolygonType) → no polygon_2d.
        var r = (Dict)((List<object?>)split["primitives"]!)[0]!;
        await Assert.That(r.ContainsKey("polygon_2d")).IsFalse();
    }
}
