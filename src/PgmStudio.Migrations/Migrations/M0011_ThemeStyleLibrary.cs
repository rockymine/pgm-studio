using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The terrain-paint <c>theme</c> / <c>style</c> library (B44, docs/world-export/finishing-model.md §3). Themes
/// were only ever opaque JSON blobs inside a plan/intent, with every material inlined per bucket — no library to
/// browse or reuse. These tables make the two things the theme JSON already decomposes into first-class:
/// <para>A <c>style</c> is one reusable, named material recipe — its <c>kind</c> (the material discriminator:
/// solid | layered | teamTint | voronoi | noise | wallRun) and its <c>params_json</c> (one serialized
/// <c>TerrainMaterial</c>, the polymorphic subtree kept in the leaf). It is the unit an author browses ("every
/// voronoi") and reuses.</para>
/// <para>A <c>theme</c> is a composition: the geometry knobs on the row, and a <c>theme_bucket</c> binding per
/// themeable bucket (rim | surface | wall | fill) pointing at the <c>style</c> that fills it, with the bucket's
/// depth and toggle. A full theme resolves to exactly the theme JSON the painter consumes. A map's <em>applied</em>
/// theme is a snapshot copy (kept with the map, not a FK into this library), so editing a library theme never
/// silently repaints a shipped map.</para>
/// </summary>
[Migration(11, "The terrain-paint theme / style library")]
public sealed class M0011_ThemeStyleLibrary : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Create.Table("style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("kind").AsString(32).NotNullable()          // solid | layered | teamTint | voronoi | noise | wallRun
            .WithColumn("params_json").AsCustom(Json).NotNullable()  // one serialized TerrainMaterial (the leaf subtree)
            .WithColumn("created_at").AsDateTime().NotNullable();
        Execute.Sql("ALTER TABLE `style` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        Create.Table("theme")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("bedrock_relative").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("bedrock_value").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("closed").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("wall_on_terrain_faces").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("created_at").AsDateTime().NotNullable();
        Execute.Sql("ALTER TABLE `theme` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        Create.Table("theme_bucket")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("theme_id").AsInt64().NotNullable()
            .WithColumn("bucket").AsString(16).NotNullable()        // rim | surface | wall | fill
            .WithColumn("style_id").AsInt64().NotNullable()
            .WithColumn("depth").AsInt32().NotNullable().WithDefaultValue(1)   // top-band depth (rim/surface)
            .WithColumn("enabled").AsBoolean().NotNullable().WithDefaultValue(true);

        // A theme's buckets die with it; a style may not be deleted while a theme binds it (restrict, no rule).
        Create.ForeignKey("fk_theme_bucket_theme").FromTable("theme_bucket").ForeignColumn("theme_id")
              .ToTable("theme").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_theme_bucket_style").FromTable("theme_bucket").ForeignColumn("style_id")
              .ToTable("style").PrimaryColumn("id");

        Create.Index("ux_theme_bucket").OnTable("theme_bucket").OnColumn("theme_id").Ascending()
              .OnColumn("bucket").Ascending().WithOptions().Unique();
        Create.Index("ix_theme_bucket_style").OnTable("theme_bucket").OnColumn("style_id").Ascending();
        Create.Index("ix_style_kind").OnTable("style").OnColumn("kind").Ascending();
    }

    public override void Down()
    {
        Delete.Table("theme_bucket");
        Delete.Table("theme");
        Delete.Table("style");
    }
}
