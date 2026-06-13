using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using MySqlConnector;
using PgmStudio.Data;
using PgmStudio.Import;
using PgmStudio.Migrations;
using PgmStudio.Pgm;

namespace PgmStudio.Import.Tests;

/// <summary>
/// M3 integration test: import a synthetic map's xml_data.json into MariaDB and verify the
/// entity rows (counts, wool grouping, JSON column content). The parquet feature path is
/// covered end-to-end by the corpus importer run (src/PgmStudio.Import over the output dir).
/// </summary>
[NotInParallel]
public sealed class ImporterTests
{
    private const string ConnectionString =
        "Server=localhost;Database=pgm_studio_test;User ID=pgm;Password=pgm_dev_pw;";

    private const string SampleXml = """
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>Synthetic</name><version>1.0.0</version><objective>ctw</objective>
          <teams>
            <team id="red-team" color="red" max="8">Red</team>
            <team id="blue-team" color="blue" max="8">Blue</team>
          </teams>
          <wools team="red-team"><wool color="red" location="10,20,30"><monument><block>1,2,3</block></monument></wool></wools>
          <wools team="blue-team"><wool color="red" location="10,20,30"><monument><block>4,5,6</block></monument></wool></wools>
          <filters><team id="only-red">red-team</team></filters>
          <regions>
            <cuboid id="red-spawn" min="0,0,0" max="5,5,5"/>
            <union id="bases"><region id="red-spawn"/><cuboid id="blue-spawn" min="10,0,10" max="15,5,15"/></union>
          </regions>
        </map>
        """;

    [Test]
    public async Task Imports_synthetic_map_into_entity_rows()
    {
        await ResetSchemaAsync();
        var m = MapParser.ParseXmlString(SampleXml);

        var dir = Path.Combine(Path.GetTempPath(), "pgm-import-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "xml_data.json"),
                JsonSerializer.Serialize(Serializer.ToDict(m)));

            await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ConnectionString));
            await new MapImporter(db).ImportDirAsync("synthetic", dir);

            var map = await db.Maps.FirstOrDefaultAsync(x => x.Slug == "synthetic");
            await Assert.That(map).IsNotNull();
            await Assert.That(map!.Name).IsEqualTo("Synthetic");

            await Assert.That(await db.Regions.CountAsync(r => r.MapId == map.Id)).IsEqualTo(m.Regions.Count);
            await Assert.That(await db.Filters.CountAsync(f => f.MapId == map.Id)).IsEqualTo(m.Filters.Count);
            await Assert.That(await db.Teams.CountAsync(t => t.MapId == map.Id)).IsEqualTo(2);

            // wools group by colour → one "red" wool row with two monuments (red-team + blue-team)
            var wools = await db.Wools.Where(w => w.MapId == map.Id).ToListAsync();
            await Assert.That(wools.Count).IsEqualTo(1);
            await Assert.That(wools[0].WoolKey).IsEqualTo("red");
            await Assert.That(await db.Monuments.CountAsync(mo => mo.WoolId == wools[0].Id)).IsEqualTo(2);

            // a cuboid region's coords_json round-trips its min/max
            var cuboid = await db.Regions.FirstOrDefaultAsync(r => r.MapId == map.Id && r.RegionKey == "red-spawn");
            await Assert.That(cuboid).IsNotNull();
            await Assert.That(cuboid!.Type).IsEqualTo("cuboid");
            using var coords = JsonDocument.Parse(cuboid.CoordsJson!);
            await Assert.That(coords.RootElement.GetProperty("max").GetProperty("x").GetDouble()).IsEqualTo(5d);

            // the union's children are stored as id refs
            var union = await db.Regions.FirstOrDefaultAsync(r => r.MapId == map.Id && r.RegionKey == "bases");
            using var children = JsonDocument.Parse(union!.ChildRefIdsJson!);
            await Assert.That(children.RootElement.GetArrayLength()).IsEqualTo(2);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static async Task ResetSchemaAsync()
    {
        await using (var conn = new MySqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var tables = new List<string>();
            await using (var cmd = new MySqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()", conn))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync()) tables.Add(r.GetString(0));
            if (tables.Count > 0)
            {
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=0");
                foreach (var t in tables) await Exec(conn, $"DROP TABLE IF EXISTS `{t}`");
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=1");
            }
        }
        SchemaMigrator.MigrateUp(ConnectionString);
    }

    private static async Task Exec(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
