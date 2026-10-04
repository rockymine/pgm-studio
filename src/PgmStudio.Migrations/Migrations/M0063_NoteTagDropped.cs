using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A note carries no tag. What a note is about is the agent's to read off its text and its mark, since one
/// message speaks to the ground, the look and the play at once, and the agent's reply says how it read it. The
/// author ruled that the stored tags go with the column.
/// </summary>
[Migration(63, "A note carries no tag")]
public sealed class M0063_NoteTagDropped : Migration
{
    public override void Up() => Delete.Column("tag").FromTable("map_note");

    public override void Down() => Alter.Table("map_note").AddColumn("tag").AsString(16).Nullable();
}
