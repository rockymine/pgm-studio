using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A room style can stack storeys (M0012 widened again).
///
/// <para>A storey is measured by its <b>clear height</b> — the blocks of air a player stands in — rather than
/// by its courses of wall, because that is the number being decided and the only one with a floor under it. The
/// courses follow: a storey with another above it spends one more on the slab between them, and the top storey
/// spends none, since the roof is its lid.</para>
///
/// <para><c>storey_clear</c> 0 means "however tall <c>wall_height</c> says", which is what makes this migration
/// change nothing. A row with one storey is the building it always was — same wall, same windows, same roof at
/// the same course — and only a row that asks for a second storey grows a slab, a ladder and a second band of
/// windows.</para>
///
/// <para>Per-storey <em>materials</em> need no column either. Every storey falls back to the style's own wall,
/// windows and floor zones where it names none, so a stack of identical storeys is a count rather than a
/// description repeated N times. Storeys that differ from each other are a storey-style library and a table of
/// their own; this is the count.</para>
/// </summary>
[Migration(17, "A room style can stack storeys")]
public sealed class M0017_RoomStyleStoreys : Migration
{
    public override void Up()
    {
        Alter.Table("room_style")
            .AddColumn("storeys").AsInt32().NotNullable().WithDefaultValue(1)
            .AddColumn("storey_clear").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("storeys").FromTable("room_style");
        Delete.Column("storey_clear").FromTable("room_style");
    }
}
