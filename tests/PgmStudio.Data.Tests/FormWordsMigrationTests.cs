using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0075</c> rewrites a hub's and a front line's stored form into the merged form words — in the library's board
/// columns and in the hub and front-line slots of a generated plan's structure key — and leaves the wool words and
/// the words that were already merged alone. Rolls the schema back and forward, so it runs apart from every other
/// database test.
/// </summary>
[NotInParallel]
public sealed class FormWordsMigrationTests
{
    [Test]
    public async Task Hub_and_front_line_words_become_the_merged_form_words()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 74);
        try
        {
            await using (var db = TestDb.Connect())
            {
                var boards = new[] { ("bar", "twin"), ("single", "none"), ("ring", "bar"), ("arms-3", "single"), ("two-u", "none") };
                for (var seed = 0; seed < boards.Length; seed++)
                    db.Execute("INSERT INTO composed_board (composer_version, band, symmetry, cell, seed, score, wool_count, "
                               + "wools, hub, frontline, card_json, plan_json, created_at) VALUES ('v', 'b', 'rot_180', 5, "
                               + "@seed, 0, 2, ',i,l,', @hub, @front, '{}', '{}', NOW())",
                        new DataParameter("seed", seed), new DataParameter("hub", boards[seed].Item1),
                        new DataParameter("front", boards[seed].Item2));
                foreach (var structure in new[]
                         {
                             "wools:donut,l|hub:ring|front:twin", "wools:i|hub:bar|front:bar",
                             "wools:u,u|hub:arms-3|front:single", "wools:l|hub:double-hole|front:none",
                         })
                    db.Execute("INSERT INTO plan (name, origin, plan_json, content_hash, structure, created_at, updated_at) "
                               + "VALUES ('p', 'generated', '{}', @hash, @structure, NOW(), NOW())",
                        new DataParameter("hash", structure.GetHashCode().ToString("x").PadLeft(64, '0')),
                        new DataParameter("structure", structure));
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 75);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Query<string>("SELECT CONCAT(hub, '/', frontline) FROM composed_board ORDER BY seed").ToList())
                    .IsEquivalentTo(["i/u", "l/none", "donut/i", "e/l", "two-u/none"]);
                await Assert.That(db.Query<string>("SELECT wools FROM composed_board").Distinct().ToList())
                    .IsEquivalentTo([",i,l,"]).Because("a wool's words are already the merged ones");
                await Assert.That(db.Query<string>("SELECT structure FROM plan ORDER BY id").ToList())
                    .IsEquivalentTo([
                        "wools:donut,l|hub:donut|front:u", "wools:i|hub:i|front:i",
                        "wools:u,u|hub:e|front:l", "wools:l|hub:double-hole|front:none",
                    ]);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }
}
