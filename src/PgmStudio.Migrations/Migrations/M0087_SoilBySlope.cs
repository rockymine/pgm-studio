using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Carry a theme bucket's soil by slope on <c>theme_bucket</c>: the surface's depth by how steep the ground is, as
/// JSON <c>[{"below": 25, "depth": 3}, …]</c>. NULL is a bucket that takes its one <c>depth</c> everywhere, which is
/// what every stored row says, so there is nothing to carry forward.
/// </summary>
[Migration(87, "A theme's surface thins with the slope")]
public sealed class M0087_SoilBySlope : Migration
{
    public override void Up() => Alter.Table("theme_bucket").AddColumn("soil").AsString(1024).Nullable();

    public override void Down() => Delete.Column("soil").FromTable("theme_bucket");
}
