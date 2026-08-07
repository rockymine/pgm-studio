using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Two changes to the theme library (M0011), both widening a choice that was stored as a narrower one.
/// <para>The rim's <c>closed</c> flag becomes <c>rim_edges</c>, a three-value word. A boolean could say "cap
/// every drop" or "cap every plateau boundary" and had no way to say "cap only the void", which is what a
/// staircase of stacked plateaus wants: one rim around the outside of the body rather than a lip on every
/// tread. The old flag maps straight across — <c>closed</c> is <c>boundary</c>, everything else is
/// <c>drop</c> — so no theme changes what it paints.</para>
/// <para><c>theme_bucket.style_id</c> becomes nullable. A theme needs neither a rim nor a wall — fill and
/// surface are a complete finish — but "off" could only be stored on a row that also named a style, so
/// switching the rim off meant first binding it a material nothing would paint. A binding with no style
/// carries its depth and its toggle alone, and the bucket keeps the built-in material.</para>
/// </summary>
[Migration(13, "Three-value rim edges, and bucket bindings that need no style")]
public sealed class M0013_RimEdgesAndUnboundBuckets : Migration
{
    public override void Up()
    {
        Alter.Table("theme")
            .AddColumn("rim_edges").AsString(16).NotNullable().WithDefaultValue("drop");   // void | drop | boundary
        Execute.Sql("UPDATE `theme` SET `rim_edges` = 'boundary' WHERE `closed` = 1");
        Delete.Column("closed").FromTable("theme");

        // The foreign key is dropped and remade around the type change: MariaDB will not alter a column a
        // constraint references. Nullable keeps the same restrict rule — a style still may not be deleted
        // while a theme binds it; a binding that names none simply has nothing to restrict.
        Delete.ForeignKey("fk_theme_bucket_style").OnTable("theme_bucket");
        Alter.Table("theme_bucket").AlterColumn("style_id").AsInt64().Nullable();
        Create.ForeignKey("fk_theme_bucket_style").FromTable("theme_bucket").ForeignColumn("style_id")
              .ToTable("style").PrimaryColumn("id");
    }

    public override void Down()
    {
        // A styleless binding has no style to give back, so it goes: without one the row can only say what it
        // already said by being absent.
        Execute.Sql("DELETE FROM `theme_bucket` WHERE `style_id` IS NULL");
        Delete.ForeignKey("fk_theme_bucket_style").OnTable("theme_bucket");
        Alter.Table("theme_bucket").AlterColumn("style_id").AsInt64().NotNullable();
        Create.ForeignKey("fk_theme_bucket_style").FromTable("theme_bucket").ForeignColumn("style_id")
              .ToTable("style").PrimaryColumn("id");

        Alter.Table("theme").AddColumn("closed").AsBoolean().NotNullable().WithDefaultValue(false);
        Execute.Sql("UPDATE `theme` SET `closed` = 1 WHERE `rim_edges` = 'boundary'");
        Delete.Column("rim_edges").FromTable("theme");
    }
}
