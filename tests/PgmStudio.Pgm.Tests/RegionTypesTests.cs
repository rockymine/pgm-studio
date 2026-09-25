using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>Every region element PGM parses (its <c>RegionParser</c>'s <c>@MethodParser</c> names) is read
/// here or refused. A region the parser drops leaves the rule naming it with no region, and a rule with no
/// region is read as applying everywhere, so a missing type is not a smaller map but a wrong one.</summary>
public sealed class RegionTypesTests
{
    private static MapXml Parse(string regions) => MapParser.ParseXmlString(
        $"""<?xml version="1.0"?><map proto="1.4.0"><name>m</name><version>1</version><objective>o</objective><regions>{regions}</regions></map>""");

    /// <summary>PGM's region vocabulary, one element per name, each with the smallest body it accepts.</summary>
    public static IEnumerable<(string Tag, string Xml)> PgmRegions() =>
    [
        ("above", """<above id="r" y="5"/>"""),
        ("below", """<below id="r" y="5"/>"""),
        ("block", """<block id="r">1,2,3</block>"""),
        ("circle", """<circle id="r" center="0,0" radius="4"/>"""),
        ("complement", """<complement id="r"><everywhere/><rectangle min="0,0" max="4,4"/></complement>"""),
        ("cuboid", """<cuboid id="r" min="0,0,0" max="4,4,4"/>"""),
        ("cylinder", """<cylinder id="r" base="0,0,0" radius="4" height="2"/>"""),
        ("empty", """<empty id="r"/>"""),
        ("everywhere", """<everywhere id="r"/>"""),
        ("half", """<half id="r" origin="0,0,0" normal="1,0,0"/>"""),
        ("intersect", """<intersect id="r"><rectangle min="0,0" max="4,4"/><rectangle min="2,2" max="6,6"/></intersect>"""),
        ("mirror", """<rectangle id="s" min="0,0" max="4,4"/><mirror id="r" origin="0,0,0" normal="1,0,0"><region id="s"/></mirror>"""),
        ("negative", """<negative id="r"><rectangle min="0,0" max="4,4"/></negative>"""),
        ("nowhere", """<nowhere id="r"/>"""),
        ("point", """<point id="r">1,2,3</point>"""),
        ("rectangle", """<rectangle id="r" min="0,0" max="4,4"/>"""),
        ("sphere", """<sphere id="r" origin="0,0,0" radius="4"/>"""),
        ("translate", """<rectangle id="s" min="0,0" max="4,4"/><translate id="r" offset="1,0,1"><region id="s"/></translate>"""),
        ("union", """<union id="r"><rectangle min="0,0" max="4,4"/><rectangle min="8,8" max="9,9"/></union>"""),
    ];

    [Test]
    [MethodDataSource(nameof(PgmRegions))]
    public async Task Every_region_PGM_parses_is_read(string tag, string xml)
    {
        var map = Parse(xml);
        await Assert.That(map.Regions.ContainsKey("r")).IsTrue().Because($"<{tag}> is a region PGM reads");
    }

    [Test]
    public async Task A_region_type_the_studio_cannot_read_refuses_the_map()
    {
        await Assert.That(() => Parse("""<resize id="r" min="-1,0,-1" max="1,0,1"><rectangle min="0,0" max="4,4"/></resize>"""))
            .Throws<UnsupportedMapException>();
    }

    [Test]
    public async Task A_region_wrapper_with_several_children_is_their_union()
    {
        var map = MapParser.ParseXmlString(
            """<?xml version="1.0"?><map proto="1.4.0"><name>m</name><version>1</version><objective>o</objective><regions>"""
            + """<apply block="never"><region><circle center="0,0" radius="4"/><rectangle min="10,10" max="12,12"/></region></apply>"""
            + "</regions></map>");
        var rule = map.ApplyRules.Single();
        var region = map.Regions[rule.RegionId];
        await Assert.That(region.Type).IsEqualTo("union");
        await Assert.That(region.Children.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Above_and_below_keep_every_axis_they_state()
    {
        var map = Parse("""<above id="a" x="5"/><below id="b" y="7" z="-3"/>""");
        await Assert.That(map.Regions["a"].HalfX).IsEqualTo(5);
        await Assert.That(map.Regions["a"].HalfY).IsNull();
        await Assert.That(map.Regions["b"].Type).IsEqualTo("below");
        await Assert.That(map.Regions["b"].HalfY).IsEqualTo(7);
        await Assert.That(map.Regions["b"].HalfZ).IsEqualTo(-3);
    }
}
