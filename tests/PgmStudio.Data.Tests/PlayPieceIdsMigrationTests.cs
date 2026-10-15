using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0076</c> gives every stored spawn, wool, destroyable and core the id <c>PlayIds</c> mints for it, keeps
/// an id already stated, numbers two that would collide, and leaves an intent whose pieces all carry one byte
/// for byte as it was. Rolls the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class PlayPieceIdsMigrationTests
{
    private const string Untouched = """{"spawns":[{"id":"home","team":"red-team"}]}""";

    [Test]
    public async Task Every_play_piece_is_given_an_id()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 75);
        try
        {
            long board, untouched;
            await using (var db = TestDb.Connect())
            {
                board = Intent(db, "board", """
                    {"spawns":[{"team":"red-team"},{"team":"blue-team","id":"blue-home"}],
                     "wools":[{"owner":"red-team","color":"lime"},{"owner":"blue-team"}],
                     "destroyables":[{"owner":"red-team"},{"owner":"red-team"}],
                     "cores":[{"owner":"blue-team"}]}
                    """);
                untouched = Intent(db, "untouched", Untouched);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 76);
            await using (var db = TestDb.Connect())
            {
                var intent = Read(db, board);
                await Assert.That(Ids(intent, "spawns")).IsEquivalentTo(new[] { "spawn-red-team", "blue-home" })
                    .Because("a stated id is kept");
                await Assert.That(Ids(intent, "wools")).IsEquivalentTo(new[] { "wool-red-team-lime", "wool-blue-team" })
                    .Because("a wool with no colour is named by its owner alone");
                await Assert.That(Ids(intent, "destroyables"))
                    .IsEquivalentTo(new[] { "destroyable-red-team", "destroyable-red-team-2" })
                    .Because("two pieces that would share an id are numbered");
                await Assert.That(Ids(intent, "cores")).IsEquivalentTo(new[] { "core-blue-team" });

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

    private static List<string> Ids(JsonObject intent, string list)
        => [.. ((JsonArray)intent[list]!).Select(piece => piece!["id"]!.GetValue<string>())];
}
