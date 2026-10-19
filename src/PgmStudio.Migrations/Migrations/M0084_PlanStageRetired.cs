using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The plan is no longer a stage a map stands at: a map at <c>plan</c> is at <c>sketch</c>, where the tool that
/// opens it is, and its stored plan stays beside it as history. One holding no layout is given the layout a new
/// draft starts with — its ground layer and nothing on it — so it is a sketch to draw on, listed with the others.
/// Rolling back takes those layouts away again and puts <c>plan</c> back on every sketch-stage map that holds a
/// plan document; a plan-stage map that held no plan cannot be told from a fresh draft afterwards, and stays one.
/// </summary>
[Migration(84, "The plan stage is retired")]
public sealed class M0084_PlanStageRetired : Migration
{
    /// <summary>The layout <c>POST /api/sketch</c> stores for a draft stating no frame.</summary>
    private const string EmptyLayout =
        """{"layers":[{"id":"ground","name":"Ground","base_y":0,"layout":{"shapes":[],"groups":[]}}]}""";

    public override void Up() => Execute.Sql($"""
        INSERT INTO map_artifact (map_id, kind, data, revision)
        SELECT m.id, 'sketch_layout_json', '{EmptyLayout}', 1 FROM map m
        WHERE m.stage = 'plan'
          AND NOT EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id AND a.kind = 'sketch_layout_json');
        UPDATE map SET stage = 'sketch' WHERE stage = 'plan';
        """);

    public override void Down() => Execute.Sql($"""
        DELETE a FROM map_artifact a
        JOIN map m ON m.id = a.map_id
        JOIN map_artifact p ON p.map_id = m.id AND p.kind = 'plan_json'
        WHERE a.kind = 'sketch_layout_json' AND a.revision = 1 AND a.data = '{EmptyLayout}' AND m.stage = 'sketch';
        UPDATE map m SET stage = 'plan'
        WHERE m.stage = 'sketch'
          AND EXISTS (SELECT 1 FROM map_artifact a WHERE a.map_id = m.id AND a.kind = 'plan_json');
        """);
}
