using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PgmStudio.Tests;

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
/// The <see cref="WebApplicationFactory{TEntryPoint}"/> every DB-touching Api test runs against. It pins the
/// connection string to the <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>) via an
/// in-memory config source added <em>last</em>, so it wins over any ambient <c>ConnectionStrings__PgmStudio</c>
/// / <c>PGM_STUDIO_DB</c> the shell happens to carry (e.g. a running dev server's) — without this the tests
/// silently ran against the dev database, which the per-test reset never cleared, so counts accumulated.
/// The DB-mutating classes reset the schema per test and share the <c>[NotInParallel("api-db")]</c> group so
/// no reset overlaps another test.
///
/// <para>There is <b>one host</b> for the whole assembly (<see cref="Shared"/>), and what makes that safe is
/// where the API keeps its state: every service that reads or writes the database is scoped, so it is built
/// per request and holds nothing across one, and the only singletons are immutable configuration records.
/// A host therefore has no memory of the schema it last saw, which is what a per-test reset would otherwise
/// have to clear — and booting one per test cost about half a second each with nothing to show for it.</para>
/// </summary>
internal sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    // Every DB-touching test class shares [NotInParallel("api-db")] so resets never overlap another test.
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PGM_STUDIO_TEST_DB")
        ?? "Server=localhost;Database=pgm_studio_test;User ID=pgm;Password=pgm_dev_pw;";

    /// <summary>The host every test takes its client from. Built on first use and left running for the life
    /// of the process, which is the whole of the test run — there is nothing to dispose it after, and a host
    /// disposed between tests is a host rebuilt before the next one.</summary>
    public static ApiTestFactory Shared { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PgmStudio"] = ConnectionString,
        }));
    }

    /// <summary>The migrated schema with no rows in it — a clean database for the next test.</summary>
    public static Task ResetSchemaAsync() => TestSchema.ResetAsync(ConnectionString);

    /// <summary>Run one statement against the test schema — for arranging a row state no endpoint produces,
    /// such as a plan stamped by an older composer.</summary>
    public static Task ExecuteAsync(string sql) => TestSchema.ExecuteAsync(ConnectionString, sql);
}
