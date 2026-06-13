using PgmStudio.Analysis;
using PgmStudio.Pgm;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Synthetic categorizer tests (the 350-map exact-parity-vs-Python harness lives in
/// tools/PgmStudio.RoundTrip --categorize). Covers the core category signals + a role.
/// </summary>
public sealed class RegionCategorizerTests
{
    // spawn (referenced by a spawn), monument (referenced by a wool monument), a named
    // wool-room, and a void-complement build region with an apply rule carrying enter wiring.
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>Cat</name><version>1</version><objective>o</objective>
          <teams><team id="red-team" color="red">Red</team><team id="blue-team" color="blue">Blue</team></teams>
          <spawns><spawn team="red-team" region="red-spawn"/></spawns>
          <wools team="red-team">
            <wool color="red" location="0,0,0" monument="red-monument"><monument><block>1,1,1</block></monument></wool>
          </wools>
          <filters>
            <deny id="no-build"><void/></deny>
            <team id="only-red">red-team</team>
          </filters>
          <regions>
            <cuboid id="red-spawn" min="0,0,0" max="4,4,4"/>
            <block id="red-monument">1,1,1</block>
            <cuboid id="blue-wool-room" min="20,0,20" max="25,5,25"/>
            <cuboid id="playable" min="-50,0,-50" max="50,20,50"/>
            <negative id="void-wrapper"><region id="playable"/></negative>
            <apply region="void-wrapper" block="no-build"/>
            <apply region="red-spawn" enter="only-red"/>
          </regions>
        </map>
        """;

    [Test]
    public async Task Categorizes_core_gameplay_signals()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        var facets = RegionCategorizer.DeriveFacets(doc);

        await Assert.That(facets["red-spawn"].Category).IsEqualTo("spawn");          // referenced by a spawn
        await Assert.That(facets["red-monument"].Category).IsEqualTo("monument");    // wool monument region
        await Assert.That(facets["blue-wool-room"].Category).IsEqualTo("wool_room"); // name heuristic
        await Assert.That(facets["playable"].Category).IsEqualTo("build");           // carved out of the void-complement
        await Assert.That(facets["void-wrapper"].Roles).Contains("rule_container");  // negative wrapper role
    }

    [Test]
    public async Task Categorize_flat_map_covers_all_regions()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        var cats = RegionCategorizer.Categorize(doc);
        var regions = (Dictionary<string, object?>)doc["regions"]!;
        await Assert.That(cats.Count).IsEqualTo(regions.Count);   // every region categorised
    }
}
