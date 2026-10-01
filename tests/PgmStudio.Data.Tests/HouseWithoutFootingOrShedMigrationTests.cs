using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0056</c> carries every stored house to the shape a house is saved in: a shed is a gable wherever a library
/// row states one, no room style binds a footing, and each map's current sketch layout and refinement state no
/// footing and no shed for a house, a wing or a porch — while a tree's form, a document that will not parse and
/// a footing already stated as none are left as they were. Rolls the schema back one migration and forward
/// again, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class HouseWithoutFootingOrShedMigrationTests
{
    private const string Layout = """
        {"dressing": {"props": [
            {"kind": "house", "id": "mill", "wings": [{"corners": [[0, 0], [6, 6]], "spec": {"form": "shed"}}],
             "style": {"foundation": {"footing": {"kind": "solid", "id": 4}},
                       "roof": {"form": "shed", "pitch": 1},
                       "porch": {"depth": 2, "roof": "shed"}}},
            {"kind": "tree", "id": "oak", "spec": {"form": "oak"}}]},
         "roomStyles": {"spawn": {"foundation": {"footing": null}, "roof": {"form": "gable"}}}}
        """;

    private const string Refinement = """
        {"roomStyles": {"spawn": {"library": "stone-house", "foundation": {"footing": {"kind": "solid", "id": 4}}}},
         "dressing": {"styles": {"barn": {"kind": "house", "library": "hay-gambrel-barn", "porch": {"roof": "Shed"}}}}}
        """;

    [Test]
    public async Task Every_stored_house_is_carried_to_no_footing_and_no_shed()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 55);
        try
        {
            long layout, refinement, broken;
            await using (var db = TestDb.Connect())
            {
                var roof = Id(db, "INSERT INTO roof_style (name, form, created_at) VALUES ('lean', 'shed', NOW())");
                Id(db, "INSERT INTO porch_style (name, roof_form, created_at) VALUES ('stoop', 'shed', NOW())");
                var house = Id(db, "INSERT INTO room_style (name, roof_form, porch_depth, porch_roof, roof_style_id, created_at) "
                                   + $"VALUES ('stone-house', 'shed', 2, 'shed', {roof}, NOW())");
                var stone = Id(db, "INSERT INTO style (name, kind, params_json, created_at) VALUES ('cobble', 'solid', '{}', NOW())");
                db.Execute($"INSERT INTO room_style_course (room_style_id, part, ordinal, style_id) VALUES ({house}, 'sill', 0, {stone})");
                db.Execute($"INSERT INTO room_style_course (room_style_id, part, ordinal, style_id) VALUES ({house}, 'wall', 0, {stone})");

                var map = Id(db, "INSERT INTO map (slug) VALUES ('weir')");
                layout = Artifact(db, map, "sketch_layout_json", Layout);
                refinement = Artifact(db, map, "refinement_json", Refinement);
                broken = Artifact(db, Id(db, "INSERT INTO map (slug) VALUES ('torn')"), "sketch_layout_json", "{\"dressing\": ");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<string>("SELECT form FROM roof_style WHERE name = 'lean'")).IsEqualTo("gable");
                await Assert.That(db.Execute<string>("SELECT roof_form FROM porch_style WHERE name = 'stoop'")).IsEqualTo("gable");
                await Assert.That(db.Execute<string>("SELECT CONCAT(roof_form, '/', porch_roof) FROM room_style WHERE name = 'stone-house'"))
                    .IsEqualTo("gable/gable");
                await Assert.That(Parts(db)).IsEquivalentTo(["wall"])
                    .Because("the footing course leaves and the wall it shared a material with stays");
                await Assert.That(db.Execute<long>("SELECT COUNT(*) FROM style WHERE name = 'cobble'")).IsEqualTo(1L);

                db.Execute("INSERT INTO room_style (name, created_at) VALUES ('new-house', NOW())");
                db.Execute("INSERT INTO porch_style (name, created_at) VALUES ('new-porch', NOW())");
                await Assert.That(db.Execute<string>("SELECT porch_roof FROM room_style WHERE name = 'new-house'")).IsEqualTo("gable");
                await Assert.That(db.Execute<string>("SELECT roof_form FROM porch_style WHERE name = 'new-porch'")).IsEqualTo("gable");

                var sketch = Document(db, layout);
                var mill = sketch["dressing"]!["props"]![0]!;
                await Assert.That(mill["style"]!["foundation"]!.AsObject().ContainsKey("footing")).IsTrue();
                await Assert.That(mill["style"]!["foundation"]!["footing"]).IsNull();
                await Assert.That(Word(mill["style"]!["roof"]!["form"])).IsEqualTo("gable");
                await Assert.That(Word(mill["style"]!["porch"]!["roof"])).IsEqualTo("gable");
                await Assert.That(Word(mill["wings"]![0]!["spec"]!["form"])).IsEqualTo("gable");
                await Assert.That(Word(sketch["dressing"]!["props"]![1]!["spec"]!["form"])).IsEqualTo("oak")
                    .Because("a tree's form is not a roof's");

                var stated = Document(db, refinement);
                await Assert.That(stated["roomStyles"]!["spawn"]!["foundation"]!["footing"]).IsNull();
                await Assert.That(Word(stated["dressing"]!["styles"]!["barn"]!["porch"]!["roof"])).IsEqualTo("gable");

                await Assert.That(Text(db, broken)).IsEqualTo("{\"dressing\": ")
                    .Because("a document that will not parse is left as it was found");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Artifact(DataConnection db, long map, string kind, string json)
    {
        db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, @kind, @data)",
            new DataParameter("map", map), new DataParameter("kind", kind),
            new DataParameter("data", Encoding.UTF8.GetBytes(json)));
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static long Id(DataConnection db, string insert)
    {
        db.Execute(insert);
        return db.Execute<long>("SELECT LAST_INSERT_ID()");
    }

    private static List<string> Parts(DataConnection db) =>
        [.. db.Query<string>("SELECT part FROM room_style_course")];

    private static string Text(DataConnection db, long artifact) =>
        Encoding.UTF8.GetString(db.Execute<byte[]>($"SELECT data FROM map_artifact WHERE id = {artifact}"));

    private static JsonNode Document(DataConnection db, long artifact) => JsonNode.Parse(Text(db, artifact))!;

    private static string? Word(JsonNode? node) => node?.GetValue<string>();
}
