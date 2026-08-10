using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Verdict collection (G118) — the browse feed's absolute up/down votes, the first labeled dataset of the
/// generator feedback loop (duel preference pairs are a separate dataset by design, G120).
/// <para><c>plan_verdict</c> holds one row per plan — the author's <b>current</b> judgment of that board,
/// updated in place as tags and note are refined; the JSONL export is the dataset snapshot. Each row
/// carries the verdict (<c>up</c>/<c>down</c>), the annotation tags, a free-text note, and the evaluator's
/// reading at vote time (score + per-term snapshot + evaluator version) — so a downvote tagged with a rule
/// whose term did not fire is a ready-made evaluator bug report.</para>
/// <para><c>plan.pinned</c> separates the two reasons a generated row persists: pinned (in the hold tray) and
/// voted (in the labeled corpus). Every pre-existing generated row was a pin, so the column defaults true;
/// a vote persists its plan with <c>pinned = false</c>, and pinning later promotes it.</para>
/// </summary>
[Migration(15, "Verdict collection on the plan store")]
public sealed class M0015_PlanVerdict : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Alter.Table("plan").AddColumn("pinned").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.Table("plan_verdict")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("plan_id").AsInt64().NotNullable()
            .WithColumn("verdict").AsString(8).NotNullable()                     // up | down
            .WithColumn("tags_json").AsCustom(Json).NotNullable()                // ["wools-too-close", …]
            .WithColumn("note").AsString(2000).Nullable()
            .WithColumn("score").AsDouble().NotNullable()                        // evaluator score at vote time
            .WithColumn("terms_json").AsCustom(Json).NotNullable()               // per-term snapshot at vote time
            .WithColumn("evaluator_version").AsString(32).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Execute.Sql("ALTER TABLE `plan_verdict` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        // One judgment per plan, refined in place. Cascade is a formality: the store demotes a voted plan to
        // unpinned instead of deleting it, so the cascade only fires if a row is removed around the store.
        Create.ForeignKey("fk_plan_verdict_plan").FromTable("plan_verdict").ForeignColumn("plan_id")
              .ToTable("plan").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ux_plan_verdict_plan").OnTable("plan_verdict").OnColumn("plan_id").Unique();
    }

    public override void Down()
    {
        Delete.Table("plan_verdict");
        Delete.Column("pinned").FromTable("plan");
    }
}
