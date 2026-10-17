using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// How far each reader has read each note's thread: the newest message they have seen, by id, which is what
/// counts a note's messages as new to them. A reader is a person's account, the open studio's local admin, or a
/// token by its label. A new table, so no existing row changes.
/// </summary>
[Migration(80, "How far each reader has read each note")]
public sealed class M0080_NoteReads : Migration
{
    public override void Up()
    {
        Create.Table("map_note_read")
            .WithColumn("note_id").AsInt64().NotNullable().PrimaryKey()
                .ForeignKey("fk_map_note_read_note", "map_note", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("reader").AsString(140).NotNullable().PrimaryKey()
            .WithColumn("last_message_id").AsInt64().NotNullable();
    }

    public override void Down() => Delete.Table("map_note_read");
}
