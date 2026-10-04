using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A reply in a note's thread can carry a mark of its own on the picture in view — a point, a box or a lasso and
/// the ground it was projected onto, in the shape a note's anchor is kept in. Null for every stored message, which
/// carried none.
/// </summary>
[Migration(62, "A note's reply keeps the mark it was written with")]
public sealed class M0062_NoteMessageMark : Migration
{
    public override void Up() => Alter.Table("map_note_message").AddColumn("mark_json").AsCustom("JSON").Nullable();

    public override void Down() => Delete.Column("mark_json").FromTable("map_note_message");
}
