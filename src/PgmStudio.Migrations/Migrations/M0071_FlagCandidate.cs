using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The flag-candidate store: every banner a scanned world holds, as the place a flag could stand. PGM loads a
/// flag only where a banner stands at its starting post, so a banner is the suggestion itself — where it is,
/// whether it hangs on a wall, which way it faces, its base dye and its patterns. The world is discarded after
/// import, so what is not written in that pass cannot be recovered; a map scanned before this store has none
/// until it is scanned again.
///
/// <para>Same shape as <c>core_candidate</c>: surrogate PK, cascade-delete FK to <c>map</c>, and a
/// <c>map_id</c> index, so a re-scan is a delete-then-insert and a deleted map takes its rows with it.</para>
/// </summary>
[Migration(71, "Flag candidate store")]
public sealed class M0071_FlagCandidate : Migration
{
    public override void Up()
    {
        Create.Table("flag_candidate")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_y").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("wall").AsBoolean().NotNullable()
            .WithColumn("facing").AsInt32().NotNullable()
            .WithColumn("color").AsString(32).NotNullable()
            .WithColumn("patterns_json").AsCustom("TEXT").NotNullable();

        Create.ForeignKey("fk_flag_candidate_map").FromTable("flag_candidate").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_flag_candidate_map").OnTable("flag_candidate").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("flag_candidate");
}
