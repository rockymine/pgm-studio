using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>A map's damage module survives storage: the <c>&lt;damage&gt;</c> it writes after a round trip through
/// the database is the one it wrote before.</summary>
[NotInParallel]
public sealed class DamageStorageTests
{
    private const string Xml = """
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
    public async Task A_damage_map_writes_the_same_damage_after_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "damage-map", Name = "Damage Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        var parsed = MapParser.ParseXmlString(Xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.Damage!.FilterIds.Count).IsEqualTo(3);
        await Assert.That(DamageOf(read)).IsEqualTo(DamageOf(parsed));
    }
}
