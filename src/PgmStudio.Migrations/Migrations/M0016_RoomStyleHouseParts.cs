using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A room style can ask for a house rather than a box (M0012/M0015 widened): four more roofs, a floor divided
/// across the room, windows through the walls, and a porch.
///
/// <para><c>roof_form</c> already held a word and now holds one of six — <c>hip</c>, <c>shed</c>,
/// <c>gambrel</c> and <c>saltbox</c> join the lid and the gable. No column changes for that: every form is a
/// height field over the same plan, so the stamper reads the word it always read. <c>ridge_cap</c> is the one
/// finish they share, the line the slopes meet on laid in the verge rather than in the roof's own material.</para>
///
/// <para><b>The floor's zones are parts, not columns.</b> The ring hugging the walls, the open floor and the
/// centred plate each take a material, and <c>room_style_course.part</c> is a free string, so they are three
/// more parts a course may bind and no column carries them. Only their two <em>distances</em> land here —
/// <c>border_width</c> and <c>inlay_inset</c> — because those are numbers rather than materials.</para>
///
/// <para><b>The windows are a block id, not a bound style</b>, and that is the one place this schema departs
/// from the library's shape. A stair's metadata is which way it climbs and a slab's is which half it fills, so
/// the four stairs of a lattice differ from each other by geometry alone; a bound material resolves its data
/// from where the cell sits and would turn all four the same way, which is a solid 2×2 patch rather than a
/// window. The block is therefore chosen from the block picker and the stamper supplies the nibble.</para>
///
/// <para>The porch needs no flag: <c>porch_depth</c> 0 is a building whose walls stand on the whole footprint,
/// which is what every existing row is. Every column defaults to what a row already behaves as, so nothing
/// that was saved changes shape.</para>
/// </summary>
[Migration(16, "A room style can ask for windows, a floor in zones, a porch and four more roofs")]
public sealed class M0016_RoomStyleHouseParts : Migration
{
    public override void Up()
    {
        Alter.Table("room_style")
            .AddColumn("ridge_cap").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("border_width").AsInt32().NotNullable().WithDefaultValue(1)
            .AddColumn("inlay_inset").AsInt32().NotNullable().WithDefaultValue(2)
            .AddColumn("window_form").AsString(16).NotNullable().WithDefaultValue("none")
            .AddColumn("window_block").AsInt32().NotNullable().WithDefaultValue(102)      // glass pane
            .AddColumn("window_data").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("window_sill").AsInt32().NotNullable().WithDefaultValue(2)
            .AddColumn("window_width").AsInt32().NotNullable().WithDefaultValue(2)
            .AddColumn("window_height").AsInt32().NotNullable().WithDefaultValue(2)
            .AddColumn("window_spacing").AsInt32().NotNullable().WithDefaultValue(3)
            .AddColumn("porch_depth").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("porch_inset").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("porch_edge").AsString(16).NotNullable().WithDefaultValue("front")
            .AddColumn("porch_roof").AsString(16).NotNullable().WithDefaultValue("shed")
            .AddColumn("porch_rail_block").AsInt32().NotNullable().WithDefaultValue(85);  // oak fence
    }

    public override void Down()
    {
        // A row that asked for one of the four new roofs has no word to go back to, so it lands on the gable —
        // the nearest sloped form, and the only other one the older schema could say.
        Execute.Sql("UPDATE `room_style` SET `roof_form` = 'gable' " +
                    "WHERE `roof_form` IN ('hip', 'shed', 'gambrel', 'saltbox')");

        foreach (var column in new[]
                 {
                     "ridge_cap", "border_width", "inlay_inset",
                     "window_form", "window_block", "window_data", "window_sill",
                     "window_width", "window_height", "window_spacing",
                     "porch_depth", "porch_inset", "porch_edge", "porch_roof", "porch_rail_block",
                 })
            Delete.Column(column).FromTable("room_style");

        // The floor's zones were courses rather than columns, so they leave with the parts they bound.
        Execute.Sql("DELETE FROM `room_style_course` WHERE `part` IN ('border', 'field', 'inlay')");
    }
}
