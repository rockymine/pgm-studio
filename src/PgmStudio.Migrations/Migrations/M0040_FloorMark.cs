using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The floor-mark store: every y=0 block of a scanned world that no <c>segment</c> holds — the invisible
/// block-36 marker PGM removes on load, a stained-glass floor sheet. PGM's void filter counts any block at
/// (x, 0, z), so these columns may be built over although nobody stands on them. The world is discarded after
/// import, so what is not written in that pass cannot be recovered.
///
/// <para>Same shape as <c>segment</c>: surrogate PK, cascade-delete FK to <c>map</c>, and a <c>map_id</c>
/// index, so a re-scan is a delete-then-insert and a deleted map takes its marks with it.</para>
/// </summary>
[Migration(40, "Floor mark store")]
public sealed class M0040_FloorMark : Migration
{
    public override void Up()
    {
        Create.Table("floor_mark")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("block_id").AsInt32().NotNullable();

        Create.ForeignKey("fk_floor_mark_map").FromTable("floor_mark").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_floor_mark_map").OnTable("floor_mark").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("floor_mark");
}
