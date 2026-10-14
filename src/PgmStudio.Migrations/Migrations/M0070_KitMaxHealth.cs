using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Carry a kit's <c>&lt;max-health&gt;</c> on <c>kit</c>: the health a player is given the kit at, which a
/// flag's carrier kit lowers and a drop kit restores. NULL is a kit that states none. The column is new and
/// no stored row states one, so there is nothing to carry forward.
/// </summary>
[Migration(70, "Kit max health")]
public sealed class M0070_KitMaxHealth : Migration
{
    public override void Up() => Alter.Table("kit").AddColumn("max_health").AsDouble().Nullable();

    public override void Down() => Delete.Column("max_health").FromTable("kit");
}
