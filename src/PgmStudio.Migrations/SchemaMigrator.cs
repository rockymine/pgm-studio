using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace PgmStudio.Migrations;

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
}
