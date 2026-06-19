using MySqlConnector;
using PgmStudio.Data.Schema;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// Test database helper. Targets the local <c>pgm_studio_test</c> schema (override with the
/// <c>PGM_STUDIO_TEST_DB</c> env var). Resets by dropping every table in the schema — the
/// <c>pgm</c> user has table privileges but not DROP/CREATE DATABASE — then re-applies all
/// migrations from scratch.
/// </summary>
internal static class TestDb
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PGM_STUDIO_TEST_DB")
        ?? "Server=localhost;Database=pgm_studio_test;User ID=pgm;Password=pgm_dev_pw;";

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
                foreach (var t in tables)
                    await Exec(conn, $"DROP TABLE IF EXISTS `{t}`");
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=1");
            }
        }

        SchemaMigrator.MigrateUp(ConnectionString);
    }

    public static PgmDb Connect() => new(PgmDataOptions.ForConnectionString(ConnectionString));

    private static async Task Exec(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
