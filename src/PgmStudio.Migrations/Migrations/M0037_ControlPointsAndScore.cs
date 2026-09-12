using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The CP/KotH objective and the <c>&lt;score&gt;</c> module it pays into, beside <c>destroyable</c> and
/// <c>core</c> and under the same rules: hang off <c>map_id</c> with a cascade delete, real columns for
/// everything listed or edited, a unique key per map.
///
/// <para><b>Almost every column is nullable, and that is the point.</b> PGM applies a different default to
/// an unwritten attribute depending on which element the point was spelled as — a hill keeps partial
/// capture progress and a control point discards it, from the same unwritten <c>incremental</c> — so
/// storing the default a point "would have" is storing a different map. NULL means the author stated
/// nothing, and <c>element</c> is what says how PGM will read that.</para>
///
/// <para><c>map_score</c> exists per map rather than as columns on <c>map</c> because its absence is
/// meaningful: no row is a map PGM loads no score module for, and on such a map a control point's
/// <c>points</c> rate pays nothing at all. A row with every column NULL is a different map from no row.</para>
/// </summary>
[Migration(37, "Control-point objective and the score module")]
public sealed class M0037_ControlPointsAndScore : Migration
{
    public override void Up()
    {
        Create.Table("control_point")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("control_point_key").AsString(190).NotNullable()  // XML id, generated when unauthored
            .WithColumn("name").AsString(255).Nullable()                  // NULL = PGM auto-names "Hill", "Hill 2", …
            // which of PGM's two spellings the point was written as: control-points | king. Not decoration —
            // it selects the defaults for every column left NULL below.
            .WithColumn("element").AsString(32).NotNullable()
            // the region players stand in (PGM requires it), and the two cosmetic display regions
            .WithColumn("capture_region_key").AsString(190).Nullable()
            .WithColumn("progress_region_key").AsString(190).Nullable()
            .WithColumn("owner_region_key").AsString(190).Nullable()
            .WithColumn("visual_materials_key").AsString(190).Nullable() // NULL = every colour-affected material
            .WithColumn("initial_owner").AsString(190).Nullable()
            .WithColumn("capture_time").AsString(64).Nullable()          // a duration; NULL = PGM's 30s
            .WithColumn("capture_rule").AsString(32).Nullable()          // exclusive | majority | lead
            .WithColumn("capture_filter_key").AsString(190).Nullable()
            .WithColumn("player_filter_key").AsString(190).Nullable()
            // the capture state machine's four rates, plus the shorthand PGM refuses to combine with them
            .WithColumn("incremental").AsBoolean().Nullable()
            .WithColumn("recovery").AsDouble().Nullable()
            .WithColumn("decay").AsDouble().Nullable()
            .WithColumn("owned_decay").AsDouble().Nullable()
            .WithColumn("contested").AsDouble().Nullable()
            .WithColumn("time_multiplier").AsDouble().Nullable()
            .WithColumn("neutral_state").AsBoolean().Nullable()
            .WithColumn("permanent").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("points").AsDouble().Nullable()
            .WithColumn("owner_points").AsDouble().Nullable()
            .WithColumn("points_growth").AsDouble().Nullable()
            .WithColumn("show_progress").AsBoolean().Nullable()
            // NULL is NOT "false": PGM defaults required to true at every proto this studio reads, so a
            // point with nothing here ends the match for whoever captures it.
            .WithColumn("required").AsBoolean().Nullable()
            .WithColumn("show").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.Table("map_score")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("initial").AsInt32().Nullable()
            .WithColumn("score_limit").AsInt32().Nullable()              // `limit` is reserved in MySQL
            .WithColumn("enforce_limit").AsBoolean().Nullable()
            .WithColumn("kills").AsInt32().Nullable()
            .WithColumn("deaths").AsInt32().Nullable()
            .WithColumn("mercy").AsInt32().Nullable()
            .WithColumn("mercy_min").AsInt32().Nullable()
            .WithColumn("display").AsString(32).Nullable()
            .WithColumn("scoreboard_filter_key").AsString(190).Nullable()
            // the legacy <king/> marker, a no-op at proto 1.3.6 and above, kept so it round-trips
            .WithColumn("king").AsBoolean().NotNullable().WithDefaultValue(false);

        foreach (var t in new[] { "control_point", "map_score" })
        {
            Execute.Sql($"ALTER TABLE `{t}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            Create.ForeignKey($"fk_{t}_map").FromTable(t).ForeignColumn("map_id")
                  .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
            Create.Index($"ix_{t}_map").OnTable(t).OnColumn("map_id").Ascending();
        }

        Create.Index("ux_control_point_map_key").OnTable("control_point")
              .OnColumn("map_id").Ascending().OnColumn("control_point_key").Ascending().WithOptions().Unique();
        // At most one score module per map: PGM reads several <score> elements as one configuration, each
        // overwriting what the last said.
        Create.Index("ux_map_score_map").OnTable("map_score").OnColumn("map_id").Ascending().WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("control_point");
        Delete.Table("map_score");
    }
}
