using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The door-run store: every run of door blocks a scanned world stands on solid ground — a doorway's glass, a
/// wool room's pane wall, a nether-brick-fence gate. Those runs stay in <c>segment</c>, because they are solid;
/// a walk that knows the map lets players break blocks in their column opens them. The world is discarded
/// after import, so what is not written in that pass cannot be recovered.
///
/// <para>Same shape as <c>segment</c>: surrogate PK, cascade-delete FK to <c>map</c>, and a <c>map_id</c>
/// index, so a re-scan is a delete-then-insert and a deleted map takes its runs with it.</para>
/// </summary>
[Migration(41, "Door run store")]
public sealed class M0041_DoorRun : Migration
{
    public override void Up()
    {
        Create.Table("door_run")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y_start").AsInt32().NotNullable()
            .WithColumn("world_y_end").AsInt32().NotNullable();

        Create.ForeignKey("fk_door_run_map").FromTable("door_run").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_door_run_map").OnTable("door_run").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("door_run");
}
