using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0050</c> carries every stored sketch layout's <c>water</c> props into kind <c>fluid</c>, leaving the
/// fluid they fill with and every other prop as they were. Rolls the schema back one migration and forward
/// again, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class FluidPropMigrationTests
{
    [Test]
    public async Task A_stored_water_prop_becomes_a_fluid_prop_and_rolls_back()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 49);
        try
        {
            long mapId;
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO map (slug, name, stage, created_at, updated_at, revision) VALUES ('moat', 'Moat', 'sketch', NOW(), NOW(), 1)");
                mapId = db.Execute<long>("SELECT id FROM map WHERE slug = 'moat'");
                const string layout = """
                    {"dressing":{"styles":{},"props":[
                      {"kind":"water","id":"canal","points":[[0,0],[10,0]]},
                      {"kind":"water","id":"forge","fluid":"lava","points":[[0,5],[10,5]]},
                      {"kind":"stroke","id":"road","points":[[0,9],[10,9]]}]}}
                    """;
                db.Execute("INSERT INTO map_artifact (map_id, kind, data, revision) VALUES (@map, 'sketch_layout_json', @data, 1)",
                    new DataParameter("map", mapId), new DataParameter("data", Encoding.UTF8.GetBytes(layout)));
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            var props = Props(mapId);
            await Assert.That(props.Select(Kind)).IsEquivalentTo(["fluid", "fluid", "stroke"]);
            await Assert.That(props[1]!["fluid"]!.GetValue<string>()).IsEqualTo("lava");
            await Assert.That(props.All(prop => prop!.AsObject().First().Key == "kind")).IsTrue();

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 49);
            await Assert.That(Props(mapId).Select(Kind)).IsEquivalentTo(["water", "water", "stroke"]);
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static string Kind(JsonNode? prop) => prop!["kind"]!.GetValue<string>();

    private static JsonArray Props(long mapId)
    {
        using var db = TestDb.Connect();
        var data = db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE map_id = {mapId} AND kind = 'sketch_layout_json'");
        return (JsonArray)JsonNode.Parse(Encoding.UTF8.GetString(data))!["dressing"]!["props"]!;
    }
}
