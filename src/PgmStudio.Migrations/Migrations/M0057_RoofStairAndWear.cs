using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A roof states the stair it steps in and how worn it is, on a house and on a roof part alike. -1 is "no
/// stair" and 0 is a roof laid true, so every stored row reads as the roof it already was.
/// </summary>
[Migration(57, "A roof's stair and its wear")]
public sealed class M0057_RoofStairAndWear : Migration
{
    public override void Up()
    {
        foreach (var table in (string[])["room_style", "roof_style"])
        {
            Create.Column("roof_stair").OnTable(table).AsInt32().NotNullable().WithDefaultValue(-1);
            Create.Column("roof_wear").OnTable(table).AsDouble().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        foreach (var table in (string[])["room_style", "roof_style"])
        {
            Delete.Column("roof_wear").FromTable(table);
            Delete.Column("roof_stair").FromTable(table);
        }
    }
}
