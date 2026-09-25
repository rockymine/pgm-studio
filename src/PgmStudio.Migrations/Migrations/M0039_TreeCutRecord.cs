using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A copied tree records where it was cut: the world directory, the foot's world coordinates, and when.
///
/// <para>The cutter writes all five and nothing else writes any, so a <c>copied</c> row is one taken out of a
/// world rather than a block list typed into a request. The columns are nullable because a template carries
/// none of them; a row already in the table keeps its body and loads as before, and states no cut until the
/// cutter files it again.</para>
/// </summary>
[Migration(39, "A copied tree records where it was cut")]
public sealed class M0039_TreeCutRecord : Migration
{
    public override void Up()
    {
        Alter.Table("tree_style")
            .AddColumn("cut_world").AsString(1024).Nullable()
            .AddColumn("cut_x").AsInt32().Nullable()
            .AddColumn("cut_y").AsInt32().Nullable()
            .AddColumn("cut_z").AsInt32().Nullable()
            .AddColumn("cut_at").AsDateTime().Nullable();
    }

    public override void Down()
    {
        foreach (var column in new[] { "cut_world", "cut_x", "cut_y", "cut_z", "cut_at" })
            Delete.Column(column).FromTable("tree_style");
    }
}
