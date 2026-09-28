using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0045</c> carries the rows already stored into the shape where a copied tree names its builder: the
/// showcase's library rows are credited to rockymine, and a stored map's copied trees are given the builder of
/// the library row whose body they carry — the same blocks, or the same blocks less the planks and moved to
/// their own corner. Rolls the schema back one migration and forward again, so it runs apart from every other
/// database test.
/// </summary>
[NotInParallel]
public sealed class TreeBuilderMigrationTests
{
    // A trunk standing on a plank, and the same tree carrying no plank: its logs and leaves one course lower.
    private const string WithPlank = "[[0,0,0,5,1],[0,1,0,17,0],[0,2,0,17,0],[0,3,0,18,0]]";
    private const string NoPlank = "[[0,0,0,17,0],[0,1,0,17,0],[0,2,0,18,0]]";
    private const string Elsewhere = "[[0,0,0,17,2],[0,1,0,18,2]]";

    [Test]
    public async Task A_showcase_tree_and_every_stored_map_carrying_it_are_credited_to_its_builder()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 44);
        try
        {
            long mapId;
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO tree_style (name, form, species, height, body, cut_world, cut_x, cut_y, cut_z, cut_at, created_at) VALUES "
                           + $"('showcase-r1-1', 'copied', 'oak', 4, '{WithPlank}', '/srv/mapgen/corpus/tree-showcase', 1, 2, 3, NOW(), NOW()), "
                           + $"('grove-r1-1', 'copied', 'oak', 2, '{Elsewhere}', '/srv/grove', 1, 2, 3, NOW(), NOW())");
                db.Execute("INSERT INTO map (slug, name, stage, created_at, updated_at, revision) VALUES ('orchard', 'Orchard', 'sketch', NOW(), NOW(), 1)");
                mapId = db.Execute<long>("SELECT id FROM map WHERE slug = 'orchard'");
                var layout = $$$"""
                    {"dressing":{"styles":{
                      "now":{"kind":"tree","form":"copied","body":{{{WithPlank}}}},
                      "plankless":{"kind":"tree","form":"copied","body":{{{NoPlank}}}},
                      "other":{"kind":"tree","form":"copied","body":{{{Elsewhere}}}},
                      "oak":{"kind":"tree","form":"template","species":"oak"}},
                     "props":[]}}
                    """;
                db.Execute("INSERT INTO map_artifact (map_id, kind, data, revision) VALUES (@map, 'sketch_layout_json', @data, 1)",
                    new DataParameter("map", mapId), new DataParameter("data", Encoding.UTF8.GetBytes(layout)));
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<string?>("SELECT cut_builder FROM tree_style WHERE name = 'showcase-r1-1'")).IsEqualTo("rockymine");
                await Assert.That(db.Execute<string?>("SELECT cut_builder FROM tree_style WHERE name = 'grove-r1-1'")).IsNull();
            }
            var styles = Styles(mapId);
            await Assert.That(styles["now"]?["builder"]?.GetValue<string>()).IsEqualTo("rockymine");
            await Assert.That(styles["plankless"]?["builder"]?.GetValue<string>()).IsEqualTo("rockymine");
            await Assert.That(styles["other"]?["builder"]).IsNull();
            await Assert.That(styles["oak"]?["builder"]).IsNull();

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 44);
            await Assert.That(Styles(mapId).Select(pair => pair.Value?["builder"]).All(builder => builder is null)).IsTrue();
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static JsonObject Styles(long mapId)
    {
        using var db = TestDb.Connect();
        var data = db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE map_id = {mapId} AND kind = 'sketch_layout_json'");
        return (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(data))!["dressing"]!["styles"]!;
    }
}
