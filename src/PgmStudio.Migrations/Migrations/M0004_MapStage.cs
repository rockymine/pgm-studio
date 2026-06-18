using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Add the lifecycle <c>stage</c> column to <c>map</c> (sketch | configure | edit) so the dashboard can
/// list each stage on its own and a user can resume a draft. New rows are seeded by the originating
/// endpoints (sketch-create → sketch, import → configure); existing rows are backfilled from what they
/// already have: a sketch layout with no rasterized geometry is a draft; an intent-authored map, or a
/// scanned world with geometry but no regions yet, is being configured; everything else (a parsed
/// <c>map.xml</c>) is editable.
/// </summary>
[Migration(4, "Map lifecycle stage")]
public sealed class M0004_MapStage : Migration
{
    public override void Up()
    {
        Create.Column("stage").OnTable("map").AsString(16).NotNullable().WithDefaultValue("edit");

        // Configure: intent-authored, or a scanned/finished world with geometry but no regions yet.
        Execute.Sql("""
            UPDATE map m SET stage = 'configure'
            WHERE EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id AND a.kind = 'map_intent_json')
               OR (EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id
                                   AND a.kind IN ('layer_parquet', 'islands_json'))
                   AND NOT EXISTS (SELECT 1 FROM region r WHERE r.map_id = m.id));
            """);

        // Sketch: a drawn layout that hasn't been rasterized into world geometry yet (most specific — last).
        Execute.Sql("""
            UPDATE map m SET stage = 'sketch'
            WHERE EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id AND a.kind = 'sketch_layout_json')
              AND NOT EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id AND a.kind = 'layer_parquet');
            """);
    }

    public override void Down() => Delete.Column("stage").FromTable("map");
}
