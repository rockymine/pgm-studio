using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>A map's score boxes survive storage: the <c>&lt;score&gt;</c> it writes after a round trip through the
/// database is the one it wrote before.</summary>
[NotInParallel]
public sealed class ScoreBoxStorageTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Box Map</name>
          <version>1.0.0</version>
          <objective>Score</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <score>
            <limit>5</limit>
            <box region="red-box" points="1" filter="only-red" silent="true"/>
            <box value="3" filter="only-blue">
              <region><cuboid min="-42,10,-2" max="-38,14,2"/></region>
            </box>
          </score>
          <portals>
            <portal region="red-box" destination="red-spawn" yaw="@90" filter="only-red"/>
          </portals>
          <filters>
            <team id="only-red">red</team>
            <team id="only-blue">blue</team>
          </filters>
          <regions>
            <cuboid id="red-box" min="38,10,-2" max="42,14,2"/>
            <point id="red-spawn">-30.5,10,0.5</point>
          </regions>
        </map>
        """;

    private static string ScoreOf(MapXml map) => XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("score")!.ToString();

    [Test]
    public async Task A_box_map_writes_the_same_score_after_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "box-map", Name = "Box Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        var parsed = MapParser.ParseXmlString(Xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.Score!.Boxes.Count).IsEqualTo(2);
        await Assert.That(ScoreOf(read)).IsEqualTo(ScoreOf(parsed));
    }
}
