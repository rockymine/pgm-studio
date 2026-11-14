using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>
/// A stored spawn keeps every region it states, each with its own yaw: the <c>&lt;spawns&gt;</c> a map writes
/// after a round trip through the database is the one it wrote before.
/// </summary>
[NotInParallel]
public sealed class SpawnStorageTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Spawn Map</name>
          <version>1.0.0</version>
          <objective>Win</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <spawns>
            <spawn team="blue">
              <regions>
                <region id="blue-near" yaw="90"/>
                <cuboid min="40,10,40" max="42,11,42" yaw="180"/>
                <region id="blue-far" yaw="-90"/>
              </regions>
            </spawn>
            <spawn team="red" region="red-spawn" yaw="30"/>
            <default region="observers"/>
          </spawns>
          <regions>
            <cuboid id="red-spawn" min="-20,10,-20" max="-18,11,-18"/>
            <cuboid id="blue-near" min="20,10,20" max="22,11,22"/>
            <cuboid id="blue-far" min="30,10,30" max="32,11,32"/>
            <cuboid id="observers" min="0,60,0" max="1,61,1"/>
          </regions>
        </map>
        """;

    private static string SpawnsOf(MapXml map) => XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("spawns")!.ToString();

    [Test]
    public async Task A_spawn_map_writes_the_same_spawns_after_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "spawn-map", Name = "Spawn Map", Version = "1.0.0", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        var parsed = MapParser.ParseXmlString(Xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.Spawns.Single(spawn => spawn.Team == "blue").Regions.Select(place => place.Yaw))
            .IsEquivalentTo(new[] { 90.0, 180.0, -90.0 });
        await Assert.That(SpawnsOf(read)).IsEqualTo(SpawnsOf(parsed));
    }
}
