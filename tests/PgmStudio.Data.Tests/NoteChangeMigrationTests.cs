using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0053</c> carries every stored note message across from the map row's revision it recorded to the change
/// its map stood at: the latest change at or before the moment the message was written, and 0 where none had
/// landed by then. Rolls the schema back one migration and forward again, so it runs apart from every other
/// database test.
/// </summary>
[NotInParallel]
public sealed class NoteChangeMigrationTests
{
    [Test]
    public async Task A_message_is_carried_to_the_change_its_map_stood_at_and_rolls_back()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 52);
        try
        {
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO map (slug, name, stage, created_at, updated_at, revision) "
                           + "VALUES ('weir', 'Weir', 'configure', NOW(), NOW(), 6)");
                db.Execute("INSERT INTO map_change (map_slug, number, created_at) VALUES "
                           + "('weir', 4, '2026-05-01 10:00:00'), ('weir', 7, '2026-05-03 10:00:00')");
                db.Execute("INSERT INTO map_note (map_slug, anchor_kind, anchor_json, status, created_at, updated_at) "
                           + "VALUES ('weir', 'map', '{}', 'open', '2026-04-30 09:00:00', '2026-05-04 09:00:00')");
                var note = db.Execute<long>("SELECT id FROM map_note WHERE map_slug = 'weir'");
                foreach (var (at, revision) in new[]
                         {
                             ("2026-04-30 09:00:00", 2), ("2026-05-02 09:00:00", 5), ("2026-05-04 09:00:00", 6),
                         })
                    db.Execute("INSERT INTO map_note_message (note_id, author_name, body, revision, created_at) "
                               + $"VALUES ({note}, 'local', 'x', {revision}, '{at}')");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
            await using (var db = TestDb.Connect())
                await Assert.That(Numbers(db, "change_number")).IsEquivalentTo([0L, 4L, 7L])
                    .Because("each message stood at the latest change before it, and the first before any");

            SchemaMigrator.MigrateDown(TestDb.ConnectionString, 52);
            await using (var db = TestDb.Connect())
                await Assert.That(Numbers(db, "revision")).IsEquivalentTo([6L, 6L, 6L])
                    .Because("rolling back gives every message the revision its map is at");
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private static List<long> Numbers(DataConnection db, string column) =>
        [.. db.Query<long>($"SELECT {column} FROM map_note_message ORDER BY created_at")];
}
