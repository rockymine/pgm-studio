using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The plan is no longer a stage a map stands at: a map at <c>plan</c> is at <c>sketch</c>, where the tool that
/// opens it is, and its stored plan stays beside it as history. Rolling back puts <c>plan</c> back on every
/// sketch-stage map that holds a plan document, which is every map the step moved.
/// </summary>
[Migration(84, "The plan stage is retired")]
public sealed class M0084_PlanStageRetired : Migration
{
    public override void Up() => Execute.Sql("UPDATE map SET stage = 'sketch' WHERE stage = 'plan';");

    public override void Down() => Execute.Sql("""
        UPDATE map m SET stage = 'plan'
        WHERE m.stage = 'sketch'
          AND EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id AND a.kind = 'plan_json');
        """);
}
