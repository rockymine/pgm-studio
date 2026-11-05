using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A copied tree states no species: it is made of the blocks it was cut with, and a species on it names a tree
/// it is not. The column takes a null, and every copied row's species becomes one; a template keeps the species it
/// grows as. Forward-only.
/// </summary>
[Migration(92, "A copied tree states no species")]
public sealed class M0092_CopiedTreesStateNoSpecies : Migration
{
    public override void Up()
    {
        Alter.Table("tree_style").AlterColumn("species").AsString(32).Nullable();
        Execute.Sql("UPDATE tree_style SET species = NULL WHERE form = 'copied'");
    }

    public override void Down() { }
}
