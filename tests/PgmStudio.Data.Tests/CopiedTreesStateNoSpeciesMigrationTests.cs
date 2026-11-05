using LinqToDB;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0092</c> takes the species off every copied tree and leaves a template's alone. Rolls the schema back and
/// forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class CopiedTreesStateNoSpeciesMigrationTests
{
    [Test]
    public async Task A_copied_tree_loses_its_species_and_a_template_keeps_its()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 91);
        try
        {
            await using (var db = TestDb.Connect())
                db.Execute("INSERT INTO tree_style (name, form, species, height, body) VALUES "
                           + "('acacia-1', 'copied', 'oak', 9, '[]'), ('birch', 'template', 'birch', 12, '')");

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 92);
            await using (var db = TestDb.Connect())
            {
                var rows = db.Query<Tree>("SELECT name AS Name, species AS Species FROM tree_style ORDER BY name")
                    .Select(row => (row.Name, row.Species)).ToList();
                await Assert.That(rows).IsEquivalentTo([("acacia-1", (string?)null), ("birch", "birch")]);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private sealed class Tree
    {
        public string Name { get; set; } = "";
        public string? Species { get; set; }
    }
}
