using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>A map's portals survive storage: the <c>&lt;portals&gt;</c> it writes after a round trip through the
/// database is the one it wrote before.</summary>
[NotInParallel]
public sealed class PortalStorageTests
{
    private const string Xml = """
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
    public async Task A_portal_map_writes_the_same_portals_after_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "portal-map", Name = "Portal Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        var parsed = MapParser.ParseXmlString(Xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.Portals.Count).IsEqualTo(3);
        await Assert.That(PortalsOf(read)).IsEqualTo(PortalsOf(parsed));
    }
}
