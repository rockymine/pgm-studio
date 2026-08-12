using MySqlConnector;
using PgmStudio.Migrations;

namespace PgmStudio.Tests;

/// <summary>
/// The database state a test starts from: the migrated schema with nothing in it.
///
/// <para>It is reached by <b>emptying the tables</b> rather than by dropping and re-migrating them. A test
/// cannot tell the two apart — either way it looks at the schema this build defines and no rows — and they
/// do not cost the same. Re-migrating writes the whole schema as DDL, and InnoDB gives every table its own
/// file to create and flush, so one reset costs about seven hundred milliseconds; a suite that resets per
/// test spends most of its life there, and spends more of it the slower the disk underneath is. Emptying the
/// same tables is one round trip of DML over rows that have already gone, and costs about two
/// milliseconds.</para>
///
/// <para>The migrations still have to run <b>once</b>, and a schema that has been damaged on purpose has to
/// be noticed, so <see cref="ResetAsync"/> reads the applied version before it empties anything and rebuilds
/// where that is not the newest version this build knows. One check covers all three cases it needs to: a
/// database that has never been migrated, one left behind by an older build, and the migration tests
/// themselves, which delete a version row to prove the startup guard catches a database that is behind.</para>
///
/// <para>Shared by the two test projects that touch a database rather than written out in each, because what
/// they would be duplicating is not the schema shape but the <em>meaning</em> of a reset — and two copies of
/// that is how they come to disagree about what a test starts from.</para>
/// </summary>
internal static class TestSchema
{
    /// <summary>The tables to empty, read once and kept: the schema does not move under a suite, and a
    /// rebuild is the one thing that can change the list, so it is what clears this. <c>VersionInfo</c> is
    /// not among them — it is the migration ledger rather than test data, and emptying it would leave the
    /// next reset looking at a database that had never been migrated.</summary>
    private static string[]? cachedTables;

    /// <summary>Empty every table, having first made sure the schema is the one this build defines.</summary>
    public static async Task ResetAsync(string connectionString)
    {
        if (await IsBehindAsync(connectionString))
        {
            await RebuildAsync(connectionString);
            return;
        }

        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        cachedTables ??= await TablesAsync(conn, includeVersionInfo: false);
        if (cachedTables.Length == 0) return;

        // One round trip: the foreign keys off, a delete per table, the foreign keys back on. They are
        // turned off rather than the deletes being ordered parent-last, since an order that is right today
        // is one migration away from being wrong and nothing would report it but a failing unrelated test.
        await ExecuteAsync(conn,
            "SET FOREIGN_KEY_CHECKS=0; "
            + string.Concat(cachedTables.Select(table => $"DELETE FROM `{table}`; "))
            + "SET FOREIGN_KEY_CHECKS=1");
    }

    /// <summary>Drop every table and re-apply every migration. What a test asks for when the migrating
    /// itself is the thing under test, and what <see cref="ResetAsync"/> falls back to when it finds a
    /// schema that is not the one this build defines.</summary>
    public static async Task RebuildAsync(string connectionString)
    {
        await using (var conn = new MySqlConnection(connectionString))
        {
            await conn.OpenAsync();
            // The ledger goes with the rest: a rebuild is a database that has never been migrated, and one
            // that kept its version rows would have the runner skip every migration it claims were applied.
            var tables = await TablesAsync(conn, includeVersionInfo: true);
            if (tables.Length > 0)
            {
                await ExecuteAsync(conn, "SET FOREIGN_KEY_CHECKS=0");
                foreach (var table in tables) await ExecuteAsync(conn, $"DROP TABLE IF EXISTS `{table}`");
                await ExecuteAsync(conn, "SET FOREIGN_KEY_CHECKS=1");
            }
        }

        SchemaMigrator.MigrateUp(connectionString);
        cachedTables = null;
    }

    /// <summary>Run one statement against the schema — for arranging a row state no endpoint produces.</summary>
    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await ExecuteAsync(conn, sql);
    }

    /// <summary>Whether the schema is behind the migrations this build defines — including the case where it
    /// has no ledger at all, which is what a database nobody has migrated looks like. Asked of the ledger
    /// directly rather than through the migration runner, because it is asked before every reset and the
    /// runner builds a container and scans an assembly to answer it.</summary>
    private static async Task<bool> IsBehindAsync(string connectionString)
    {
        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT MAX(Version) FROM VersionInfo", conn);
            return await cmd.ExecuteScalarAsync() is not long applied
                   || applied != SchemaMigrator.LatestKnownVersion();
        }
        // A missing ledger is the answer to the question — that database has never been migrated. Anything
        // else is the server saying it cannot serve, and rebuilding on it would report a failure to connect
        // as a failure to migrate, which is a long way from where the fault is.
        catch (MySqlException missing) when (missing.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            return true;
        }
    }

    private static async Task<string[]> TablesAsync(MySqlConnection conn, bool includeVersionInfo)
    {
        var tables = new List<string>();
        await using var cmd = new MySqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            if (includeVersionInfo || table != "VersionInfo") tables.Add(table);
        }
        return [.. tables];
    }

    private static async Task ExecuteAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
