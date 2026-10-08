using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0064</c> moves a stored wool's <c>room</c> into <c>protection</c>, wrapping a single rectangle into a
/// list, keeps a <c>protection</c> already stated, and leaves an intent with no <c>room</c> byte for byte as
/// it was. Rolls the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class WoolRoomIsProtectionMigrationTests
{
    private const string Untouched = """{"wools":[{"owner":"red-team","color":"blue","protection":[{"minX":1,"minZ":2,"maxX":3,"maxZ":4}]}]}""";

    [Test]
    public async Task A_wool_room_becomes_its_protection()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 63);
        try
        {
            long drawn, single, both, untouched;
            await using (var db = TestDb.Connect())
            {
                drawn = Intent(db, "drawn", """{"wools":[{"color":"blue","protection":[],"room":[{"minX":150,"minZ":220,"maxX":160,"maxZ":232}]}]}""");
                single = Intent(db, "single", """{"wools":[{"color":"red","room":{"minX":-6,"minZ":227,"maxX":4,"maxZ":239}}]}""");
                both = Intent(db, "both", """{"wools":[{"color":"lime","protection":[{"minX":0,"minZ":0,"maxX":5,"maxZ":5}],"room":[{"minX":9,"minZ":9,"maxX":12,"maxZ":12}]}]}""");
                untouched = Intent(db, "untouched", Untouched);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 64);
            await using (var db = TestDb.Connect())
            {
                var moved = Wool(db, drawn);
                await Assert.That(moved.ContainsKey("room")).IsFalse();
                await Assert.That(moved["protection"]!.ToJsonString())
                    .IsEqualTo("""[{"minX":150,"minZ":220,"maxX":160,"maxZ":232}]""")
                    .Because("an empty protection is no answer, so the room drawn under the old name is the region");

                await Assert.That(Wool(db, single)["protection"]!.ToJsonString())
                    .IsEqualTo("""[{"minX":-6,"minZ":227,"maxX":4,"maxZ":239}]""")
                    .Because("a single rectangle is a list of one");

                var kept = Wool(db, both);
                await Assert.That(kept.ContainsKey("room")).IsFalse();
                await Assert.That(kept["protection"]!.ToJsonString())
                    .IsEqualTo("""[{"minX":0,"minZ":0,"maxX":5,"maxZ":5}]""")
                    .Because("a protection already stated is the newer answer");

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

    private static JsonObject Wool(DataConnection db, long artifact)
        => (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(Data(db, artifact)))!["wools"]![0]!;
}
