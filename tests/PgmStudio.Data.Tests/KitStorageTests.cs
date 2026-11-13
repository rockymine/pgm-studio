using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm;

namespace PgmStudio.Data.Tests;

/// <summary>
/// A stored kit keeps what it states that the studio does not read: its other attributes and elements go into
/// the kit row as written and come back out, so a map written after storage plays as the one read.
/// </summary>
[NotInParallel]
public sealed class KitStorageTests
{
    private const string Runner = """
        <kit id="runner" parents="carrier" potion-particles="false"><game-mode>adventure</game-mode><double-jump power="3"/></kit>
        """;

    [Test]
    public async Task A_kits_carried_attributes_and_elements_survive_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "kit-map", Name = "Kit Map", Version = "1.0.0", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        var xml = FlagStorageTests.Xml.Replace("""<kit id="carrier"><max-health>12</max-health></kit>""",
            """<kit id="carrier"><max-health>12</max-health></kit>""" + Runner);
        var parsed = MapParser.ParseXmlString(xml);

        await new MapWriter(db).WriteEntitiesAsync(mapId, parsed);
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        var written = XDocument.Parse(XmlWriter.ToXml(read)).Root!.Element("kits")!.Elements("kit")
            .Single(kit => (string?)kit.Attribute("id") == "runner");
        await Assert.That((string?)written.Attribute("parents")).IsEqualTo("carrier");
        await Assert.That((string?)written.Attribute("potion-particles")).IsEqualTo("false");
        await Assert.That(written.Element("game-mode")?.Value).IsEqualTo("adventure");
        await Assert.That((string?)written.Element("double-jump")?.Attribute("power")).IsEqualTo("3");
        await Assert.That(read.Kits.Single(kit => kit.Id == "carrier").CarriedElements).IsEmpty()
            .Because("a kit stating only what the studio reads carries nothing");
    }
}
