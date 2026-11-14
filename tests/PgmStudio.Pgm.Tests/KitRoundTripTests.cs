using System.Xml.Linq;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A kit loses nothing it states. What the studio reads into its own fields is read; everything else a kit
/// carries — its other attributes and elements — is kept as written and written back, through the XML codec and
/// the document tree alike, so an exported map plays as the one read.
/// </summary>
public sealed class KitRoundTripTests
{
    private static string Map(string kits) => FlagRoundTripTests.KingOfTheFlag.Replace(
        """<kit id="reset-health"><max-health>20</max-health></kit>""",
        """<kit id="reset-health"><max-health>20</max-health></kit>""" + kits);

    private const string Kits = """
        <kit id="runner" parents="flag-kit" potion-particles="false">
          <item slot="0" material="stone sword"/>
          <game-mode>adventure</game-mode>
          <double-jump power="3" recharge-time="2s"/>
          <walk-speed>1.2</walk-speed>
        </kit>
        <kit id="spectator"><game-mode>spectator</game-mode></kit>
        """;

    private static XElement KitOf(string xml, string id) =>
        XDocument.Parse(xml).Root!.Element("kits")!.Elements("kit").Single(kit => (string?)kit.Attribute("id") == id);

    [Test]
    public async Task What_a_kit_states_and_the_studio_does_not_read_is_written_back()
    {
        var parsed = MapParser.ParseXmlString(Map(Kits));
        var runner = KitOf(XmlWriter.ToXml(parsed), "runner");

        await Assert.That((string?)runner.Attribute("parents")).IsEqualTo("flag-kit");
        await Assert.That((string?)runner.Attribute("potion-particles")).IsEqualTo("false");
        await Assert.That(runner.Element("game-mode")?.Value).IsEqualTo("adventure");
        await Assert.That((string?)runner.Element("double-jump")?.Attribute("recharge-time")).IsEqualTo("2s");
        await Assert.That(runner.Element("walk-speed")?.Value).IsEqualTo("1.2");
        await Assert.That(runner.Elements("item").Count()).IsEqualTo(1).Because("the item is read, and written once");
    }

    [Test]
    public async Task A_kit_stating_only_what_the_studio_does_not_read_is_kept()
    {
        var parsed = MapParser.ParseXmlString(Map(Kits));
        await Assert.That(parsed.Kits.Select(kit => kit.Id)).Contains("spectator");
        await Assert.That(KitOf(XmlWriter.ToXml(parsed), "spectator").Element("game-mode")?.Value).IsEqualTo("spectator");
    }

    [Test]
    public async Task The_document_tree_carries_the_same_kit()
    {
        var parsed = MapParser.ParseXmlString(Map(Kits));
        var again = Deserializer.FromDict(Serializer.ToDict(parsed));
        await Assert.That(XmlWriter.ToXml(again)).IsEqualTo(XmlWriter.ToXml(parsed));
    }
}
