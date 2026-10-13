using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>
/// A map's flags, the posts and nets declared beside them, and the flag filters they name survive storage:
/// the <c>&lt;flags&gt;</c> the map writes after a round trip through the database is the one it wrote before.
/// </summary>
[NotInParallel]
public sealed class FlagStorageTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>CTF Map</name>
          <version>1.0.0</version>
          <objective>Take their flag home</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <kits>
            <kit id="carrier"><max-health>12</max-health></kit>
          </kits>
          <flags>
            <post id="red-home" owner="red">-40.5,10,0.5</post>
            <flag id="red-flag" owner="red" color="red" post="red-home" pickup-filter="only-blue" pickup-kit="carrier">
              <net points="1" respawn-together="true">
                <region><cuboid min="40,10,-2" max="43,13,3"/></region>
              </net>
            </flag>
            <flag id="blue-flag" color="blue" points-rate="1">
              <post recover-time="0s" respawn-time="15s">
                <post name="Mid">0.5,9,0.5</post>
                <post name="East" yaw="-90"><block>40,10,0</block></post>
              </post>
            </flag>
            <net region="red-net" flags="red-flag blue-flag" post="red-home">
              <capture-filter><not><flag-carried post="red-home">red-flag</flag-carried></not></capture-filter>
            </net>
          </flags>
          <filters>
            <team id="only-blue">blue</team>
          </filters>
          <regions>
            <cuboid id="red-net" min="-43,10,-2" max="-40,13,3"/>
          </regions>
        </map>
        """;

    private static string FlagsOf(MapXml map) => XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("flags")!.ToString();

    private static async Task<long> InsertMapAsync(PgmDb db) =>
        await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "ctf-map", Name = "CTF Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

    [Test]
    public async Task A_flag_map_writes_the_same_flags_after_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);
        var parsed = MapParser.ParseXmlString(Xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.Flags.Count).IsEqualTo(2);
        await Assert.That(FlagsOf(read)).IsEqualTo(FlagsOf(parsed));
        await Assert.That(read.Kits.Single(kit => kit.Id == "carrier").MaxHealth).IsEqualTo(12.0)
            .Because("a carrier kit that lowers health is the whole of what the kit states");
    }

    [Test]
    public async Task A_stored_flag_map_reads_as_capture_the_flag()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);
        await new MapWriter(db).WriteEntitiesAsync(mapId, MapParser.ParseXmlString(Xml));

        var gamemodes = await new MapRepository(db).GamemodesAsync();

        await Assert.That(gamemodes[mapId]).IsEquivalentTo(new[] { Gamemodes.Ctf });
    }
}
