using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A room style keeps the wall its doorway is cut through. <c>front</c> is the building's own, read off its
/// footprint, and is what every stored row was: a house style could state another, and the row had nowhere to
/// keep it.
/// </summary>
[Migration(59, "A room style keeps the wall its doorway faces")]
public sealed class M0059_RoomStyleFront : Migration
{
    public override void Up()
        => Alter.Table("room_style").AddColumn("front").AsString(8).NotNullable().WithDefaultValue("front");

    public override void Down() => Delete.Column("front").FromTable("room_style");
}
