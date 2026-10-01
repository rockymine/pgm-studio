using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0055</c> gives the library's house styles the names the author's review gave them, and takes the rejected
/// ones out. A renamed style takes its parts with it and leaves its footing behind; a rejected one leaves with the
/// parts nothing else binds; and a map's current refinement names a renamed style by its new name where a room
/// style stands, and nowhere else. Rolls the schema back one migration and forward again, so it runs apart from
/// every other database test.
/// </summary>
[NotInParallel]
public sealed class HouseStyleNamesMigrationTests
{
    private const string Refinement = """
        {"roomStyles": {"spawn": {"library": "showcase-hall"}},
         "materials": {"plaster": {"library": "showcase-hall · wall 1"}},
         "themes": {"lawn": {"library": "cottage"}},
         "dressing": {"styles": {"hall": {"kind": "house", "library": "stilts"},
                                 "oak": {"kind": "tree", "library": "stilts"}},
                      "props": [{"kind": "house", "style": {"library": "hoar-store"}}]}}
        """;

    [Test]
    public async Task A_kept_style_is_renamed_with_its_parts_a_rejected_one_leaves_and_a_refinement_follows()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 54);
        try
        {
            await using (var db = TestDb.Connect())
            {
                var hall = Room(db, "showcase-hall");
                Bind(db, hall, "wall", Material(db, "showcase-hall · wall 1"));
                var roof = Id(db, "INSERT INTO roof_style (name, created_at) VALUES ('showcase-hall · roof', NOW())");
                db.Execute($"UPDATE room_style SET roof_style_id = {roof} WHERE id = {hall}");
                var storey = Id(db, "INSERT INTO storey_style (name, created_at) VALUES ('showcase-hall · storey 1', NOW())");
                db.Execute($"INSERT INTO room_style_storey (room_style_id, ordinal, storey_style_id) VALUES ({hall}, 0, {storey})");

                var cottage = Room(db, "cottage");
                Bind(db, cottage, "wall", Material(db, "cottage · wall 1"));
                Bind(db, cottage, "sill", Material(db, "cottage · sill"));

                var store = Room(db, "hoar-store");
                Bind(db, store, "wall", Material(db, "hoar-store · wall 1"));
                var shared = Material(db, "hoar-store · roof");
                Bind(db, store, "roof", shared);
                var theme = Id(db, "INSERT INTO theme (name, created_at) VALUES ('frost', NOW())");
                db.Execute($"INSERT INTO theme_bucket (theme_id, bucket, style_id) VALUES ({theme}, 'surface', {shared})");

                var map = Id(db, "INSERT INTO map (slug) VALUES ('weir')");
                db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, 'refinement_json', @data)",
                    new DataParameter("map", map), new DataParameter("data", Encoding.UTF8.GetBytes(Refinement)));
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(Names(db, "room_style")).IsEquivalentTo(
                    ["brick-roofed-stone-and-dark-oak-house", "spruce-roofed-stone-cottage"]);
                await Assert.That(Names(db, "style")).IsEquivalentTo(
                    ["brick-roofed-stone-and-dark-oak-house · wall 1", "spruce-roofed-stone-cottage · wall 1", "hoar-store · roof"])
                    .Because("the footing and the rejected style's own wall leave; a material a theme binds stays");
                await Assert.That(Names(db, "roof_style")).IsEquivalentTo(["brick-roofed-stone-and-dark-oak-house · roof"]);
                await Assert.That(Names(db, "storey_style")).IsEquivalentTo(["brick-roofed-stone-and-dark-oak-house · storey 1"]);
                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM room_style_course WHERE part = 'sill'")).IsEqualTo(0L);

                var data = db.Execute<byte[]>("SELECT data FROM map_artifact WHERE kind = 'refinement_json'");
                var refinement = JsonNode.Parse(Encoding.UTF8.GetString(data))!;
                await Assert.That(Library(refinement["roomStyles"]!["spawn"])).IsEqualTo("brick-roofed-stone-and-dark-oak-house");
                await Assert.That(Library(refinement["materials"]!["plaster"])).IsEqualTo("brick-roofed-stone-and-dark-oak-house · wall 1");
                await Assert.That(Library(refinement["dressing"]!["styles"]!["hall"])).IsEqualTo("oak-stilt-house");
                await Assert.That(Library(refinement["dressing"]!["styles"]!["oak"])).IsEqualTo("stilts")
                    .Because("a tree that happens to share a house's name is a tree");
                await Assert.That(Library(refinement["themes"]!["lawn"])).IsEqualTo("cottage")
                    .Because("a theme that happens to share a house's name is a theme");
                await Assert.That(Library(refinement["dressing"]!["props"]![0]!["style"])).IsEqualTo("hoar-store")
                    .Because("a rejected style's name is left for the next apply to report");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Room(DataConnection db, string name) =>
        Id(db, $"INSERT INTO room_style (name, created_at) VALUES ('{name}', NOW())");

    private static long Material(DataConnection db, string name) =>
        Id(db, $"INSERT INTO style (name, kind, params_json, created_at) VALUES ('{name}', 'solid', '{{}}', NOW())");

    private static void Bind(DataConnection db, long room, string part, long material) =>
        db.Execute($"INSERT INTO room_style_course (room_style_id, part, ordinal, style_id) VALUES ({room}, '{part}', 0, {material})");

    private static long Id(DataConnection db, string insert)
    {
        db.Execute(insert);
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static List<string> Names(DataConnection db, string table) =>
        [.. db.Query<string>($"SELECT name FROM {table}")];

    private static string? Library(JsonNode? named) => named?["library"]?.GetValue<string>();
}
