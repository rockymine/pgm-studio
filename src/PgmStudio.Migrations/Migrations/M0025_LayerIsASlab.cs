using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Frees the word <c>layer</c> in the scan family, where it was borrowed, so it names a slab of a sketch and
/// nothing else.
///
/// <para>Three stored names carried it. The <c>layer_segment</c> table holds a scanned world's solid runs,
/// one row per contiguous span in a column — the scan's own word is <em>segment</em> and only the prefix was
/// wrong, so it is <c>scan_segment</c>. The <c>layer_parquet</c> artifact kind is the surface scan, so it is
/// <c>surface_parquet</c>. And the <c>scan_layer</c> key inside <c>map_config_json</c> names which reading
/// the scan takes the ground off — <c>surface</c> or <c>cleanbase</c> — so it is <c>scan_read</c>, with
/// <c>scan_layer_confirmed</c> following it.</para>
///
/// <para>The two JSON updates are guarded by <c>JSON_VALID</c> rather than left to fail on one bad blob and
/// take the migration with it, and each moves a key only where the new one is not already there.</para>
/// </summary>
[Migration(25, "Free the word layer in the scan family: table, artifact kind, and the scan-read key")]
public sealed class M0025_LayerIsASlab : Migration
{
    public override void Up()
    {
        Rename.Table("layer_segment").To("scan_segment");

        Execute.Sql("UPDATE map_artifact SET kind = 'surface_parquet' WHERE kind = 'layer_parquet';");

        Execute.Sql(
            """
            UPDATE map_artifact
               SET data = CAST(
                     JSON_REMOVE(
                       JSON_SET(CAST(data AS CHAR),
                                '$.scan_read', JSON_EXTRACT(CAST(data AS CHAR), '$.scan_layer')),
                       '$.scan_layer')
                   AS BINARY)
             WHERE kind = 'map_config_json'
               AND JSON_VALID(CAST(data AS CHAR))
               AND JSON_EXTRACT(CAST(data AS CHAR), '$.scan_layer') IS NOT NULL
               AND JSON_EXTRACT(CAST(data AS CHAR), '$.scan_read') IS NULL;
            """);

        Execute.Sql(
            """
            UPDATE map_artifact
               SET data = CAST(
                     JSON_REMOVE(
                       JSON_SET(CAST(data AS CHAR),
                                '$.scan_read_confirmed',
                                JSON_EXTRACT(CAST(data AS CHAR), '$.scan_layer_confirmed')),
                       '$.scan_layer_confirmed')
                   AS BINARY)
             WHERE kind = 'map_config_json'
               AND JSON_VALID(CAST(data AS CHAR))
               AND JSON_EXTRACT(CAST(data AS CHAR), '$.scan_layer_confirmed') IS NOT NULL
               AND JSON_EXTRACT(CAST(data AS CHAR), '$.scan_read_confirmed') IS NULL;
            """);
    }

    /// <summary>The same three names back the way they were. Every one is a rename, so nothing is lost by
    /// going either direction.</summary>
    public override void Down()
    {
        Rename.Table("scan_segment").To("layer_segment");

        Execute.Sql("UPDATE map_artifact SET kind = 'layer_parquet' WHERE kind = 'surface_parquet';");

        Execute.Sql(
            """
            UPDATE map_artifact
               SET data = CAST(
                     JSON_REMOVE(
                       JSON_SET(CAST(data AS CHAR),
                                '$.scan_layer', JSON_EXTRACT(CAST(data AS CHAR), '$.scan_read')),
                       '$.scan_read')
                   AS BINARY)
             WHERE kind = 'map_config_json'
               AND JSON_VALID(CAST(data AS CHAR))
               AND JSON_EXTRACT(CAST(data AS CHAR), '$.scan_read') IS NOT NULL;
            """);

        Execute.Sql(
            """
            UPDATE map_artifact
               SET data = CAST(
                     JSON_REMOVE(
                       JSON_SET(CAST(data AS CHAR),
                                '$.scan_layer_confirmed',
                                JSON_EXTRACT(CAST(data AS CHAR), '$.scan_read_confirmed')),
                       '$.scan_read_confirmed')
                   AS BINARY)
             WHERE kind = 'map_config_json'
               AND JSON_VALID(CAST(data AS CHAR))
               AND JSON_EXTRACT(CAST(data AS CHAR), '$.scan_read_confirmed') IS NOT NULL;
            """);
    }
}
