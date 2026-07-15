using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using PgmStudio.Migrations;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Runs once at assembly load — before any test or app host — and pins the connection-string environment
/// variables at the <c>pgm_studio_test</c> schema, overwriting whatever the shell carried. Minimal hosting
/// resolves <c>ConnectionStrings:PgmStudio</c> straight from the environment, ahead of a factory's
/// <c>ConfigureAppConfiguration</c>/<c>UseSetting</c>, so a dev server's <c>ConnectionStrings__PgmStudio</c>
/// would otherwise silently point every test at the live dev database (never reset → accumulating counts).
/// </summary>
internal static class ApiTestBootstrap
{
    [ModuleInitializer]
    internal static void ForceTestDatabase()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PgmStudio", ApiTestFactory.ConnectionString);
        Environment.SetEnvironmentVariable("PGM_STUDIO_DB", null);
    }
}

/// <summary>
/// The single <see cref="WebApplicationFactory{TEntryPoint}"/> every DB-touching Api test boots. It pins the
/// connection string to the <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>) via an
/// in-memory config source added <em>last</em>, so it wins over any ambient <c>ConnectionStrings__PgmStudio</c>
/// / <c>PGM_STUDIO_DB</c> the shell happens to carry (e.g. a running dev server's) — without this the tests
/// silently ran against the dev database, which the per-test reset never cleared, so counts accumulated.
/// The DB-mutating classes reset the schema per test and share the <c>[NotInParallel("api-db")]</c> group so
/// no reset overlaps another booting test.
/// </summary>
internal sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    // Every DB-touching test class shares [NotInParallel("api-db")] so resets never overlap another test.
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PGM_STUDIO_TEST_DB")
        ?? "Server=localhost;Database=pgm_studio_test;User ID=pgm;Password=pgm_dev_pw;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PgmStudio"] = ConnectionString,
        }));
    }

    /// <summary>Drop every table then re-apply the migrations — a clean schema for the next test.</summary>
    public static async Task ResetSchemaAsync()
    {
        await using (var conn = new MySqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var tables = new List<string>();
            await using (var cmd = new MySqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    tables.Add(reader.GetString(0));
            if (tables.Count > 0)
            {
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=0");
                foreach (var t in tables) await Exec(conn, $"DROP TABLE IF EXISTS `{t}`");
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=1");
            }
        }
        SchemaMigrator.MigrateUp(ConnectionString);
    }

    private static async Task Exec(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
