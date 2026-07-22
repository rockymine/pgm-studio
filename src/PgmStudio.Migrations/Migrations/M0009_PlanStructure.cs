using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The generated plan's structural bucket key — its canonical <c>StructureSummary</c> (sorted wool families +
/// hub form + frontline form, e.g. <c>wools:donut,l|hub:ring|front:none</c>). Computed for free when a browse
/// card is pinned (the pin re-composes via <c>ComposeStages</c> anyway) and stored so the corpus stays
/// queryable by structure without ever reading labels back — the same key that becomes the verdict column
/// (G118) and the duel bucket (G120). Nullable: authored/imported rows have no generated structure, and the
/// column is populated only going forward (recompute-on-read covers the handful of pre-existing rows).
/// </summary>
[Migration(9, "Structural bucket key on the plan store")]
public sealed class M0009_PlanStructure : Migration
{
    public override void Up()
    {
        Alter.Table("plan").AddColumn("structure").AsString(255).Nullable();
        Create.Index("ix_plan_structure").OnTable("plan").OnColumn("structure").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_plan_structure").OnTable("plan");
        Delete.Column("structure").FromTable("plan");
    }
}
