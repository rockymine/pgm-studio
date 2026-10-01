using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A note's message records the change its map stood at when it was written, where it recorded the map row's
/// revision — a number a layout edit does not move. Every stored message is carried across as the latest change
/// of its map at or before the moment it was written, and as 0 where none had landed by then.
/// </summary>
[Migration(53, "A note's message records the change its map stood at")]
public sealed class M0053_NoteChange : Migration
{
    public override void Up()
    {
        Execute.Sql("ALTER TABLE map_note_message CHANGE COLUMN revision change_number BIGINT NOT NULL");
        Execute.Sql("""
            UPDATE map_note_message message
            JOIN map_note note ON note.id = message.note_id
            SET message.change_number = COALESCE(
                (SELECT MAX(change_row.number) FROM map_change change_row
                 WHERE change_row.map_slug = note.map_slug AND change_row.created_at <= message.created_at),
                0)
            """);
    }

    // The map row's revision a message was written against is not kept anywhere, so a message goes back as
    // written against the revision its map is at now, and one whose map is gone as 1.
    public override void Down()
    {
        Execute.Sql("ALTER TABLE map_note_message CHANGE COLUMN change_number revision BIGINT NOT NULL");
        Execute.Sql("""
            UPDATE map_note_message message
            JOIN map_note note ON note.id = message.note_id
            SET message.revision = COALESCE((SELECT map.revision FROM map WHERE map.slug = note.map_slug), 1)
            """);
    }
}
