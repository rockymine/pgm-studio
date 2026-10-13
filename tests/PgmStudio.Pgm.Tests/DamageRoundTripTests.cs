using System.Xml.Linq;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The damage module survives both codecs: every filter of every <c>&lt;damage&gt;</c> element in order, inline or
/// named, and the two actions.
/// </summary>
public sealed class DamageRoundTripTests
{
    internal const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Damage Map</name>
          <version>1.0.0</version>
          <objective>Fight</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <damage victim-action="hurt">
            <deny><cause>fall</cause></deny>
            <deny><all><region id="lobby"/><cause>player</cause></all></deny>
          </damage>
          <damage>
            <filter id="no-team-hits"/>
          </damage>
          <filters>
            <deny id="no-team-hits"><relation>ally</relation></deny>
          </filters>
          <regions>
            <cuboid id="lobby" min="0,0,0" max="10,10,10"/>
          </regions>
        </map>
        """;

    private static string DamageOf(MapXml map) => XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("damage")!.ToString();

    [Test]
    public async Task Every_filter_of_every_damage_element_is_read_in_order()
    {
        var damage = MapParser.ParseXmlString(Xml).Damage!;

        await Assert.That(damage.FilterIds.Count).IsEqualTo(3);
        await Assert.That(damage.FilterIds[2]).IsEqualTo("no-team-hits");
        await Assert.That(damage.VictimAction).IsEqualTo("hurt");
    }

    [Test]
    public async Task A_map_writes_the_same_damage_after_both_codecs()
    {
        var first = MapParser.ParseXmlString(Xml);

        await Assert.That(DamageOf(MapParser.ParseXmlString(XmlWriter.ToXml(first)))).IsEqualTo(DamageOf(first));
        await Assert.That(DamageOf(Deserializer.FromDict(Serializer.ToDict(first)))).IsEqualTo(DamageOf(first));
    }

    [Test]
    public async Task A_map_with_no_damage_writes_none()
    {
        var map = MapParser.ParseXmlString("""
            <?xml version="1.0"?>
            <map proto="1.5.0"><name>Plain</name><version>1.0.0</version><objective>o</objective></map>
            """);

        await Assert.That(map.Damage).IsNull();
        await Assert.That(XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("damage")).IsNull();
    }
}
