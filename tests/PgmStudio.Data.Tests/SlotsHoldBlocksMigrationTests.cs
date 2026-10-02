using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0058</c> moves every single block out of the pattern library and into the slots that lay it, merges the
/// patterns, roofs, storeys and porches that hold the same thing into one row each, names the seeded patterns as
/// the seed folder does, and makes each map's current refinement follow. Rolled back, a block takes a row again.
/// Rolls the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class SlotsHoldBlocksMigrationTests
{
    private const string Noise = """{"kind":"noise","seed":6311,"scale":2,"octaves":1,"stops":[{"kind":"solid","id":4,"data":0},{"kind":"solid","id":1,"data":5}],"rise":0}""";
    private const string NoiseReordered = """{"stops":[{"data":0,"id":4,"kind":"solid"},{"kind":"solid","id":1,"data":5}],"kind":"noise","seed":6311,"scale":2,"octaves":1,"rise":0}""";
    private const string Reseeded = """{"kind":"noise","seed":4101,"scale":2,"octaves":1,"stops":[{"kind":"solid","id":4,"data":0},{"kind":"solid","id":1,"data":5}],"rise":0}""";

    [Test]
    public async Task Blocks_move_into_their_slots_and_one_pattern_or_part_is_one_row()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 57);
        try
        {
            long quartz, laid, noise, copy, barn, theme, house, roofA, roofB, map;
            await using (var db = TestDb.Connect())
            {
                quartz = Style(db, "quartz rim", "solid", """{"kind":"solid","id":155,"data":0}""");
                laid = Style(db, "spruce beam", "laidLog", """{"kind":"laidLog","id":17,"data":1}""");
                noise = Style(db, "hay-gambrel-barn · wall 1", "noise", Noise);
                copy = Style(db, "my noise", "noise", NoiseReordered);
                barn = Style(db, "jungle-saltbox-cottage · wall 1", "noise", Reseeded);

                theme = Id(db, "INSERT INTO theme (name, created_at) VALUES ('frost', NOW())");
                db.Execute($"INSERT INTO theme_bucket (theme_id, bucket, style_id, depth, enabled) VALUES ({theme}, 'rim', {quartz}, 1, 1), ({theme}, 'wall', {copy}, 0, 1)");

                house = Id(db, "INSERT INTO room_style (name, created_at) VALUES ('barn', NOW())");
                db.Execute($"INSERT INTO room_style_course (room_style_id, part, ordinal, style_id, height) VALUES ({house}, 'wall', 0, {barn}, 2), ({house}, 'post', 0, {laid}, 1)");

                roofA = Id(db, "INSERT INTO roof_style (name, pitch, created_at) VALUES ('barn · roof', 2, NOW())");
                roofB = Id(db, "INSERT INTO roof_style (name, pitch, created_at) VALUES ('shed · roof', 2, NOW())");
                foreach (var roof in new[] { roofA, roofB })
                    db.Execute($"INSERT INTO roof_style_course (roof_style_id, part, ordinal, style_id, height) VALUES ({roof}, 'roof', 0, {quartz}, 1)");
                db.Execute($"UPDATE room_style SET roof_style_id = {roofB} WHERE id = {house}");

                map = Id(db, "INSERT INTO map (slug) VALUES ('weir')");
                var refinement = $$$"""
                    {"materials": {"rim": {"library": "quartz rim", "row": {{{quartz}}}, "data": 1},
                                   "walls": {"library": {{{copy}}}, "row": {{{copy}}}},
                                   "barn": {"library": "jungle-saltbox-cottage · wall 1"} },
                     "roomStyles": {"wool": {"library": "quartz rim"} } }
                    """;
                db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, 'refinement_json', @data)",
                    new DataParameter("map", map), new DataParameter("data", Encoding.UTF8.GetBytes(refinement)));
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 58);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(Names(db, "style")).IsEquivalentTo(["cobblestone-andesite-columnar-noise"])
                    .Because("both blocks leave, the copy merges by content, and the seed folder states the barn's reseeded noise as the same pattern");

                await Assert.That(Slot(db, "theme_bucket", $"theme_id = {theme} AND bucket = 'rim'")).IsEqualTo("block 155:0");
                await Assert.That(Slot(db, "theme_bucket", $"theme_id = {theme} AND bucket = 'wall'")).IsEqualTo($"style {noise}");
                await Assert.That(Slot(db, "room_style_course", $"room_style_id = {house} AND part = 'wall'")).IsEqualTo($"style {noise}");
                await Assert.That(Slot(db, "room_style_course", $"room_style_id = {house} AND part = 'post'")).IsEqualTo("laid 17:1");

                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM roof_style")).IsEqualTo(1L);
                await Assert.That(db.Execute<long>($"SELECT roof_style_id FROM room_style WHERE id = {house}")).IsEqualTo(roofA)
                    .Because("the house wearing the merged roof wears the one kept");

                var data = db.Execute<byte[]>("SELECT data FROM map_artifact WHERE kind = 'refinement_json'");
                var kept = JsonNode.Parse(Encoding.UTF8.GetString(data))!;
                await Assert.That(kept["materials"]!["rim"]!.ToJsonString())
                    .IsEqualTo("""{"kind":"solid","id":155,"data":1}""")
                    .Because("a block named from the library is the block, with the fields stated beside the name laid over it");
                await Assert.That(kept["materials"]!["walls"]!["library"]!.GetValue<long>()).IsEqualTo(noise);
                await Assert.That(kept["materials"]!["walls"]!["row"]!.GetValue<long>()).IsEqualTo(noise);
                await Assert.That(kept["materials"]!["barn"]!["library"]!.GetValue<string>())
                    .IsEqualTo("cobblestone-andesite-columnar-noise");
                await Assert.That(kept["roomStyles"]!["wool"]!["library"]!.GetValue<string>()).IsEqualTo("quartz rim")
                    .Because("a room style that happens to share a block row's name is a room style");
            }

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 57);
            await using (var db = TestDb.Connect())
            {
                var rim = db.Execute<long>($"SELECT style_id FROM theme_bucket WHERE theme_id = {theme} AND bucket = 'rim'");
                await Assert.That(db.Execute<string>($"SELECT params_json FROM style WHERE id = {rim}"))
                    .IsEqualTo("""{"kind":"solid","id":155,"data":0}""").Because("rolled back, a block takes a row again");
                var post = db.Execute<long>($"SELECT style_id FROM room_style_course WHERE room_style_id = {house} AND part = 'post'");
                await Assert.That(db.Execute<string>($"SELECT kind FROM style WHERE id = {post}")).IsEqualTo("laidLog");
                await Assert.That(db.Execute<long>($"SELECT style_id FROM roof_style_course WHERE roof_style_id = {roofA}")).IsEqualTo(rim)
                    .Because("one block is one row, however many slots lay it");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Style(DataConnection db, string name, string kind, string json)
    {
        db.Execute("INSERT INTO style (name, kind, params_json, created_at) VALUES (@name, @kind, @json, NOW())",
            new DataParameter("name", name), new DataParameter("kind", kind), new DataParameter("json", json));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static long Id(DataConnection db, string insert)
    {
        db.Execute(insert);
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    /// <summary>What one slot holds: <c>style N</c>, <c>block id:data</c> or <c>laid id:data</c>.</summary>
    private static string Slot(DataConnection db, string table, string where)
        => db.Execute<string>(
            $"SELECT IF(block_id IS NULL, CONCAT('style ', style_id), CONCAT(IF(block_laid, 'laid ', 'block '), block_id, ':', block_data)) FROM {table} WHERE {where}");

    private static List<string> Names(DataConnection db, string table) =>
        [.. db.Query<string>($"SELECT name FROM {table}")];
}
