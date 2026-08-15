using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Carry the <c>&lt;clear/&gt;</c> flag on <c>kit</c>. A spawn kit empties the inventory and armour before it
/// hands out its own items, so a player who respawns carrying what the last life left them starts the next one
/// with the kit and nothing else. It is per-kit rather than universal: a force-applied reset kit is re-applied
/// every tick, and clearing there would wipe the inventory continuously.
///
/// <para>Stored as its own queryable bool beside <c>force</c>, which it pairs with. Existing kits default to
/// clear=false, which is what every stored kit already wrote.</para>
/// </summary>
[Migration(20, "Kit clear flag")]
public sealed class M0020_KitClear : Migration
{
    public override void Up() =>
        Create.Column("clear").OnTable("kit").AsBoolean().NotNullable().WithDefaultValue(false);

    public override void Down() => Delete.Column("clear").FromTable("kit");
}
