using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A copied tree carries its blocks in its own row.
///
/// <para>A tree recipe was two built forms, each described by its numbers. The third form is a tree an author
/// built by hand and cut out of a world, and what describes it is the blocks themselves, so the row gains the
/// one column that holds them: the body as JSON, empty on the two forms that build their own.</para>
/// </summary>
[Migration(31, "A copied tree recipe carries its blocks")]
public sealed class M0031_CopiedTreeBody : Migration
{
    public override void Up()
    {
        Alter.Table("tree_style").AddColumn("body").AsCustom("LONGTEXT").NotNullable().WithDefaultValue("");
    }

    public override void Down()
    {
        Delete.Column("body").FromTable("tree_style");
    }
}
