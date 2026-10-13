using System.Xml.Linq;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A portal survives both codecs unchanged: its coordinates as written — a place after <c>@</c>, an offset
/// without — its region and destination named or inline, and its filters. A <c>&lt;portals&gt;</c> group
/// hands its attributes to every portal under it.
/// </summary>
public sealed class PortalRoundTripTests
{
    internal const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Portal Map</name>
          <version>1.0.0</version>
          <objective>Score</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <portals sound="false" observers="never">
            <portal region="red-box" x="@-40.5" y="@12" z="@0.5" yaw="@-90" filter="only-red"/>
            <portal x="0" y="10" z="0" bidirectional="true">
              <region><cuboid min="-2,5,-2" max="2,8,2"/></region>
            </portal>
            <portal id="lift" destination="top" smooth="true" protect="true">
              <forward><team>blue</team></forward>
            </portal>
          </portals>
          <filters>
            <team id="only-red">red</team>
          </filters>
          <regions>
            <cuboid id="red-box" min="38,10,-2" max="42,14,2"/>
            <cuboid id="top" min="0,60,0" max="4,61,4"/>
          </regions>
        </map>
        """;

    private static string PortalsOf(MapXml map) => XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("portals")!.ToString();

    [Test]
    public async Task Every_portal_is_read_with_its_group_attributes()
    {
        var map = MapParser.ParseXmlString(Xml);

        await Assert.That(map.Portals.Count).IsEqualTo(3);
        var home = map.Portals[0];
        await Assert.That((home.RegionId, home.X, home.Y, home.Z, home.Yaw)).IsEqualTo(("red-box", "@-40.5", "@12", "@0.5", "@-90"));
        await Assert.That(home.FilterId).IsEqualTo("only-red");
        await Assert.That(map.Portals.All(portal => portal.Sound == false && portal.ObserversFilterId == "never")).IsTrue()
            .Because("a group's attributes reach every portal under it");
        await Assert.That(map.Regions.ContainsKey(map.Portals[1].RegionId)).IsTrue().Because("an inline region is registered");
        await Assert.That(map.Portals[2].ForwardFilterId).IsNotEmpty();
        await Assert.That(map.Portals[2].DestinationId).IsEqualTo("top");
    }

    [Test]
    public async Task A_map_writes_the_same_portals_after_the_xml_codec()
    {
        var first = MapParser.ParseXmlString(Xml);
        var again = MapParser.ParseXmlString(XmlWriter.ToXml(first));

        await Assert.That(PortalsOf(again)).IsEqualTo(PortalsOf(first));
    }

    [Test]
    public async Task A_map_writes_the_same_portals_after_the_json_codec()
    {
        var first = MapParser.ParseXmlString(Xml);
        var viaJson = Deserializer.FromDict(Serializer.ToDict(first));

        await Assert.That(PortalsOf(viaJson)).IsEqualTo(PortalsOf(first));
    }

    [Test]
    public async Task A_named_region_a_portal_uses_stays_in_the_regions_block()
    {
        var root = XDocument.Parse(XmlWriter.ToXml(MapParser.ParseXmlString(Xml))).Root!;
        var ids = root.Element("regions")!.Elements().Select(region => (string?)region.Attribute("id")).ToList();

        await Assert.That(ids).Contains("red-box");
        await Assert.That(ids).Contains("top");
    }
}
