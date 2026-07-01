using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Carry potion effects and the reset <c>force</c> flag on <c>kit</c>. Spawn protection grants an infinite
/// damage-resistance effect inside the spawn and force-applies a reset kit (duration 0) outside it, so a kit
/// now needs a <c>force</c> scalar and a small list of <c>{type,duration,amplifier}</c> effects. The list is a
/// polymorphic leaf we never query per-row, so it lives in an <c>effects_json</c> column (the hybrid rule),
/// while <c>force</c> is a queryable bool column. Existing kits default to force=false / no effects.
/// </summary>
[Migration(6, "Kit potion effects + force flag")]
public sealed class M0006_KitEffects : Migration
{
    public override void Up()
    {
        Create.Column("force").OnTable("kit").AsBoolean().NotNullable().WithDefaultValue(false);
        Create.Column("effects_json").OnTable("kit").AsCustom("JSON").Nullable();
    }

    public override void Down()
    {
        Delete.Column("effects_json").FromTable("kit");
        Delete.Column("force").FromTable("kit");
    }
}
