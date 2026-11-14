using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0096</c> gives every stored room style the shell its card was already drawn on, <c>8x8</c>. Rolls the schema
/// back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class HouseCardFootprintMigrationTests
{
    [Test]
    public async Task Every_stored_house_is_drawn_on_the_default_shell()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 95);
        try
        {
            long row;
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO room_style (name, created_at) VALUES (@name, NOW())", new DataParameter("name", "cottage"));
                row = db.Execute<long>("SELECT LAST_INSERT_ID()");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 96);

            await using (var db = TestDb.Connect())
                await Assert.That(db.Execute<string>($"SELECT footprint FROM room_style WHERE id = {row}")).IsEqualTo("8x8");
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }
}
