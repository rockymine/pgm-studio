using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A map authored from a generator plan candidate carries a <c>plan_source_id</c> back to that
/// candidate `plan` row (provenance). Nullable — only stage=plan maps that were authored from a
/// candidate have one; sketches/imports/blank plans don't. See docs/contracts/plan-as-map.md.
/// </summary>
[Migration(10, "Plan-source provenance link on the map row")]
public sealed class M0010_MapPlanSource : Migration
{
    public override void Up()
        => Alter.Table("map").AddColumn("plan_source_id").AsInt64().Nullable();

    public override void Down()
        => Delete.Column("plan_source_id").FromTable("map");
}
