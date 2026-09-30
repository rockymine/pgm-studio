using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0049</c> renames the tree showcase's copied rows from the band and place they stood at to the kind the
/// author states for that band, counted through the bands a kind spans; a tree its band does not describe takes
/// its own kind, and a row from any other world, or one no longer carrying a band, is left alone. Rolls the
/// schema back one migration and forward again, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class ShowcaseTreesNamedByKindMigrationTests
{
    private const string Body = "[[0,0,0,17,0],[0,1,0,18,0]]";

    [Test]
    public async Task A_showcase_row_takes_its_bands_kind_and_every_other_row_keeps_its_name()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 48);
        try
        {
            await using (var db = TestDb.Connect())
            {
                string[] showcase =
                [
                    "tree-showcase-r17-1", "tree-showcase-r17-2", "tree-showcase-r2-3", "tree-showcase-r3-1",
                    "tree-showcase-r7-3", "tree-showcase-r7-4", "tree-showcase-r7-5", "showcase-r13-9",
                ];
                var values = showcase.Select((name, at) =>
                    $"('{name}', 'copied', 'oak', 2, '{Body}', '/srv/mapgen/corpus/tree-showcase', {at}, 1, {at}, NOW(), NOW())");
                db.Execute("INSERT INTO tree_style (name, form, species, height, body, cut_world, cut_x, cut_y, cut_z, cut_at, created_at) VALUES "
                           + string.Join(", ", values)
                           + $", ('grove-r17-1', 'copied', 'oak', 2, '{Body}', '/srv/grove', 1, 1, 1, NOW(), NOW())"
                           + $", ('oak', 'template', 'oak', 12, '', NULL, NULL, NULL, NULL, NULL, NOW())");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                string Named(int cutX) => db.Execute<string>(
                    $"SELECT name FROM tree_style WHERE cut_world LIKE '%/tree-showcase' AND cut_x = {cutX}");
                await Assert.That(Named(0)).IsEqualTo("willow-1");
                await Assert.That(Named(1)).IsEqualTo("willow-2");
                await Assert.That(Named(2)).IsEqualTo("large-pine-1");
                await Assert.That(Named(3)).IsEqualTo("large-pine-2");
                await Assert.That(Named(4)).IsEqualTo("tall-spruce-1");
                await Assert.That(Named(5)).IsEqualTo("sequoia-1");
                await Assert.That(Named(6)).IsEqualTo("tall-spruce-2");
                await Assert.That(Named(7)).IsEqualTo("birch-1");
                await Assert.That(db.Execute<int>("SELECT COUNT(*) FROM tree_style WHERE name IN ('grove-r17-1', 'oak')"))
                    .IsEqualTo(2);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }
}
