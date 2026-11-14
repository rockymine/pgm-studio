using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A room style keeps the sample shell its card is drawn on, which its editor's dock chooses. Every stored row
/// is drawn on <c>8x8</c>, so that is what each row states.
/// </summary>
[Migration(99, "A room style keeps the shell its card is drawn on")]
public sealed class M0099_HouseCardFootprint : Migration
{
    public override void Up()
        => Alter.Table("room_style").AddColumn("footprint").AsString(8).NotNullable().WithDefaultValue("8x8");

    public override void Down() => Delete.Column("footprint").FromTable("room_style");
}
