using PgmStudio.Data.Schema;
using PgmStudio.Tests;

namespace PgmStudio.Data.Tests;

/// <summary>
/// Test database helper. Targets the local <c>pgm_studio_test</c> schema (override with the
/// <c>PGM_STUDIO_TEST_DB</c> env var) — the <c>pgm</c> user has table privileges but not
/// DROP/CREATE DATABASE, so a reset works inside the schema rather than replacing it.
/// </summary>
internal static class TestDb
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PGM_STUDIO_TEST_DB")
        ?? "Server=localhost;Database=pgm_studio_test;User ID=pgm;Password=pgm_dev_pw;";

    /// <summary>The migrated schema with no rows in it — what nearly every test wants.</summary>
    public static Task ResetSchemaAsync() => TestSchema.ResetAsync(ConnectionString);

    /// <summary>The same schema arrived at by dropping every table and migrating again, for the tests whose
    /// subject is the migrating itself.</summary>
    public static Task RebuildSchemaAsync() => TestSchema.RebuildAsync(ConnectionString);

    public static PgmDb Connect() => new(PgmDataOptions.ForConnectionString(ConnectionString));
}
