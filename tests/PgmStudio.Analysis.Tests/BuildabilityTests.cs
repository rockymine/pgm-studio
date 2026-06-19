using PgmStudio.Analysis.Playability;
using PgmStudio.Analysis.Region;
using PgmStudio.Pgm;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Synthetic buildability/geometry tests (exact parity vs Python's grid is in
/// tools/PgmStudio.RoundTrip --buildability over the feature maps).
/// </summary>
public sealed class BuildabilityTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>b</name><version>1</version><objective>o</objective>
          <regions>
            <cuboid id="arena" min="0,0,0" max="10,10,10"/>
            <apply region="arena" block="never"/>
          </regions>
        </map>
        """;

    [Test]
    public async Task Never_rule_marks_only_cells_inside_the_region()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        // all columns solid → no void; only the "never" rule acts.
        var bbox = Buildability.RegionBbox(doc, 16);
        var y0 = new HashSet<(int, int)>();
        for (var x = bbox.minX; x < bbox.maxX; x++)
            for (var z = bbox.minZ; z < bbox.maxZ; z++)
                y0.Add((x, z));

        var res = Buildability.Compute(doc, y0);

        // box(0,0,10,10) strictly contains the 10×10 block of cell centres 0.5..9.5
        await Assert.That(res.Counts["never"]).IsEqualTo(100);
        await Assert.That(res.Counts["void_denied"]).IsEqualTo(0);          // every column solid
        await Assert.That(res.Counts["buildable"]).IsEqualTo(res.Width * res.Height - 100);
        await Assert.That(res.HasY0).IsTrue();
    }

    [Test]
    public async Task Region_geometry_contains_matches_footprint()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        var regions = (Dictionary<string, object?>)doc["regions"]!;
        var arena = (Dictionary<string, object?>)regions["arena"]!;
        var geom = RegionGeometry2d.ToGeometry(arena, (-100, -100, 100, 100), regions);

        await Assert.That(geom).IsNotNull();
        await Assert.That(geom!.Contains(new NetTopologySuite.Geometries.Point(5, 5))).IsTrue();
        await Assert.That(geom.Contains(new NetTopologySuite.Geometries.Point(50, 50))).IsFalse();
    }
}
