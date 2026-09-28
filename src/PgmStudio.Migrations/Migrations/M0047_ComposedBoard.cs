using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The composed-board library the Generator page browses: one row per board the composer made for a size band,
/// a symmetry and a seed, under one composer version, with its plan, its score and structure as columns the
/// feed filters and orders on, and the rest of its card. A new table, so no row is carried forward.
/// </summary>
[Migration(47, "The composed-board library")]
public sealed class M0047_ComposedBoard : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Create.Table("composed_board")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("composer_version").AsString(32).NotNullable()
            .WithColumn("band").AsString(8).NotNullable()
            .WithColumn("symmetry").AsString(16).NotNullable()
            .WithColumn("cell").AsInt32().NotNullable()
            .WithColumn("seed").AsCustom("BIGINT UNSIGNED").NotNullable()
            .WithColumn("score").AsDouble().NotNullable()
            .WithColumn("wool_count").AsInt32().NotNullable()
            .WithColumn("wools").AsString(64).NotNullable()
            .WithColumn("hub").AsString(16).NotNullable()
            .WithColumn("frontline").AsString(16).NotNullable()
            .WithColumn("card_json").AsCustom(Json).NotNullable()
            .WithColumn("plan_json").AsCustom(Json).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("ux_composed_board_key").OnTable("composed_board")
            .OnColumn("composer_version").Ascending()
            .OnColumn("band").Ascending()
            .OnColumn("symmetry").Ascending()
            .OnColumn("cell").Ascending()
            .OnColumn("seed").Ascending()
            .WithOptions().Unique();

        Create.Index("ix_composed_board_feed").OnTable("composed_board")
            .OnColumn("composer_version").Ascending()
            .OnColumn("band").Ascending()
            .OnColumn("symmetry").Ascending()
            .OnColumn("score").Ascending()
            .OnColumn("seed").Ascending();
    }

    public override void Down() => Delete.Table("composed_board");
}
