using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0077</c> takes every room shape out of a stored sketch layout and keeps what it said about the ground
/// under it in the layout's <c>rooms</c> record, keyed by the id of the piece the intent states for it — a
/// region by its <c>intentRef</c>, a building by its region. A shape no piece answers keeps its record under
/// its own id, the intent is left as it was, and a layout carrying no room shape is left byte for byte. Rolls
/// the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class RoomsStoredOnceMigrationTests
{
    private const string Untouched = """{"layers":[{"id":"ground","layout":{"shapes":[],"groups":[]}}]}""";

    private const string Intent = """
        {"teams":[{"id":"red"}],
         "spawns":[{"id":"home","team":"red","protection":[{"minX":0,"minZ":0,"maxX":10,"maxZ":10}]}],
         "wools":[{"id":"wool-red-lime","owner":"red","color":"lime","protection":[]}]}
        """;

    private const string Layout = """
        {"layers":[{"id":"ground","layout":{"shapes":[
           {"id":"land","type":"rectangle","operation":"add","min_x":0,"min_z":0,"max_x":60,"max_z":60},
           {"id":"spawn-red","type":"rectangle","operation":"add","role":"spawn","intentRef":"red","color":"red",
            "min_x":0,"min_z":0,"max_x":10,"max_z":10,"floor":2,"base_height":4,"relief_scope":"hold",
            "height_authored":true,"doors":["-z"]},
           {"id":"spawn-red-building","type":"rectangle","operation":"add","role":"building","color":"red",
            "min_x":2,"min_z":2,"max_x":8,"max_z":8,"theme":"stone"},
           {"id":"wool-red-lime","type":"rectangle","operation":"add","role":"woolRoom","intentRef":"red:lime",
            "min_x":30,"min_z":0,"max_x":40,"max_z":10},
           {"id":"wool-blue-blue","type":"rectangle","operation":"add","role":"woolRoom","intentRef":"blue:blue",
            "min_x":90,"min_z":0,"max_x":100,"max_z":10,"relief_scope":"follow","base_height":9}],
         "groups":[]}}]}
        """;

    [Test]
    public async Task Every_room_shape_leaves_the_layout_and_its_ground_is_kept_under_its_pieces_id()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 76);
        try
        {
            long layout, intent, untouched;
            await using (var db = TestDb.Connect())
            {
                var map = Map(db, "board");
                layout = Artifact(db, map, "sketch_layout_json", Layout);
                intent = Artifact(db, map, "map_intent_json", Intent);
                untouched = Artifact(db, Map(db, "untouched"), "sketch_layout_json", Untouched);
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 77);
            await using (var db = TestDb.Connect())
            {
                var stored = JsonNode.Parse(Text(db, layout))!;
                var shapes = stored["layers"]![0]!["layout"]!["shapes"]!.AsArray();
                await Assert.That(shapes.Select(shape => shape!["id"]!.GetValue<string>())).IsEquivalentTo(new[] { "land" });

                var rooms = stored["rooms"]!.AsObject();
                await Assert.That(rooms.Select(room => room.Key))
                    .IsEquivalentTo(new[] { "home", "home-building", "wool-blue-blue" })
                    .Because("a region is keyed by its piece, a building by its region's, an unmatched shape by itself");
                await Assert.That(rooms["home"]!["base_height"]!.GetValue<double>()).IsEqualTo(4);
                await Assert.That(rooms["home"]!["height_authored"]!.GetValue<bool>()).IsTrue();
                await Assert.That(rooms["home"]!["doors"]![0]!.GetValue<string>()).IsEqualTo("-z");
                await Assert.That(rooms["home-building"]!["theme"]!.GetValue<string>()).IsEqualTo("stone");
                await Assert.That(rooms["wool-blue-blue"]!["relief_scope"]!.GetValue<string>()).IsEqualTo("follow");

                await Assert.That(Text(db, intent)).IsEqualTo(Intent).Because("the intent holds the rooms already");
                await Assert.That(Text(db, untouched)).IsEqualTo(Untouched);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Map(DataConnection db, string slug)
    {
        db.Execute("INSERT INTO map (slug) VALUES (@slug)", new DataParameter("slug", slug));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static long Artifact(DataConnection db, long map, string kind, string json)
    {
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, @kind, @data)",
            new DataParameter("map", map), new DataParameter("kind", kind),
            new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static string Text(DataConnection db, long artifact) =>
        Encoding.UTF8.GetString(db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE id = {artifact}"));
}
