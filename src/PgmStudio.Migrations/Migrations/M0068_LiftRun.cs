using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The lift-run store: every ladder, vine and water run a scanned world holds, as the feet heights a player
/// climbs or swims through in its column. None of it is in <c>segment</c> — a ladder is walked through, and a
/// segment does not say which of its runs is water — and the world is discarded after import, so what is not
/// written in that pass cannot be recovered. A map scanned before this store reads with no lifts until it is
/// scanned again.
///
/// <para>Same shape as <c>door_run</c>: surrogate PK, cascade-delete FK to <c>map</c>, and a <c>map_id</c>
/// index, so a re-scan is a delete-then-insert and a deleted map takes its runs with it.</para>
/// </summary>
[Migration(68, "Lift run store")]
public sealed class M0068_LiftRun : Migration
{
    public override void Up()
    {
        Create.Table("lift_run")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y_start").AsInt32().NotNullable()
            .WithColumn("world_y_end").AsInt32().NotNullable();

        Create.ForeignKey("fk_lift_run_map").FromTable("lift_run").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_lift_run_map").OnTable("lift_run").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("lift_run");
}
