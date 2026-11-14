using System.Xml.Linq;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A spawn keeps every region PGM reads for it — its <c>region</c> attribute, each <c>&lt;region&gt;</c> child and
/// each child of a <c>&lt;regions&gt;</c> block, an inline union standing for each member — each with the yaw
/// nearest to it, and writes them back so a second read finds the same regions facing the same way.
/// </summary>
public sealed class SpawnRoundTripTests
{
    internal const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Spawn Map</name>
          <version>1.0.0</version>
          <objective>Win</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <kits><kit id="spawn-kit"><item slot="0" material="STONE_SWORD"/></kit></kits>
          <spawns kit="spawn-kit">
            <spawn team="blue" yaw="45">
              <region yaw="90"><cuboid min="0,10,0" max="2,11,2"/></region>
              <region yaw="-90"><region id="blue-far"/></region>
              <regions yaw="180">
                <cuboid min="40,10,40" max="42,11,42"/>
                <region id="blue-back" yaw="270"/>
                <union>
                  <cuboid min="60,10,60" max="62,11,62"/>
                  <cuboid min="70,10,70" max="72,11,72"/>
                </union>
              </regions>
            </spawn>
            <spawn team="red" region="red-spawn" yaw="30"/>
            <default region="observers"/>
          </spawns>
          <regions>
            <cuboid id="red-spawn" min="-20,10,-20" max="-18,11,-18"/>
            <cuboid id="blue-far" min="20,10,20" max="22,11,22"/>
            <cuboid id="blue-back" min="30,10,30" max="32,11,32"/>
            <cuboid id="observers" min="0,60,0" max="1,61,1"/>
          </regions>
        </map>
        """;

    /// <summary>A region by what it is: its id where it is named, its shape where it is inline.</summary>
    private static string Signature(Region region) => region.Id.StartsWith("__")
        ? $"{region.Type}:{region.MinX},{region.MinY},{region.MinZ}..{region.MaxX},{region.MaxY},{region.MaxZ}"
        : region.Id;

    /// <summary>A spawn's regions in order, each as <c>region@yaw</c>.</summary>
    private static string Regions(Spawn spawn) =>
        string.Join(" ; ", spawn.Regions.Select(place => $"{Signature(place.Region)}@{place.Yaw}"));

    private static Spawn Blue(MapXml map) => map.Spawns.Single(spawn => spawn.Team == "blue");

    [Test]
    public async Task Every_region_a_spawn_states_is_read_with_its_own_yaw()
    {
        var blue = Blue(MapParser.ParseXmlString(Xml));

        await Assert.That(Regions(blue)).IsEqualTo(string.Join(" ; ",
            "cuboid:0,10,0..2,11,2@90",
            "blue-far@-90",
            "cuboid:40,10,40..42,11,42@180",
            "blue-back@270",
            "cuboid:60,10,60..62,11,62@180",
            "cuboid:70,10,70..72,11,72@180"));
        await Assert.That(blue.Kit).IsEqualTo("spawn-kit").Because("a <spawns> block hands its kit to every spawn under it");
    }

    [Test]
    public async Task A_spawn_of_one_region_keeps_the_spawns_yaw()
    {
        var map = MapParser.ParseXmlString(Xml);

        await Assert.That(Regions(map.Spawns.Single(spawn => spawn.Team == "red"))).IsEqualTo("red-spawn@30");
        await Assert.That(Regions(map.ObserverSpawn!)).IsEqualTo("observers@0");
    }

    [Test]
    public async Task Every_spawn_region_survives_the_xml_codec()
    {
        var first = MapParser.ParseXmlString(Xml);
        var again = MapParser.ParseXmlString(XmlWriter.ToXml(first));

        foreach (var team in new[] { "blue", "red" })
            await Assert.That(Regions(again.Spawns.Single(spawn => spawn.Team == team)))
                .IsEqualTo(Regions(first.Spawns.Single(spawn => spawn.Team == team)));
        await Assert.That(Regions(again.ObserverSpawn!)).IsEqualTo(Regions(first.ObserverSpawn!));
    }

    [Test]
    public async Task Every_spawn_region_survives_the_json_codec()
    {
        var first = MapParser.ParseXmlString(Xml);
        var again = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(Serializer.ToDict(first))));

        await Assert.That(Regions(Blue(again))).IsEqualTo(Regions(Blue(first)));
    }

    /// <summary>The written spawn, read the way PGM reads it: six regions, none of them a union left to be
    /// split.</summary>
    [Test]
    public async Task The_written_spawn_names_every_region()
    {
        var written = XDocument.Parse(XmlWriter.ToXml(MapParser.ParseXmlString(Xml))).Root!
            .Element("spawns")!.Elements("spawn").Single(spawn => (string?)spawn.Attribute("team") == "blue");

        var providers = written.Elements("region").Count() + written.Elements("regions").SelectMany(block => block.Elements()).Count();
        await Assert.That(providers).IsEqualTo(6);
        await Assert.That(written.Descendants("union").Any()).IsFalse();
    }
}
