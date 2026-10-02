using System.Text;
using System.Text.Json.Nodes;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0060</c> makes every stored library name a name — a <c>+</c> spelled <c>plus</c>, any other character a name
/// may not hold a space — gives each later row sharing a name the first free count after it, puts a unique index
/// over each table's names, and makes each map's current refinement follow a renamed row. Rolls the schema back and
/// forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class LibraryNamesMigrationTests
{
    [Test]
    public async Task Names_are_made_names_once_each_and_a_refinement_follows()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 59);
        try
        {
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO biome_pattern (name, kind, params_json, created_at) VALUES "
                           + "('Mesa (Bryce)', 'solid', '{}', NOW()), ('Extreme hills+ M', 'solid', '{}', NOW())");
                db.Execute("INSERT INTO theme (name, created_at) VALUES ('Frost night', NOW()), ('frost NIGHT', NOW()), "
                           + "('dusk & dawn', NOW())");
                db.Execute("INSERT INTO map (slug) VALUES ('weir')");
                var map = db.Execute<long>("SELECT LAST_INSERT_ID()");
                var refinement = """
                    {"biome": {"library": "Mesa (Bryce)"},
                     "themes": {"heath": {"library": "dusk & dawn"}, "frost": {"library": "Frost night"}},
                     "materials": {"rim": {"library": "dusk & dawn"}}}
                    """;
                db.Execute("INSERT INTO map_artifact (map_id, kind, data) VALUES (@map, 'refinement_json', @data)",
                    new DataParameter("map", map), new DataParameter("data", Encoding.UTF8.GetBytes(refinement)));
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 60);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Query<string>("SELECT name FROM biome_pattern").ToList())
                    .IsEquivalentTo(["Mesa Bryce", "Extreme hills plus M"]);
                await Assert.That(db.Query<string>("SELECT name FROM theme ORDER BY id").ToList())
                    .IsEquivalentTo(["Frost night", "frost NIGHT-2", "dusk dawn"])
                    .Because("the lower id keeps a shared name, and a later row counts on from it");

                var data = db.Execute<byte[]>("SELECT data FROM map_artifact WHERE kind = 'refinement_json'");
                var kept = JsonNode.Parse(Encoding.UTF8.GetString(data))!;
                await Assert.That(kept["biome"]!["library"]!.GetValue<string>()).IsEqualTo("Mesa Bryce");
                await Assert.That(kept["themes"]!["heath"]!["library"]!.GetValue<string>()).IsEqualTo("dusk dawn");
                await Assert.That(kept["themes"]!["frost"]!["library"]!.GetValue<string>()).IsEqualTo("Frost night");
                await Assert.That(kept["materials"]!["rim"]!["library"]!.GetValue<string>()).IsEqualTo("dusk & dawn")
                    .Because("a material is a pattern, and no pattern carried that name");

                await Assert.That(() => db.Execute("INSERT INTO theme (name, created_at) VALUES ('FROST night', NOW())"))
                    .Throws<Exception>().Because("one name is one row of its kind");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }
}
