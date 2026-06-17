using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Promote map symmetry from the <c>symmetry_json</c> artifact blob to a first-class table
/// (docs/contracts/new-map-authoring.md §6b). One row per map: the queried scalars (status, centre,
/// chosen mode) as columns; the irregular candidate list (<c>modes_json</c>) + authoring inputs
/// (<c>excluded_islands_json</c>, <c>detection_layer</c>) as JSON. <c>center_cell</c> and the primary
/// projection are derived on read. Mirrors the hybrid rule (columns for what we query, JSON for the
/// irregular leaf) and the <c>monument_candidate</c> cascade-FK pattern.
/// </summary>
[Migration(3, "Symmetry table")]
public sealed class M0003_Symmetry : Migration
{
    private const string Json = "JSON";   // MariaDB JSON (LONGTEXT + JSON_VALID), as in M0001

    public override void Up()
    {
        Create.Table("symmetry")
            .WithColumn("map_id").AsInt64().PrimaryKey()                          // one row per map
            .WithColumn("status").AsString(16).NotNullable()                      // unconfirmed | confirmed | none
            .WithColumn("center_x").AsDouble().Nullable()
            .WithColumn("center_z").AsDouble().Nullable()
            .WithColumn("primary_type").AsString(16).Nullable()                   // rot_90 | rot_180 | mirror_x | mirror_z
            .WithColumn("primary_confidence").AsDouble().Nullable()
            .WithColumn("primary_user_override").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("modes_json").AsCustom(Json).NotNullable()                // [{type,detected,confidence}]
            .WithColumn("excluded_islands_json").AsCustom(Json).Nullable()        // §6b authoring input
            .WithColumn("detection_layer").AsString(16).Nullable()               // §6b: cleanbase | bedrock | y0
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.ForeignKey("fk_symmetry_map").FromTable("symmetry").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down() => Delete.Table("symmetry");
}
