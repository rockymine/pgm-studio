using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The schema-drift guard: <see cref="SchemaMigrator.LatestKnownVersion"/> discovers the newest
/// migration from the assembly (no database), and <see cref="SchemaMigrator.GetSchemaState"/> /
/// <see cref="SchemaMigrator.AssertUpToDate"/> compare a real MariaDB against it. The drift case is
/// simulated by deleting the top VersionInfo row so the migration reads as unapplied.
/// Runs serially — shares the test schema and resets it at the start of each test.
/// </summary>
[NotInParallel]
public sealed class SchemaMigratorTests
{
    [Test]
    public async Task LatestKnownVersion_matches_the_newest_migration()
    {
        // Pure (no database): the newest migration currently in the assembly.
        await Assert.That(SchemaMigrator.LatestKnownVersion()).IsGreaterThanOrEqualTo(6L);
    }

    [Test]
    public async Task Freshly_migrated_database_is_up_to_date()
    {
        await TestDb.ResetSchemaAsync();

        var state = SchemaMigrator.GetSchemaState(TestDb.ConnectionString);

        await Assert.That(state.IsUpToDate).IsTrue();
        await Assert.That(state.Pending).IsEmpty();
        await Assert.That(state.AppliedVersion).IsEqualTo(SchemaMigrator.LatestKnownVersion());
        // No-op when up to date.
        SchemaMigrator.AssertUpToDate(TestDb.ConnectionString);
    }

    [Test]
    public async Task Missing_top_migration_is_reported_pending_and_fails_the_guard()
    {
        await TestDb.ResetSchemaAsync();
        var latest = SchemaMigrator.LatestKnownVersion();

        // Simulate a database that predates the newest migration: forget it was applied.
        await using (var db = TestDb.Connect())
            db.Execute($"DELETE FROM VersionInfo WHERE Version = {latest}");

        var state = SchemaMigrator.GetSchemaState(TestDb.ConnectionString);
        await Assert.That(state.IsUpToDate).IsFalse();
        await Assert.That(state.Pending).Contains(latest);

        var ex = Assert.Throws<SchemaOutOfDateException>(
            () => SchemaMigrator.AssertUpToDate(TestDb.ConnectionString));
        await Assert.That(ex!.Message).Contains(SchemaMigrator.FormatVersion(latest));
        await Assert.That(ex.Message).Contains("--migrate-only");
    }
}
