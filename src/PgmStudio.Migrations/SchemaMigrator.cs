using FluentMigrator;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace PgmStudio.Migrations;

/// <summary>
/// The schema state of a database relative to the migrations defined in this assembly:
/// the newest applied version, and the ordered list of known-but-unapplied versions.
/// A fresh database (no VersionInfo table) reports <see cref="AppliedVersion"/> 0 and
/// every known migration as pending.
/// </summary>
public sealed record SchemaState(long AppliedVersion, IReadOnlyList<long> Pending)
{
    public bool IsUpToDate => Pending.Count == 0;
}

/// <summary>
/// Thrown when a database is behind the migrations defined in the assembly and the caller
/// requires it to be up to date (the API startup guard). The message names the pending
/// versions and the command that applies them.
/// </summary>
public sealed class SchemaOutOfDateException(string message) : Exception(message);

/// <summary>
/// Runs the FluentMigrator migrations in this assembly against a MariaDB connection string.
/// Used by the API host at startup and by integration tests.
/// </summary>
public static class SchemaMigrator
{
    private static ServiceProvider BuildServices(string connectionString) =>
        new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddMySql5()                                  // MariaDB-compatible generator
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(SchemaMigrator).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(validateScopes: false);

    /// <summary>Apply all pending migrations.</summary>
    public static void MigrateUp(string connectionString)
    {
        using var sp = BuildServices(connectionString);
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }

    /// <summary>Roll back to a target version (0 = revert everything).</summary>
    public static void MigrateDown(string connectionString, long version = 0)
    {
        using var sp = BuildServices(connectionString);
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateDown(version);
    }

    /// <summary>
    /// The newest migration version defined in this assembly. Pure — reflects over the
    /// <see cref="MigrationAttribute"/>s, no database access.
    /// </summary>
    public static long LatestKnownVersion() =>
        typeof(SchemaMigrator).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(MigrationAttribute), false).Cast<MigrationAttribute>())
            .Select(a => a.Version)
            .DefaultIfEmpty(0L)
            .Max();

    /// <summary>
    /// Compare the database's applied migrations against those known in this assembly, without
    /// modifying the database. Reading the VersionInfo table is non-mutating; a database whose
    /// VersionInfo table does not exist yet reads as fully pending.
    /// </summary>
    public static SchemaState GetSchemaState(string connectionString)
    {
        using var sp = BuildServices(connectionString);
        using var scope = sp.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        var versionLoader = scope.ServiceProvider.GetRequiredService<IVersionLoader>();
        versionLoader.LoadVersionInfo();
        var applied = versionLoader.VersionInfo;
        var pending = runner.MigrationLoader.LoadMigrations().Keys
            .Where(v => !applied.HasAppliedMigration(v))
            .OrderBy(v => v)
            .ToList();
        return new SchemaState(applied.Latest(), pending);
    }

    /// <summary>
    /// Throw <see cref="SchemaOutOfDateException"/> if the database is missing any migration known
    /// to this assembly. The message names the pending versions and the exact command to apply them.
    /// Does not apply migrations — the database lifecycle stays explicit.
    /// </summary>
    public static void AssertUpToDate(string connectionString)
    {
        var state = GetSchemaState(connectionString);
        if (state.IsUpToDate) return;
        var pending = string.Join(", ", state.Pending.Select(FormatVersion));
        throw new SchemaOutOfDateException(
            $"Database schema is out of date: applied version {state.AppliedVersion}, " +
            $"latest known {FormatVersion(LatestKnownVersion())}. " +
            $"Pending migration(s): {pending}. " +
            "Apply them before starting the API:\n" +
            "  dotnet run --project src/PgmStudio.Import -- --migrate-only");
    }

    /// <summary>Format a migration version as its source id (e.g. 6 → "M0006").</summary>
    public static string FormatVersion(long version) => $"M{version:D4}";
}
