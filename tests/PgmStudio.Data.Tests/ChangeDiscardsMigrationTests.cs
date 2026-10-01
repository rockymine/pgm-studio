using LinqToDB.Data;
using PgmStudio.Data.Map;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0054</c> gives a change the list of earlier changes it dropped. Every stored change reads back as having
/// dropped none, a change stamped with a list reads it back, and rolling back keeps every change. Rolls the schema
/// back one migration and forward again, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class ChangeDiscardsMigrationTests
{
    [Test]
    public async Task A_stored_change_dropped_none_a_stamped_one_keeps_its_list_and_rolling_back_keeps_both()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 53);
        try
        {
            await using (var db = TestDb.Connect())
                db.Execute("INSERT INTO map_change (map_slug, number, created_at) VALUES ('weir', 4, '2026-05-01 10:00:00')");

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                var log = new MapChangeLog(db);
                await log.OpenAsync("weir", new ChangeStamp(Discarded: [4]));
                var changes = await log.ListAsync("weir");
                await Assert.That(changes.Single(change => change.Number == 4).Discarded).IsEmpty();
                await Assert.That(changes.Single(change => change.Number != 4).Discarded).IsEquivalentTo([4L]);
            }

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 53);
            await using (var db = TestDb.Connect())
                await Assert.That(Count(db)).IsEqualTo(2L).Because("rolling back drops the list, never a change");
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static long Count(DataConnection db) => db.Execute<long>("SELECT COUNT(*) FROM map_change");
}
