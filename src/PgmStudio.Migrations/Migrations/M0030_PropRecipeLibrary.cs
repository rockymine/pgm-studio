using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A tree and a boulder are recipes the library holds, not knobs on every placement.
///
/// <para>A board carries hundreds of trees over a few dozen recipes, so what was stored was the same answer
/// written out hundreds of times — and an author could not retune a stand of trees without editing every tree
/// in it. A placement now names a recipe, and these are the rows a recipe is authored in and pulled from.</para>
///
/// <para>A boulder's <c>rock</c> is a terrain material's own JSON, the way every other material in the library
/// is stored, so an erratic may be cut from any of the fourteen kinds rather than from a block id.</para>
/// </summary>
[Migration(30, "Tree and boulder recipes are library rows")]
public sealed class M0030_PropRecipeLibrary : Migration
{
    public override void Up()
    {
        Create.Table("tree_style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(120).NotNullable()
            .WithColumn("form").AsString(16).NotNullable().WithDefaultValue("template")
            .WithColumn("species").AsString(32).NotNullable().WithDefaultValue("oak")
            .WithColumn("wood").AsString(32).NotNullable().WithDefaultValue("oak")
            .WithColumn("height").AsDouble().NotNullable().WithDefaultValue(12)
            .WithColumn("stems").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("leader").AsDouble().NotNullable().WithDefaultValue(0.55)
            .WithColumn("flow").AsDouble().NotNullable().WithDefaultValue(0.45)
            .WithColumn("branch_angle").AsDouble().NotNullable().WithDefaultValue(1.1)
            .WithColumn("levels").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("whorled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("leaf_size").AsDouble().NotNullable().WithDefaultValue(0.6)
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("boulder_style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(120).NotNullable()
            .WithColumn("form").AsString(16).NotNullable().WithDefaultValue("round")
            .WithColumn("size").AsDouble().NotNullable().WithDefaultValue(4)
            .WithColumn("mossy").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("rock").AsCustom("TEXT").NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Execute.Sql("ALTER TABLE tree_style CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        Execute.Sql("ALTER TABLE boulder_style CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
    }

    public override void Down()
    {
        Delete.Table("boulder_style");
        Delete.Table("tree_style");
    }
}
