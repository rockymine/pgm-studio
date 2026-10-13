using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>A map's match rules survive storage: the time limit, respawn rules, blitz, rage and kill rewards it
/// writes after a round trip through the database are the ones it wrote before.</summary>
[NotInParallel]
public sealed class MatchRulesStorageTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Rules Map</name>
          <version>1.0.0</version>
          <objective>Fight</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <time result="score">10m</time>
          <respawn delay="3s" auto="true"/>
          <respawns>
            <respawn delay="5s" filter="only-red"/>
          </respawns>
          <blitz><lives>2</lives></blitz>
          <rage/>
          <kill-rewards>
            <kill-reward><item material="arrow" amount="8"/></kill-reward>
          </kill-rewards>
          <filters>
            <team id="only-red">red</team>
          </filters>
        </map>
        """;

    private static readonly string[] Tags = ["time", "respawn", "respawns", "blitz", "rage", "kill-rewards"];

    private static string RulesOf(MapXml map)
    {
        var root = XDocument.Parse(XmlWriter.ToXml(map)).Root!;
        return string.Join("\n", Tags.SelectMany(tag => root.Elements(tag)).Select(e => e.ToString()));
    }

    [Test]
    public async Task A_map_writes_the_same_rules_after_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "rules-map", Name = "Rules Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        var parsed = MapParser.ParseXmlString(Xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.Blitz!.Lives).IsEqualTo(2);
        await Assert.That(RulesOf(read)).IsEqualTo(RulesOf(parsed));
    }
}
