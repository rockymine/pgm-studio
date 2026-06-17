using PgmStudio.Analysis;
using PgmStudio.Pgm;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Resource-block summary tests (renewable auto-config). Exact parity vs Python over the feature maps
/// lives in the RoundTrip harness; these are synthetic.
/// </summary>
public sealed class ResourceSourcesTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>r</name><version>1</version><objective>o</objective>
          <regions><cuboid id="iron-zone" min="0,0,0" max="10,10,10"/></regions>
        </map>
        """;

    [Test]
    public async Task ResourcesInRegion_filters_by_rect_and_counts_renewable_coverage()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        doc["renewables"] = new List<object?> { new Dict { ["region_id"] = "iron-zone", ["renew_filter"] = "iron" } };

        var blocks = new List<ResourceSources.Block>
        {
            new("iron_block", 5, 4, 5),     // inside iron-zone + the rect → renewable
            new("gold_block", 8, 4, 8),     // inside the rect, NOT covered (filter is "iron")
            new("iron_block", 50, 4, 50),   // outside the rect (and the renewable region)
        };

        // Whole map (null bounds): both iron + gold; iron renewable 1 of 2 → not all-renewable.
        var all = ResourceSources.ResourcesInRegion(doc, blocks, null);
        var allIron = all.First(r => r.Type == "iron_block");
        await Assert.That(allIron.Total).IsEqualTo(2);
        await Assert.That(allIron.Renewable).IsEqualTo(1);
        await Assert.That(allIron.AllRenewable).IsFalse();

        // Drawn rect (0,0)-(10,10): the (50,50) iron is excluded.
        var inRect = ResourceSources.ResourcesInRegion(doc, blocks, (0, 0, 10, 10));
        var iron = inRect.First(r => r.Type == "iron_block");
        await Assert.That(iron.Total).IsEqualTo(1);
        await Assert.That(iron.Renewable).IsEqualTo(1);
        await Assert.That(iron.AllRenewable).IsTrue();

        var gold = inRect.First(r => r.Type == "gold_block");
        await Assert.That(gold.Total).IsEqualTo(1);
        await Assert.That(gold.Renewable).IsEqualTo(0);     // the "iron" filter doesn't cover gold
        await Assert.That(gold.AllRenewable).IsFalse();
    }
}
