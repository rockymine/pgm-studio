using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A roof part states its own half-course slab, and the two thickness columns nothing ever read are dropped.
///
/// <para><b>The slab.</b> A roof laid in half courses steps a slab on every odd course, and which slab it is
/// was a house-level column (<c>room_style.roof_slab</c>) alone — so a roof saved on its own carried a form,
/// a pitch and its three materials but not the block that pairs with them. The gate that refuses a slab named
/// as a whole-block roof therefore could not run at the part level, because a roof part had no column to read
/// and refusing on its absence would have refused a roof meant to pair with a slab set on the house later. A
/// roof style owns everything above the eave, so it owns this too, and a house binding one takes the roof's
/// answer the way it takes its form and its pitch.</para>
///
/// <para><b>The thicknesses.</b> <c>room_style.roof_thickness</c> (M0012) and <c>roof_style.thickness</c>
/// (M0018) were written, clamped and round-tripped, and no stamper ever read either: a roof's depth at a cell
/// comes from its own height field's step down to its neighbours, so there is nothing for a stored number to
/// say. Neither was ever offered as a knob, which is the only reason a stored value never misled an
/// author.</para>
/// </summary>
[Migration(21, "Roof-part slab; drop the two unread thickness columns")]
public sealed class M0021_RoofSlabAndThickness : Migration
{
    public override void Up()
    {
        // -1 is "no slab", the same absent value room_style.roof_slab carries and the same one a roof laid in
        // whole courses has always had.
        Create.Column("roof_slab").OnTable("roof_style").AsInt32().NotNullable().WithDefaultValue(-1);
        Create.Column("roof_slab_data").OnTable("roof_style").AsInt32().NotNullable().WithDefaultValue(0);

        Delete.Column("thickness").FromTable("roof_style");
        Delete.Column("roof_thickness").FromTable("room_style");
    }

    public override void Down()
    {
        Create.Column("thickness").OnTable("roof_style").AsInt32().NotNullable().WithDefaultValue(1);
        Create.Column("roof_thickness").OnTable("room_style").AsInt32().NotNullable().WithDefaultValue(1);

        Delete.Column("roof_slab_data").FromTable("roof_style");
        Delete.Column("roof_slab").FromTable("roof_style");
    }
}
