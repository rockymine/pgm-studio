using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Synthetic codec tests (no real game files — the 350-map corpus harness lives in
/// tools/PgmStudio.RoundTrip). Covers parsing, wool grouping, id-ref invariants, the
/// built-in filters, coordinate edge cases, and both round-trip checks on a small map.
/// </summary>
public sealed class CodecTests
{
    private const string SampleXml = """
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>Test Map</name>
          <version>1.0.0</version>
          <objective>Capture the wool</objective>
          <teams>
            <team id="red-team" color="red" max="8">Red</team>
            <team id="blue-team" color="blue" max="8">Blue</team>
          </teams>
          <kits><kit id="spawn-kit"><item slot="0" material="DIAMOND_SWORD" enchantment="DAMAGE_ALL:2"/></kit></kits>
          <spawns>
            <spawn team="red-team" kit="spawn-kit" region="red-spawn" yaw="90"/>
            <default><cylinder id="obs" base="0,60,0" radius="1"/></default>
          </spawns>
          <wools team="red-team">
            <wool color="red" location="10,20,30"><monument><block>1,2,3</block></monument></wool>
          </wools>
          <wools team="blue-team">
            <wool color="red" location="10,20,30"><monument><block>4,5,6</block></monument></wool>
          </wools>
          <filters>
            <team id="only-red">red-team</team>
            <all id="red-and-alive"><filter id="only-red"/><alive/></all>
          </filters>
          <regions>
            <cuboid id="red-spawn" min="0,0,0" max="5,5,5"/>
            <union id="bases"><region id="red-spawn"/><cuboid id="blue-spawn" min="10,0,10" max="15,5,15"/></union>
            <apply block="never" region="bases" message="no"/>
          </regions>
          <maxbuildheight>128</maxbuildheight>
        </map>
        """;

    private static MapXml Parse() => MapParser.ParseXmlString(SampleXml);

    [Test]
    public async Task Parses_core_fields()
    {
        var m = Parse();
        await Assert.That(m.Name).IsEqualTo("Test Map");
        await Assert.That(m.Gamemode).IsEqualTo("ctw");          // defaulted (no <gamemode>)
        await Assert.That(m.MaxBuildHeight).IsEqualTo(128);
        await Assert.That(m.Teams.Select(t => t.Id)).Contains("red-team");
        await Assert.That(m.Teams.Select(t => t.Id)).Contains("blue-team");
        await Assert.That(m.Spawns.Count).IsEqualTo(1);
        await Assert.That(m.ObserverSpawn).IsNotNull();
    }

    [Test]
    public async Task Wools_group_by_colour_with_one_monument_per_team()
    {
        var d = Serializer.ToDict(Parse());
        var wools = (List<object?>)d["wools"]!;
        await Assert.That(wools.Count).IsEqualTo(1);             // one colour group ("red")
        var group = (Dict)wools[0]!;
        await Assert.That(group["id"]).IsEqualTo("red");
        var monuments = (List<object?>)group["monuments"]!;
        await Assert.That(monuments.Count).IsEqualTo(2);        // red-team + blue-team
        var monIds = monuments.Select(mo => (string)((Dict)mo!)["id"]!).ToList();
        await Assert.That(monIds).Contains("red-red-team");
        await Assert.That(monIds).Contains("red-blue-team");
    }

    [Test]
    public async Task Builtin_filters_are_always_present()
    {
        var m = Parse();
        await Assert.That(m.Filters.ContainsKey("never")).IsTrue();
        await Assert.That(m.Filters.ContainsKey("always")).IsTrue();
        await Assert.That(m.Filters.ContainsKey("only-red")).IsTrue();
        await Assert.That(m.Filters.ContainsKey("red-and-alive")).IsTrue();
    }

    [Test]
    public async Task Composite_children_are_string_id_refs()
    {
        var d = Serializer.ToDict(Parse());
        var regions = (Dict)d["regions"]!;
        var bases = (Dict)regions["bases"]!;
        var children = (List<object?>)bases["children"]!;
        await Assert.That(children).Contains("red-spawn");
        await Assert.That(children).Contains("blue-spawn");
    }

    [Test]
    public async Task Json_round_trip_is_idempotent()
    {
        var d1 = Serializer.ToDict(Parse());
        var d2 = Serializer.ToDict(Deserializer.FromDict(d1));
        await Assert.That(JsonTree.DeepEquals(JsonTree.Canonical(d1), JsonTree.Canonical(d2))).IsTrue();
    }

    [Test]
    public async Task Xml_re_parse_preserves_named_ids_and_counts()
    {
        var m1 = Parse();
        var m3 = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(Serializer.ToDict(m1))));

        await Assert.That(NamedRegionIds(m3)).IsEquivalentTo(NamedRegionIds(m1));
        await Assert.That(NamedFilterIds(m3)).IsEquivalentTo(NamedFilterIds(m1));
        await Assert.That(m3.Teams.Select(t => t.Id).ToList()).IsEquivalentTo(m1.Teams.Select(t => t.Id).ToList());
        await Assert.That(m3.ApplyRules.Count).IsEqualTo(m1.ApplyRules.Count);
        await Assert.That(m3.Spawns.Count).IsEqualTo(m1.Spawns.Count);
        await Assert.That(m3.Wools.Select(w => $"{w.Team}/{w.Color}").OrderBy(x => x).ToList())
            .IsEquivalentTo(m1.Wools.Select(w => $"{w.Team}/{w.Color}").OrderBy(x => x).ToList());
    }

    [Test]
    public async Task Coordinates_handle_infinity_and_template_variables()
    {
        var m = MapParser.ParseXmlString("""
            <?xml version="1.0"?>
            <map><name>c</name><version>1</version><objective>o</objective>
            <regions>
              <cuboid id="inf-box" min="-oo,${y},0" max="oo,10,10"/>
            </regions></map>
            """);
        var box = m.Regions["inf-box"];
        await Assert.That(double.IsNegativeInfinity(box.MinX!.Value)).IsTrue();
        await Assert.That(box.MinY).IsNull();                    // ${y} template variable → null
        await Assert.That(double.IsPositiveInfinity(box.MaxX!.Value)).IsTrue();

        // serializer encodes ±inf as "oo"/"-oo" and null as null
        var d = Serializer.ToDict(m);
        var min = (Dict)((Dict)((Dict)d["regions"]!)["inf-box"]!)["min"]!;
        await Assert.That(min["x"]).IsEqualTo("-oo");
        await Assert.That(min["y"]).IsNull();
    }

    private static List<string> NamedRegionIds(MapXml m) => m.Regions.Keys.Where(k => !k.Contains("__")).OrderBy(x => x).ToList();
    private static List<string> NamedFilterIds(MapXml m) => m.Filters.Keys.Where(k => !k.Contains("__")).OrderBy(x => x).ToList();
}
