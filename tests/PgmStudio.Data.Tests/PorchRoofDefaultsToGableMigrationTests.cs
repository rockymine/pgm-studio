using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0056</c> makes a gable the porch form a row takes when it names none, in both columns a porch's form is
/// stored in, and leaves every stored row's own form as it was. Rolls the schema back one migration and forward
/// again, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class PorchRoofDefaultsToGableMigrationTests
{
    [Test]
    public async Task A_porch_naming_no_form_is_a_gable_and_a_stored_shed_stays()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 55);
        try
        {
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO room_style (name, porch_depth, created_at) VALUES ('stone-house', 2, NOW())");
                db.Execute("INSERT INTO porch_style (name, created_at) VALUES ('stoop', NOW())");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
            {
                await Assert.That(db.Execute<string>("SELECT porch_roof FROM room_style WHERE name = 'stone-house'"))
                    .IsEqualTo("shed").Because("a house keeps the porch it was saved with");
                await Assert.That(db.Execute<string>("SELECT roof_form FROM porch_style WHERE name = 'stoop'")).IsEqualTo("shed");

                db.Execute("INSERT INTO room_style (name, created_at) VALUES ('new-house', NOW())");
                db.Execute("INSERT INTO porch_style (name, created_at) VALUES ('new-porch', NOW())");
                await Assert.That(db.Execute<string>("SELECT porch_roof FROM room_style WHERE name = 'new-house'")).IsEqualTo("gable");
                await Assert.That(db.Execute<string>("SELECT roof_form FROM porch_style WHERE name = 'new-porch'")).IsEqualTo("gable");
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }
}
