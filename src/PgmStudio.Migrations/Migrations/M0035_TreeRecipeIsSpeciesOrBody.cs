using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A tree recipe is a species or a body, and nothing else.
///
/// <para>A template tree is named by its species and scaled by its height; a copied tree is the blocks it
/// carries. Neither reads a wood or a growth knob, so the eight columns that held them leave the table, and a
/// row that states a form no builder answers to leaves with them — a recipe whose shape cannot be built has no
/// second reading as one that can, and a map's own dressing registry holds its copy of every recipe it
/// placed.</para>
/// </summary>
[Migration(35, "A tree recipe is a species or a body")]
public sealed class M0035_TreeRecipeIsSpeciesOrBody : Migration
{
    private static readonly string[] GrownColumns =
        ["wood", "stems", "leader", "flow", "branch_angle", "levels", "whorled", "leaf_size"];

    public override void Up()
    {
        Execute.Sql("DELETE FROM tree_style WHERE form = 'grown'");
        foreach (var column in GrownColumns) Delete.Column(column).FromTable("tree_style");
    }

    /// <summary>The columns come back at their own defaults. The rows do not: what they described is not a
    /// tree this schema can hold.</summary>
    public override void Down()
    {
        Alter.Table("tree_style").AddColumn("wood").AsString(32).NotNullable().WithDefaultValue("oak");
        Alter.Table("tree_style").AddColumn("stems").AsInt32().NotNullable().WithDefaultValue(1);
        Alter.Table("tree_style").AddColumn("leader").AsDouble().NotNullable().WithDefaultValue(0.55);
        Alter.Table("tree_style").AddColumn("flow").AsDouble().NotNullable().WithDefaultValue(0.45);
        Alter.Table("tree_style").AddColumn("branch_angle").AsDouble().NotNullable().WithDefaultValue(1.1);
        Alter.Table("tree_style").AddColumn("levels").AsInt32().NotNullable().WithDefaultValue(2);
        Alter.Table("tree_style").AddColumn("whorled").AsBoolean().NotNullable().WithDefaultValue(false);
        Alter.Table("tree_style").AddColumn("leaf_size").AsDouble().NotNullable().WithDefaultValue(0.6);
    }
}
