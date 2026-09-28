using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Notes on a map: a thread per note, pinned to the place it is about, and the messages written in it. A note
/// names its map by slug rather than by row, because loading a board over a stored slug replaces the row and a
/// thread outlives every rebuild of the board it is about. Two new tables and a new column on
/// <c>studio_token</c> that is false for every token already issued, so no existing row changes what it may
/// do.
/// </summary>
[Migration(48, "Map notes and their threads")]
public sealed class M0048_MapNotes : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Create.Table("map_note")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_slug").AsString(190).NotNullable()
            .WithColumn("anchor_kind").AsString(16).NotNullable()
            .WithColumn("anchor_json").AsCustom(Json).NotNullable()
            .WithColumn("view_key").AsString(64).Nullable()
            .WithColumn("tag").AsString(16).Nullable()
            .WithColumn("status").AsString(16).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Index("ix_map_note_map").OnTable("map_note")
            .OnColumn("map_slug").Ascending()
            .OnColumn("status").Ascending();
        Create.Index("ix_map_note_status").OnTable("map_note")
            .OnColumn("status").Ascending()
            .OnColumn("updated_at").Descending();

        Create.Table("map_note_message")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("note_id").AsInt64().NotNullable()
                .ForeignKey("fk_map_note_message_note", "map_note", "id").OnDelete(System.Data.Rule.Cascade)
                .Indexed("ix_map_note_message_note")
            .WithColumn("author_uuid").AsString(36).Nullable()
            .WithColumn("author_name").AsString(64).NotNullable()
            .WithColumn("token_label").AsString(100).Nullable()
            .WithColumn("body").AsCustom("TEXT").NotNullable()
            .WithColumn("revision").AsInt64().NotNullable()
            .WithColumn("picture").AsFixedLengthString(64).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Alter.Table("studio_token")
            .AddColumn("notes").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("notes").FromTable("studio_token");
        Delete.Table("map_note_message");
        Delete.Table("map_note");
    }
}
