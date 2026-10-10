using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// An admin's level for a rule: whether a finding citing it stops the work (<c>refuse</c>) or rides along
/// (<c>hint</c>). A rule with no row does what the configuration, or its code, says, so the table starts empty.
/// </summary>
[Migration(65, "An admin's level for a rule")]
public sealed class M0065_RuleLevels : Migration
{
    public override void Up() =>
        Create.Table("rule_level")
            .WithColumn("rule").AsString(32).PrimaryKey()
            .WithColumn("level").AsString(16).NotNullable()
            .WithColumn("set_by").AsString(64).NotNullable()
            .WithColumn("set_at").AsDateTime().NotNullable();

    public override void Down() => Delete.Table("rule_level");
}
