using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0078</c> gives every stored build area, water lane, iron cube and approach wall the id <c>PlayIds</c>
/// mints for it, keeps an id already stated, numbers one that would repeat an id another piece states, and
/// leaves an intent whose entries all carry one byte for byte as it was. Rolls the schema back and forward, so
/// it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class AreaAndStructureIdsMigrationTests
{
    private const string Untouched = """{"spawns":[{"id":"spawn-red","team":"red"}],"build":{"areas":[{"minX":0,"minZ":0,"maxX":4,"maxZ":4,"id":"bridge"}]}}""";

    [Test]
    public async Task Every_area_and_structure_is_given_an_id()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 77);
        try
        {
            long board, untouched;
            await using (var db = TestDb.Connect())
            {
                board = Intent(db, "board", """
                    {"spawns":[{"id":"build-2","team":"red"}],
                     "build":{"areas":[{"minX":0,"minZ":0,"maxX":4,"maxZ":4},{"minX":8,"minZ":0,"maxX":12,"maxZ":4},
                                       {"minX":16,"minZ":0,"maxX":20,"maxZ":4,"id":"middle"}]},
                     "waterLanes":{"rects":[{"minX":0,"minZ":8,"maxX":4,"maxZ":12}]},
                     "structures":{"ironCubes":[{"minX":1,"minZ":1,"renew":true}],
                                   "walls":[{"minX":0,"minZ":0,"maxX":2,"maxZ":8,"topY":20},
                                            {"minX":30,"minZ":0,"maxX":32,"maxZ":8,"topY":20}]}}
                    """);
                untouched = Intent(db, "untouched", Untouched);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 78);
            await using (var db = TestDb.Connect())
            {
                var intent = Read(db, board);
                await Assert.That(Ids(intent, "build", "areas")).IsEquivalentTo(new[] { "build-1", "build-2-2", "middle" })
                    .Because("a stated id is kept, and one a spawn already states is numbered");
                await Assert.That(Ids(intent, "waterLanes", "rects")).IsEquivalentTo(new[] { "water-lane-1" });
                await Assert.That(Ids(intent, "structures", "ironCubes")).IsEquivalentTo(new[] { "iron-1" });
                await Assert.That(Ids(intent, "structures", "walls")).IsEquivalentTo(new[] { "wall-1", "wall-2" });

                await Assert.That(Encoding.UTF8.GetString(Data(db, untouched))).IsEqualTo(Untouched);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Intent(DataConnection db, string slug, string json)
    {
        db.Execute("INSERT INTO map (slug) VALUES (@slug)", new DataParameter("slug", slug));
        var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, 'map_intent_json', @data)",
            new DataParameter("map", map), new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static byte[] Data(DataConnection db, long artifact)
        => db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE id = {artifact}");

    private static JsonObject Read(DataConnection db, long artifact)
        => (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(Data(db, artifact)))!;

    private static List<string> Ids(JsonObject intent, string parent, string list)
        => [.. ((JsonArray)intent[parent]![list]!).Select(entry => entry!["id"]!.GetValue<string>())];
}
