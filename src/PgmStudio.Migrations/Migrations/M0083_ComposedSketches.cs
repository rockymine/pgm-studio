using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A composed board is stored as the sketch it is handed over as: its layout, its intent and the picture of its
/// pieces, in place of its plan. The library is the composer's output under settings it states,
/// so its rows are emptied rather than carried and the fill composes them again in the new shape; rolling back
/// empties them the same way.
/// </summary>
[Migration(83, "A composed board is stored as a sketch")]
public sealed class M0083_ComposedSketches : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Delete.FromTable("composed_board").AllRows();
        Delete.Column("plan_json").FromTable("composed_board");
        Alter.Table("composed_board")
            .AddColumn("layout_json").AsCustom(Json).NotNullable()
            .AddColumn("intent_json").AsCustom(Json).NotNullable()
            .AddColumn("svg").AsCustom("MEDIUMTEXT").NotNullable();
    }

    public override void Down()
    {
        Delete.FromTable("composed_board").AllRows();
        Delete.Column("layout_json").Column("intent_json").Column("svg").FromTable("composed_board");
        Alter.Table("composed_board").AddColumn("plan_json").AsCustom(Json).NotNullable();
    }
}
