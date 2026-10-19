using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0084</c> moves every plan-stage map to <c>sketch</c>, gives one holding no layout the layout a fresh draft
/// starts with, and keeps every plan and every drawn layout as it was; rolling back takes the given layouts
/// away and puts <c>plan</c> back. Rolls the schema back and forward, so it runs apart from every other
/// database test.
/// </summary>
[NotInParallel]
public sealed class PlanStageRetiredMigrationTests
{
    private const string Plan = """{"plan":2,"pieces":[]}""";
    private const string Drawn = """{"layers":[{"id":"ground","layout":{"shapes":[{"id":"s1"}],"groups":[]}}]}""";

    [Test]
    public async Task Every_planned_map_is_a_sketch_with_ground_to_draw_on()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 83);
        try
        {
            await using (var db = TestDb.Connect())
            {
                Map(db, "planned-only", "plan", plan: Plan);
                Map(db, "planned-and-drawn", "plan", plan: Plan, layout: Drawn);
                Map(db, "empty-plan-draft", "plan");
                Map(db, "a-sketch", "sketch", layout: Drawn);
                Map(db, "configured", "configure");
                Map(db, "imported", "edit");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 84);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(Stages(db)).IsEquivalentTo(new Dictionary<string, string>
                {
                    ["planned-only"] = "sketch", ["planned-and-drawn"] = "sketch", ["empty-plan-draft"] = "sketch",
                    ["a-sketch"] = "sketch", ["configured"] = "configure", ["imported"] = "edit",
                });
                foreach (var slug in (string[])["planned-only", "empty-plan-draft"])
                {
                    var layers = JsonNode.Parse(Artifact(db, slug, "sketch_layout_json")!)!["layers"]!.AsArray();
                    await Assert.That(layers.Count).IsEqualTo(1).Because($"{slug} holds the ground layer a draft starts with");
                    await Assert.That(layers[0]!["id"]!.GetValue<string>()).IsEqualTo("ground");
                    await Assert.That(layers[0]!["layout"]!["shapes"]!.AsArray().Count).IsEqualTo(0);
                }
                await Assert.That(Artifact(db, "planned-and-drawn", "sketch_layout_json")).IsEqualTo(Drawn)
                    .Because("a drawn layout is kept as it was");
                await Assert.That(Artifact(db, "planned-only", "plan_json")).IsEqualTo(Plan).Because("a plan is history");
                await Assert.That(Artifact(db, "configured", "sketch_layout_json")).IsNull()
                    .Because("only a planned map is given a layout");
            }

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 83);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(Stages(db)["planned-only"]).IsEqualTo("plan");
                await Assert.That(Stages(db)["planned-and-drawn"]).IsEqualTo("plan");
                await Assert.That(Artifact(db, "planned-only", "sketch_layout_json")).IsNull()
                    .Because("the layout the step gave is taken away again");
                await Assert.That(Artifact(db, "planned-and-drawn", "sketch_layout_json")).IsEqualTo(Drawn);
                await Assert.That(Stages(db)["a-sketch"]).IsEqualTo("sketch");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static void Map(DataConnection db, string slug, string stage, string? plan = null, string? layout = null)
    {
        db.Execute("INSERT INTO map (slug, stage) VALUES (@slug, @stage)",
            new DataParameter("slug", slug), new DataParameter("stage", stage));
        var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
        if (plan is not null) Artifact(db, map, "plan_json", plan);
        if (layout is not null) Artifact(db, map, "sketch_layout_json", layout);
    }

    private static void Artifact(DataConnection db, long map, string kind, string json) =>
        db.Execute("INSERT INTO map_artifact (map_id, kind, data, revision) VALUES (@map, @kind, @data, 3)",
            new DataParameter("map", map), new DataParameter("kind", kind),
            new DataParameter("data", Encoding.UTF8.GetBytes(json)));

    private static string? Artifact(DataConnection db, string slug, string kind)
    {
        var data = db.Execute<byte[]?>(
            "SELECT a.data FROM map_artifact a JOIN map m ON m.id = a.map_id WHERE m.slug = @slug AND a.kind = @kind",
            new DataParameter("slug", slug), new DataParameter("kind", kind));
        return data is null ? null : Encoding.UTF8.GetString(data);
    }

    private static Dictionary<string, string> Stages(DataConnection db) =>
        db.Query<string>("SELECT slug FROM map").ToList().ToDictionary(slug => slug, slug =>
            db.Execute<string>("SELECT stage FROM map WHERE slug = @slug", new DataParameter("slug", slug)));
}
