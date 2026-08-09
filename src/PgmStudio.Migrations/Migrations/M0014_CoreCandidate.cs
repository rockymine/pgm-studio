using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The core-candidate store: the output of <c>CoreSuggester.Gather</c>, persisted so the configure tool can
/// offer DTC suggestions as a DB query rather than by re-reading the world. It cannot re-read it — the
/// <c>.mca</c> files are discarded after import — so a candidate not written during that single pass is gone.
///
/// <para>One row per proposed core, which is a handful per map rather than the dozens
/// <c>monument_candidate</c> holds: a core's signature is unambiguous enough to describe the structure
/// outright instead of storing evidence for a later scoring pass. Every column is a measured property of the
/// casing, so a confirmed suggestion carries its own parameters into the intent.</para>
///
/// <para>Same shape as the other feature tables: surrogate PK, cascade-delete FK to <c>map</c>, and a
/// <c>map_id</c> index, so a re-scan is a delete-then-insert and a deleted map takes its candidates with
/// it.</para>
/// </summary>
[Migration(14, "Core candidate store")]
public sealed class M0014_CoreCandidate : Migration
{
    public override void Up()
    {
        Create.Table("core_candidate")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            // the obsidian casing's block box — the structure, never a region drawn around it (OB12)
            .WithColumn("min_x").AsInt32().NotNullable()
            .WithColumn("min_y").AsInt32().NotNullable()
            .WithColumn("min_z").AsInt32().NotNullable()
            .WithColumn("max_x").AsInt32().NotNullable()
            .WithColumn("max_y").AsInt32().NotNullable()
            .WithColumn("max_z").AsInt32().NotNullable()
            // measured parameters: what the casing holds and how it is built
            .WithColumn("lava_blocks").AsInt32().NotNullable()
            .WithColumn("shell").AsInt32().NotNullable()
            .WithColumn("float_blocks").AsInt32().NotNullable()
            .WithColumn("open_top").AsBoolean().NotNullable();

        Create.ForeignKey("fk_core_candidate_map").FromTable("core_candidate").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_core_candidate_map").OnTable("core_candidate").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("core_candidate");
}
