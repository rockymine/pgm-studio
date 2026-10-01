using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A porch that names no canopy wears a gable, so the two columns a porch's form is stored in default to one.
/// The rows themselves are left as they are: a shed stored on a porch is complained of where its style is
/// checked (<c>HS14</c>), and a house keeps the porch it was saved with.
/// </summary>
[Migration(56, "A porch's canopy defaults to a gable")]
public sealed class M0056_PorchRoofDefaultsToGable : Migration
{
    public override void Up()
    {
        Alter.Table("room_style").AlterColumn("porch_roof").AsString(16).NotNullable().WithDefaultValue("gable");
        Alter.Table("porch_style").AlterColumn("roof_form").AsString(16).NotNullable().WithDefaultValue("gable");
    }

    public override void Down()
    {
        Alter.Table("room_style").AlterColumn("porch_roof").AsString(16).NotNullable().WithDefaultValue("shed");
        Alter.Table("porch_style").AlterColumn("roof_form").AsString(16).NotNullable().WithDefaultValue("shed");
    }
}
