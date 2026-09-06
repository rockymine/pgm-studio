using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The biome library (docs/tools/library.md, docs/world-export/terrain-painting.md §5b).
///
/// <para>A biome field is one byte per column that tints grass, leaves and water, and a map states one of
/// them. Authoring a <c>cell</c> or a <c>noise</c> field is picking a kind, a scale and a palette — the same
/// weight of decision a material recipe is, and the same thing an author wants to name once and reuse. So it
/// is a library row like a style: a <c>kind</c> and the serialized <c>BiomeField</c> under it, browsed and
/// picked rather than authored wherever it is applied.</para>
///
/// <para>A map's biome is a snapshot copy, the way its theme and its room shells are (M0011): editing a
/// library pattern must never silently retint a shipped map.</para>
/// </summary>
[Migration(32, "The biome library")]
public sealed class M0032_BiomePatternLibrary : Migration
{
    public override void Up()
    {
        Create.Table("biome_pattern")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("kind").AsString(32).NotNullable()            // solid | cell | noise
            .WithColumn("params_json").AsCustom("JSON").NotNullable() // one serialized BiomeField
            .WithColumn("created_at").AsDateTime().NotNullable();

        Execute.Sql("ALTER TABLE `biome_pattern` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
    }

    public override void Down() => Delete.Table("biome_pattern");
}
